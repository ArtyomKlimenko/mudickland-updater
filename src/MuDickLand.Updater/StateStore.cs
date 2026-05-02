using System.Text.Json;

namespace MuDickLand.Updater;

public sealed class StateStore
{
    private readonly AppLogger _logger;
    private readonly string _path;

    public StateStore(AppLogger logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MuDickLandUpdater");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "state.json");
    }

    public UpdaterState Load()
    {
        if (!File.Exists(_path))
        {
            return new UpdaterState();
        }

        try
        {
            return JsonSerializer.Deserialize<UpdaterState>(
                File.ReadAllText(_path),
                JsonDefaults.Options) ?? new UpdaterState();
        }
        catch (Exception ex)
        {
            _logger.Write("Failed to read state.json: " + ex.Message);
            return new UpdaterState();
        }
    }

    public void Save(UpdaterState state)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(state, JsonDefaults.Options));
        }
        catch (Exception ex)
        {
            _logger.Write("Failed to write state.json: " + ex.Message);
        }
    }
}

