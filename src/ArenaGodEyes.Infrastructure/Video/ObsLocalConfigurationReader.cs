using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArenaGodEyes.Infrastructure.Video;

public sealed class ObsLocalConfigurationReader
{
    public bool TryEnableServer()
    {
        try
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                return false;
            }

            var root = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject();
            if (root is null)
            {
                return false;
            }

            root["server_enabled"] = true;
            File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public ObsLocalConfiguration Read()
    {
        try
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                return ObsLocalConfiguration.Missing;
            }

            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return new ObsLocalConfiguration(
                Exists: true,
                ServerEnabled: root.TryGetProperty("server_enabled", out var serverEnabled) && serverEnabled.GetBoolean(),
                AuthRequired: root.TryGetProperty("auth_required", out var authRequired) && authRequired.GetBoolean(),
                ServerPort: root.TryGetProperty("server_port", out var serverPort) ? serverPort.GetInt32() : null,
                ServerPassword: root.TryGetProperty("server_password", out var serverPassword) ? serverPassword.GetString() : null);
        }
        catch
        {
            return ObsLocalConfiguration.Missing;
        }
    }

    private static string GetConfigPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "obs-studio", "plugin_config", "obs-websocket", "config.json");
    }
}

public sealed record ObsLocalConfiguration(
    bool Exists,
    bool ServerEnabled,
    bool AuthRequired,
    int? ServerPort,
    string? ServerPassword)
{
    public static ObsLocalConfiguration Missing { get; } = new(false, false, false, null, null);
}
