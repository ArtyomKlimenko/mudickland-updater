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
}
