using System.Net.Http.Json;

namespace MuDickLand.Updater;

public sealed class TelemetryClient
{
    private readonly HttpClient _http;
    private readonly UpdaterConfig _config;
    private readonly UpdaterState _state;
    private readonly AppLogger _logger;

    public TelemetryClient(HttpClient http, UpdaterConfig config, UpdaterState state, AppLogger logger)
    {
        _http = http;
        _config = config;
        _state = state;
        _logger = logger;
    }

    public async Task SendAsync(
        string eventName,
        string status,
        string packVersion,
        CancellationToken cancellationToken)
    {
        if (!_state.TelemetryEnabled || string.IsNullOrWhiteSpace(_config.TelemetryUrl))
        {
            return;
        }
        if (!TransportPolicy.IsAllowedHttpUri(_config.TelemetryUrl))
        {
            _logger.Write("Telemetry disabled because telemetryUrl is not HTTPS or localhost HTTP.");
            return;
        }

        try
        {
            var payload = new
            {
                eventName,
                status,
                appVersion = UpdaterConfig.AppVersion,
                packVersion,
                installId = _state.InstallId,
                occurredAt = DateTimeOffset.UtcNow
            };

            using var response = await _http.PostAsJsonAsync(
                _config.TelemetryUrl,
                payload,
                JsonDefaults.Options,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Write("Telemetry send failed: " + ex.Message);
        }
    }
}
