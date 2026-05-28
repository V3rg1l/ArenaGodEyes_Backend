using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.Video;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class FirstRunBootstrapService : IFirstRunBootstrapService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IWowInstallationDetector _wowInstallationDetector;
    private readonly IAddonStatusService _addonStatusService;
    private readonly IAddonInstallerService _addonInstallerService;
    private readonly ObsLocalConfigurationReader _obsLocalConfigurationReader;

    public FirstRunBootstrapService(
        IAppSettingsService appSettingsService,
        IWowInstallationDetector wowInstallationDetector,
        IAddonStatusService addonStatusService,
        IAddonInstallerService addonInstallerService,
        ObsLocalConfigurationReader obsLocalConfigurationReader)
    {
        _appSettingsService = appSettingsService;
        _wowInstallationDetector = wowInstallationDetector;
        _addonStatusService = addonStatusService;
        _addonInstallerService = addonInstallerService;
        _obsLocalConfigurationReader = obsLocalConfigurationReader;
    }

    public Task<FirstRunBootstrapStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        BuildStatusAsync([], cancellationToken);

    public async Task<FirstRunBootstrapStatus> RunAsync(CancellationToken cancellationToken = default)
    {
        var fixes = new List<string>();
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var dirty = false;

        if (string.IsNullOrWhiteSpace(settings.WowRetailPath))
        {
            var detectedWowPath = await _wowInstallationDetector.DetectWowRetailPathAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(detectedWowPath))
            {
                settings.WowRetailPath = detectedWowPath;
                settings.CombatLogDirectory = Path.Combine(detectedWowPath, "Logs");
                settings.AddonDirectory = Path.Combine(detectedWowPath, "Interface", "AddOns", "ArenaGodEyes");
                dirty = true;
                fixes.Add("detected_wow_installation");
            }
        }

        var obsLocalConfiguration = _obsLocalConfigurationReader.Read();
        if (obsLocalConfiguration.Exists)
        {
            if (obsLocalConfiguration.ServerPort.HasValue && settings.ObsPort != obsLocalConfiguration.ServerPort.Value)
            {
                settings.ObsPort = obsLocalConfiguration.ServerPort.Value;
                dirty = true;
                fixes.Add("synced_obs_port");
            }

            if (string.IsNullOrWhiteSpace(settings.ObsPassword) &&
                obsLocalConfiguration.AuthRequired &&
                !string.IsNullOrWhiteSpace(obsLocalConfiguration.ServerPassword))
            {
                settings.ObsPassword = obsLocalConfiguration.ServerPassword;
                dirty = true;
                fixes.Add("synced_obs_password");
            }

            if (!obsLocalConfiguration.ServerEnabled && _obsLocalConfigurationReader.TryEnableServer())
            {
                fixes.Add("enabled_obs_websocket");
            }
        }

        if (dirty)
        {
            settings = await _appSettingsService.UpdateAsync(settings, cancellationToken);
        }
        else
        {
            settings = await _appSettingsService.GetAsync(cancellationToken);
        }

        var addonStatus = await _addonStatusService.GetStatusAsync(settings.AddonDirectory, cancellationToken);
        if (!addonStatus.Installed && !string.IsNullOrWhiteSpace(settings.WowRetailPath))
        {
            var installedStatus = await _addonInstallerService.InstallAsync(settings.WowRetailPath, cancellationToken);
            if (installedStatus.Installed)
            {
                fixes.Add("installed_addon");
            }
        }

        return await BuildStatusAsync(fixes, cancellationToken);
    }

    private async Task<FirstRunBootstrapStatus> BuildStatusAsync(
        IReadOnlyList<string> fixes,
        CancellationToken cancellationToken)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var addonStatus = await _addonStatusService.GetStatusAsync(settings.AddonDirectory, cancellationToken);
        var obsLocalConfiguration = _obsLocalConfigurationReader.Read();

        var recordingDirectoriesReady =
            !string.IsNullOrWhiteSpace(settings.RecordingDirectory) &&
            Directory.Exists(settings.RecordingDirectory) &&
            !string.IsNullOrWhiteSpace(settings.RecordingCacheDirectory) &&
            Directory.Exists(settings.RecordingCacheDirectory);

        var pendingActions = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.WowRetailPath))
        {
            pendingActions.Add("select_wow_folder");
        }

        if (!addonStatus.Installed)
        {
            pendingActions.Add("install_addon");
        }

        return new FirstRunBootstrapStatus(
            !string.IsNullOrWhiteSpace(settings.WowRetailPath),
            addonStatus.Installed,
            obsLocalConfiguration.Exists,
            obsLocalConfiguration.Exists && obsLocalConfiguration.ServerEnabled,
            recordingDirectoriesReady,
            fixes,
            pendingActions);
    }
}
