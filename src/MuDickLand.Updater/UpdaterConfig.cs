using System.Text.Json;

namespace MuDickLand.Updater;

public sealed class UpdaterConfig
{
    public const string AppVersion = "0.1.2";
    public const string DefaultInstallFolderName = ".minecraft-pz-exp";

    public string LatestUrl { get; set; } = "http://127.0.0.1:8088/downloads/experimental/latest.json";
    public string SiteUrl { get; set; } = "http://127.0.0.1:8088/";
    public string TelegramUrl { get; set; } = "https://t.me/pz_family_chat_bot";
    public string SupportUrl { get; set; } = "https://github.com/ArtyomKlimenko/mudickland-updater/issues";
    public string TelemetryUrl { get; set; } = "";
    public string LauncherPath { get; set; } = "";
    public bool AllowInsecureHttp { get; set; }

    public static UpdaterConfig Load(AppLogger logger)
    {
        var config = new UpdaterConfig();
        var path = Path.Combine(AppContext.BaseDirectory, "updater.json");
        if (!File.Exists(path))
        {
            return config;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<UpdaterConfig>(
                File.ReadAllText(path),
                JsonDefaults.Options);
            return loaded ?? config;
        }
        catch (Exception ex)
        {
            logger.Write("Failed to read updater.json: " + ex.Message);
            return config;
        }
    }

    public const string ManifestPublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAw/c1GNW/O9sr7Fx/WqcR
Rl4JghaBkGfRxIcMTLdAt/PsvyOXSRQcWaTLluFeC6IZ4YaT+BoywstvSuJBbrBa
8KU5jbJLii6VhWgGaizFtmxYem9eHF69yAibg/eH2DGMNtVT4oVWrka4TfzOnv6c
+GS0alw8eUPlBbY4yOYB1lTT3XAQZjd36oFtqwYKz2FYY7QTnOegfQBAjLEil8US
5UagvwY4KBX6c4FQ3GPuH/OnRNFBInoe5MpeksvXnYuARavkQVPbzS27Z8pduVCs
sx3XkApzo5nvW274tLbr8EkwctqyCd1DiZpOxyaQPn6ARCV4FxeU1ALaqMgoBB9e
PTRvcliAaRn4eiv9DJAyQQtx03oD29HdSYevriw9wpnQ/jfJy0/aV7PvqxIwq2fH
ym1iVW0R/Kc5yHKiWbVGMafqx4FppBTgWq7v/u1fKtihF+UBtYkcQCaVzXqWPWdg
czNjhUGX3uMY8Fx1bZi+nUs18OKO5bmomlJva08jis5hAgMBAAE=
-----END PUBLIC KEY-----
""";
}
