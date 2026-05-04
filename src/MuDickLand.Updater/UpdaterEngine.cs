using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MuDickLand.Updater;

public sealed class UpdaterEngine
{
    private const int MaxParallelDownloads = 4;
    private const int MaxDownloadAttempts = 5;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly UpdaterConfig _config;
    private readonly UpdaterState _state;
    private readonly AppLogger _logger;
    private readonly object _hashCacheLock = new();

    public UpdaterEngine(HttpClient http, UpdaterConfig config, UpdaterState state, AppLogger logger)
    {
        _http = http;
        _config = config;
        _state = state;
        _logger = logger;
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuDickLandUpdater", UpdaterConfig.AppVersion));
    }

    public async Task<UpdatePlan> BuildPlanAsync(
        string installDir,
        IProgress<UpdaterProgress> progress,
        CancellationToken cancellationToken)
    {
        TransportPolicy.RequireAllowedHttpUri(_config.LatestUrl, "latestUrl", _config.AllowInsecureHttp);
        progress.Report(new UpdaterProgress { Message = "Fetching latest index...", Percent = 2 });
        var latest = await GetJsonAsync<LatestIndex>(_config.LatestUrl, cancellationToken);
        if (latest is null || string.IsNullOrWhiteSpace(latest.ManifestUrl) || string.IsNullOrWhiteSpace(latest.SignatureUrl))
        {
            throw new InvalidOperationException("latest.json is missing manifestUrl or signatureUrl.");
        }
        TransportPolicy.RequireAllowedHttpUri(latest.ManifestUrl, "manifestUrl", _config.AllowInsecureHttp);
        TransportPolicy.RequireAllowedHttpUri(latest.SignatureUrl, "signatureUrl", _config.AllowInsecureHttp);

        progress.Report(new UpdaterProgress { Message = "Downloading signed manifest...", Percent = 8 });
        var manifestBytes = await _http.GetByteArrayAsync(latest.ManifestUrl, cancellationToken);
        var signatureBytes = await _http.GetByteArrayAsync(latest.SignatureUrl, cancellationToken);

        progress.Report(new UpdaterProgress { Message = "Verifying manifest signature...", Percent = 12 });
        if (!Security.VerifyManifestSignature(manifestBytes, signatureBytes))
        {
            throw new InvalidOperationException("Manifest signature verification failed.");
        }

        var manifest = JsonSerializer.Deserialize<PackManifest>(manifestBytes, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Could not parse manifest.json.");

        if (!string.IsNullOrWhiteSpace(latest.PackId) && manifest.PackId != latest.PackId)
        {
            throw new InvalidOperationException($"Manifest packId mismatch: {manifest.PackId} != {latest.PackId}");
        }
        if (latest.ReleaseNumber > 0 && manifest.ReleaseNumber != latest.ReleaseNumber)
        {
            throw new InvalidOperationException($"Manifest releaseNumber mismatch: {manifest.ReleaseNumber} != {latest.ReleaseNumber}");
        }
        if (manifest.ReleaseNumber <= 0)
        {
            throw new InvalidOperationException("Manifest does not declare releaseNumber.");
        }
        if (_state.LastReleaseNumbers.TryGetValue(manifest.PackId, out var lastRelease)
            && manifest.ReleaseNumber < lastRelease)
        {
            throw new InvalidOperationException($"Refusing downgrade/replay: release {manifest.ReleaseNumber} is older than installed {lastRelease}.");
        }

        var managedDirs = manifest.ManagedDirs
            .Select(PathSafety.NormalizeManifestPath)
            .Select(path => path.Split('/')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (managedDirs.Count == 0)
        {
            throw new InvalidOperationException("Manifest does not declare managedDirs.");
        }

        foreach (var file in manifest.Files)
        {
            file.Path = PathSafety.NormalizeManifestPath(file.Path);
            if (!PathSafety.IsUnderManagedDir(file.Path, managedDirs))
            {
                throw new InvalidOperationException("Manifest file is outside managed directories: " + file.Path);
            }

            if (file.Sha256.Length != 64 || file.Sha256.Any(ch => !Uri.IsHexDigit(ch)))
            {
                throw new InvalidOperationException("Manifest file has invalid SHA-256: " + file.Path);
            }

            TransportPolicy.RequireAllowedHttpUri(file.Url, "fileUrl", _config.AllowInsecureHttp);
        }

        Directory.CreateDirectory(installDir);
        var downloads = new List<ManifestFile>();
        for (var index = 0; index < manifest.Files.Count; index++)
        {
            var file = manifest.Files[index];
            var target = PathSafety.CombineUnderRoot(installDir, file.Path);
            var percent = 12 + (int)(30.0 * (index + 1) / Math.Max(1, manifest.Files.Count));
            progress.Report(new UpdaterProgress { Message = "Checking " + file.Path, Percent = percent });

            if (!File.Exists(target))
            {
                RemoveHashCache(file.Path);
                downloads.Add(file);
                continue;
            }

            var targetInfo = new FileInfo(target);
            if (targetInfo.Length != file.Size)
            {
                RemoveHashCache(file.Path);
                downloads.Add(file);
                continue;
            }

            if (TryGetCachedHash(file.Path, targetInfo, out var cachedHash))
            {
                if (cachedHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                downloads.Add(file);
                continue;
            }

            var hash = await Security.Sha256FileAsync(target, cancellationToken);
            UpdateHashCache(file.Path, targetInfo, hash);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                downloads.Add(file);
            }
        }

        var deletes = manifest.DeletePolicy.Enabled
            ? FindDeletes(installDir, manifest, managedDirs)
            : [];

        progress.Report(new UpdaterProgress { Message = "Plan ready.", Percent = 45 });
        return new UpdatePlan
        {
            Manifest = manifest,
            Downloads = downloads,
            Deletes = deletes
        };
    }

    public async Task ApplyPlanAsync(
        string installDir,
        UpdatePlan plan,
        IProgress<UpdaterProgress> progress,
        CancellationToken cancellationToken)
    {
        var cacheDir = Path.Combine(installDir, ".mudickland-cache");
        Directory.CreateDirectory(cacheDir);

        var completedDownloads = 0;
        if (plan.Downloads.Count > 0)
        {
            progress.Report(new UpdaterProgress { Message = "Downloading files...", Percent = 45 });
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxParallelDownloads,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(plan.Downloads, parallelOptions, async (file, token) =>
        {
            await DownloadAndInstallFileAsync(installDir, cacheDir, file, token);

            var completed = Interlocked.Increment(ref completedDownloads);
            progress.Report(new UpdaterProgress
            {
                Message = "Downloaded " + file.Path,
                Percent = 45 + (int)(45.0 * completed / Math.Max(1, plan.Downloads.Count))
            });
        });

        for (var index = 0; index < plan.Deletes.Count; index++)
        {
            var path = plan.Deletes[index];
            var percent = 90 + (int)(8.0 * (index + 1) / Math.Max(1, plan.Deletes.Count));
            progress.Report(new UpdaterProgress { Message = "Deleting stale file " + path, Percent = percent });
            DeleteManagedFile(installDir, path);
            RemoveHashCache(path);
        }

        CleanupEmptyManagedDirectories(installDir, plan.Manifest.ManagedDirs);
        progress.Report(new UpdaterProgress { Message = "Update complete.", Percent = 100 });
        _logger.Write($"Updated {plan.Manifest.PackId} {plan.Manifest.Version}: downloads={plan.Downloads.Count}, deletes={plan.Deletes.Count}");
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        await using var stream = await _http.GetStreamAsync(url, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, cancellationToken);
    }

    private List<string> FindDeletes(
        string installDir,
        PackManifest manifest,
        HashSet<string> managedDirs)
    {
        var expected = manifest.Files
            .Select(file => file.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deletes = new List<string>();

        foreach (var managedDir in managedDirs)
        {
            var fullDir = PathSafety.CombineUnderRoot(installDir, managedDir);
            if (!Directory.Exists(fullDir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullDir, "*", SearchOption.AllDirectories))
            {
                if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(installDir, file).Replace('\\', '/');
                relative = PathSafety.NormalizeManifestPath(relative);
                if (!expected.Contains(relative))
                {
                    deletes.Add(relative);
                }
            }
        }

        return deletes;
    }

    private async Task DownloadAndInstallFileAsync(
        string installDir,
        string cacheDir,
        ManifestFile file,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(cacheDir, file.Sha256 + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var target = PathSafety.CombineUnderRoot(installDir, file.Path);
        var targetDir = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("Invalid target path: " + file.Path);

        PathSafety.EnsureNoReparsePointInExistingPath(installDir, targetDir);
        Directory.CreateDirectory(targetDir);

        using (var response = await GetFileWithRetryAsync(file, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(tempPath);
            await input.CopyToAsync(output, cancellationToken);
        }

        var info = new FileInfo(tempPath);
        if (info.Length != file.Size)
        {
            File.Delete(tempPath);
            throw new InvalidOperationException($"Downloaded size mismatch for {file.Path}: {info.Length} != {file.Size}");
        }

        var hash = await Security.Sha256FileAsync(tempPath, cancellationToken);
        if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException("Downloaded hash mismatch for " + file.Path);
        }

        if (File.Exists(target) && File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException("Refusing to replace reparse point: " + target);
        }

        File.Move(tempPath, target, overwrite: true);
        UpdateHashCache(file.Path, new FileInfo(target), file.Sha256);
    }

    private async Task<HttpResponseMessage> GetFileWithRetryAsync(ManifestFile file, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!ShouldRetry(response.StatusCode) || attempt >= MaxDownloadAttempts)
                {
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                _logger.Write($"Retrying download for {file.Path}: HTTP {(int)response.StatusCode}, attempt {attempt}/{MaxDownloadAttempts}, delay={delay.TotalSeconds:0.###}s");
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxDownloadAttempts)
            {
                response?.Dispose();
                var delay = GetRetryDelay(null, attempt);
                _logger.Write($"Retrying download for {file.Path}: {ex.Message}, attempt {attempt}/{MaxDownloadAttempts}, delay={delay.TotalSeconds:0.###}s");
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout
            || code >= 500;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
            {
                return Min(delta, MaxRetryDelay);
            }

            if (retryAfter.Date is { } date)
            {
                var delay = date - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    return Min(delay, MaxRetryDelay);
                }
            }
        }

        var exponentialSeconds = Math.Pow(2, attempt - 1);
        var jitterMs = Random.Shared.Next(100, 500);
        return Min(TimeSpan.FromSeconds(exponentialSeconds) + TimeSpan.FromMilliseconds(jitterMs), MaxRetryDelay);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return left <= right ? left : right;
    }

    private bool TryGetCachedHash(string normalizedPath, FileInfo info, out string hash)
    {
        hash = "";
        lock (_hashCacheLock)
        {
            if (!_state.FileHashCache.TryGetValue(normalizedPath, out var entry))
            {
                return false;
            }

            if (entry.Size != info.Length || entry.LastWriteTimeUtcTicks != info.LastWriteTimeUtc.Ticks)
            {
                _state.FileHashCache.Remove(normalizedPath);
                return false;
            }

            hash = entry.Sha256;
            return hash.Length == 64;
        }
    }

    private void UpdateHashCache(string normalizedPath, FileInfo info, string sha256)
    {
        lock (_hashCacheLock)
        {
            _state.FileHashCache[normalizedPath] = new FileHashCacheEntry
            {
                Size = info.Length,
                LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
                Sha256 = sha256
            };
        }
    }

    private void RemoveHashCache(string normalizedPath)
    {
        lock (_hashCacheLock)
        {
            _state.FileHashCache.Remove(normalizedPath);
        }
    }

    private static void DeleteManagedFile(string installDir, string normalizedPath)
    {
        var target = PathSafety.CombineUnderRoot(installDir, normalizedPath);
        if (!File.Exists(target))
        {
            return;
        }

        if (File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        File.Delete(target);
    }

    private static void CleanupEmptyManagedDirectories(string installDir, IEnumerable<string> managedDirs)
    {
        foreach (var dir in managedDirs)
        {
            var fullDir = PathSafety.CombineUnderRoot(installDir, PathSafety.NormalizeManifestPath(dir));
            if (!Directory.Exists(fullDir))
            {
                continue;
            }

            foreach (var child in Directory.EnumerateDirectories(fullDir, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(child).Any())
                {
                    Directory.Delete(child);
                }
            }
        }
    }
}
