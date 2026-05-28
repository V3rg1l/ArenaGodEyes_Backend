using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Core.Application.Abstractions.Time;
using ArenaGodEyes.Infrastructure.Video;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly IAppSettingsRepository _repository;
    private readonly IUtcNowProvider _utcNowProvider;
    private readonly ObsLocalConfigurationReader _obsLocalConfigurationReader;

    public AppSettingsService(
        IAppSettingsRepository repository,
        IUtcNowProvider utcNowProvider,
        ObsLocalConfigurationReader obsLocalConfigurationReader)
    {
        _repository = repository;
        _utcNowProvider = utcNowProvider;
        _obsLocalConfigurationReader = obsLocalConfigurationReader;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return ApplyDerivedDefaults(settings, _obsLocalConfigurationReader.Read());
    }

    public async Task<AppSettings> UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var existingSettings = await _repository.GetAsync(cancellationToken);

        settings.Id = existingSettings.Id;
        settings.CreatedAt = existingSettings.CreatedAt == default
            ? _utcNowProvider.UtcNow
            : existingSettings.CreatedAt;
        settings.UpdatedAt = _utcNowProvider.UtcNow;

        settings = ApplyDerivedDefaults(settings, _obsLocalConfigurationReader.Read());

        return await _repository.SaveAsync(settings, cancellationToken);
    }

    private static AppSettings ApplyDerivedDefaults(AppSettings settings, ObsLocalConfiguration obsLocalConfiguration)
    {
        if (!string.IsNullOrWhiteSpace(settings.WowRetailPath))
        {
            settings.CombatLogDirectory ??= Path.Combine(settings.WowRetailPath, "Logs");
            settings.AddonDirectory ??= Path.Combine(settings.WowRetailPath, "Interface", "AddOns", "ArenaGodEyes");
        }

        settings.RecordingDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "ArenaGodEyes",
            "Recordings");

        settings.RecordingCacheDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArenaGodEyes",
            "temp");

        if (!string.IsNullOrWhiteSpace(settings.RecordingDirectory))
        {
            Directory.CreateDirectory(settings.RecordingDirectory);
        }

        if (!string.IsNullOrWhiteSpace(settings.RecordingCacheDirectory))
        {
            Directory.CreateDirectory(settings.RecordingCacheDirectory);
        }

        if (obsLocalConfiguration.Exists)
        {
            settings.ObsPort = obsLocalConfiguration.ServerPort ?? settings.ObsPort;

            if (string.IsNullOrWhiteSpace(settings.ObsPassword) && obsLocalConfiguration.AuthRequired)
            {
                settings.ObsPassword = obsLocalConfiguration.ServerPassword;
            }
        }

        return settings;
    }
}
