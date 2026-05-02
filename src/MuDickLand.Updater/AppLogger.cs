namespace MuDickLand.Updater;

public sealed class AppLogger
{
    public string LogPath { get; }

    public AppLogger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MuDickLandUpdater");
        Directory.CreateDirectory(dir);
        LogPath = Path.Combine(dir, "updater.log");
    }

    public void Write(string message)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never break updates.
        }
    }
}

