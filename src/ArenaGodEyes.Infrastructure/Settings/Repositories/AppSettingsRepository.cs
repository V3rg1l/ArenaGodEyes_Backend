using System.Text.Json;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Repositories;

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IFileSystem _fileSystem;
    private readonly WorkspacePaths _workspacePaths;

    public AppSettingsRepository(IFileSystem fileSystem, WorkspacePaths workspacePaths)
    {
        _fileSystem = fileSystem;
        _workspacePaths = workspacePaths;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settingsPath = GetSettingsPath();
        if (!_fileSystem.FileExists(settingsPath))
        {
            return CreateDefaultSettings();
        }

        var json = await _fileSystem.ReadAllTextAsync(settingsPath, cancellationToken);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

        return settings ?? CreateDefaultSettings();
    }

    public async Task<AppSettings> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var settingsPath = GetSettingsPath();
        var settingsDirectory = Path.GetDirectoryName(settingsPath)!;

        if (!_fileSystem.DirectoryExists(settingsDirectory))
        {
            _fileSystem.CreateDirectory(settingsDirectory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await _fileSystem.WriteAllTextAsync(settingsPath, json, cancellationToken);

        return settings;
    }

    private AppSettings CreateDefaultSettings()
    {
        var wowRetailPath = TryGetDefaultWowRetailPath();
        var now = DateTimeOffset.UtcNow;

        return new AppSettings
        {
            WowRetailPath = wowRetailPath,
            CombatLogDirectory = wowRetailPath is null ? null : Path.Combine(wowRetailPath, "Logs"),
            AddonDirectory = wowRetailPath is null ? null : Path.Combine(wowRetailPath, "Interface", "AddOns", "ArenaGodEyes"),
            RecordingDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "ArenaGodEyes",
                "Recordings"),
            RecordingCacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArenaGodEyes",
                "temp"),
            ObsHost = "127.0.0.1",
            ObsPort = 4455,
            ObsConnectTimeoutSeconds = 5,
            FfmpegExecutablePath = null,
            FfprobeExecutablePath = null,
            VideoThumbnailSecond = 5,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private string GetSettingsPath() =>
        Path.Combine(_workspacePaths.BackendRootPath, "data", "settings", "app-settings.json");

    private string? TryGetDefaultWowRetailPath()
    {
        var defaultPath = @"C:\Program Files (x86)\World of Warcraft\_retail_";
        return _fileSystem.DirectoryExists(defaultPath) ? defaultPath : null;
    }
}
