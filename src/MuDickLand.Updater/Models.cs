namespace MuDickLand.Updater;

public sealed class LatestIndex
{
    public string PackId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public long ReleaseNumber { get; set; }
    public string ManifestUrl { get; set; } = "";
    public string SignatureUrl { get; set; } = "";
    public string RequiredUpdaterVersion { get; set; } = "";
    public string ChangelogUrl { get; set; } = "";
    public string UpdaterDownloadUrl { get; set; } = "";
    public string UpdaterPageUrl { get; set; } = "";
    public string UpdaterMessage { get; set; } = "";
}

public sealed class PackManifest
{
    public string PackId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Version { get; set; } = "";
    public long ReleaseNumber { get; set; }
    public List<string> ManagedDirs { get; set; } = [];
    public ManifestDeletePolicy DeletePolicy { get; set; } = new();
    public List<ManifestFile> Files { get; set; } = [];
}

public sealed class ManifestDeletePolicy
{
    public bool Enabled { get; set; } = true;
    public List<string> Globs { get; set; } = [];
}

public sealed class ManifestFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class UpdatePlan
{
    public required PackManifest Manifest { get; init; }
    public required List<ManifestFile> Downloads { get; init; }
    public required List<string> Deletes { get; init; }
    public long BytesToDownload => Downloads.Sum(file => file.Size);
}

public sealed class UpdaterProgress
{
    public string Message { get; init; } = "";
    public int Percent { get; init; }
}

public sealed class UpdaterState
{
    public string InstallId { get; set; } = Guid.NewGuid().ToString("D");
    public string InstallDir { get; set; } = "";
    public bool TelemetryEnabled { get; set; } = true;
    public Dictionary<string, long> LastReleaseNumbers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, FileHashCacheEntry> FileHashCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FileHashCacheEntry
{
    public long Size { get; set; }
    public long LastWriteTimeUtcTicks { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class UpdaterOutdatedException : Exception
{
    public UpdaterOutdatedException(
        string currentVersion,
        string requiredVersion,
        string downloadUrl,
        string pageUrl,
        string customMessage)
        : base(BuildMessage(currentVersion, requiredVersion, downloadUrl, pageUrl, customMessage))
    {
        CurrentVersion = currentVersion;
        RequiredVersion = requiredVersion;
        DownloadUrl = downloadUrl;
        PageUrl = pageUrl;
        CustomMessage = customMessage;
    }

    public string CurrentVersion { get; }
    public string RequiredVersion { get; }
    public string DownloadUrl { get; }
    public string PageUrl { get; }
    public string CustomMessage { get; }

    private static string BuildMessage(
        string currentVersion,
        string requiredVersion,
        string downloadUrl,
        string pageUrl,
        string customMessage)
    {
        var text = string.IsNullOrWhiteSpace(customMessage)
            ? "Обновите апдейтер, чтобы поставить свежую experimental-сборку."
            : customMessage.Trim();
        var target = !string.IsNullOrWhiteSpace(downloadUrl) ? downloadUrl : pageUrl;
        var suffix = string.IsNullOrWhiteSpace(target) ? "" : Environment.NewLine + target;
        return $"{text}{Environment.NewLine}Текущая версия: {currentVersion}. Требуется: {requiredVersion}.{suffix}";
    }
}
