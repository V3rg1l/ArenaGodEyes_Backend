using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class SettingsValidationService : ISettingsValidationService
{
    private readonly IAddonStatusService _addonStatusService;
    private readonly IFileSystem _fileSystem;

    public SettingsValidationService(IAddonStatusService addonStatusService, IFileSystem fileSystem)
    {
        _addonStatusService = addonStatusService;
        _fileSystem = fileSystem;
    }

    public async Task<SettingsValidationResult> ValidateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();

        var wowRetailPathExists = !string.IsNullOrWhiteSpace(settings.WowRetailPath) &&
                                  _fileSystem.DirectoryExists(settings.WowRetailPath);
        if (!wowRetailPathExists)
        {
            messages.Add("WoW retail path was not found.");
        }

        var combatLogDirectoryExists = !string.IsNullOrWhiteSpace(settings.CombatLogDirectory) &&
                                       _fileSystem.DirectoryExists(settings.CombatLogDirectory);
        if (!combatLogDirectoryExists)
        {
            messages.Add("Combat log directory was not found.");
        }

        var addonDirectoryExists = !string.IsNullOrWhiteSpace(settings.AddonDirectory) &&
                                   _fileSystem.DirectoryExists(settings.AddonDirectory);
        if (!addonDirectoryExists)
        {
            messages.Add("Addon directory was not found.");
        }

        var addonStatus = await _addonStatusService.GetStatusAsync(settings.AddonDirectory, cancellationToken);
        if (!addonStatus.Installed)
        {
            messages.Add("ArenaGodEyes addon is not installed correctly.");
        }

        if (!string.IsNullOrWhiteSpace(settings.RecordingDirectory) &&
            !_fileSystem.DirectoryExists(settings.RecordingDirectory))
        {
            messages.Add("Recording directory was not found.");
        }

        if (!string.IsNullOrWhiteSpace(settings.RecordingCacheDirectory) &&
            !_fileSystem.DirectoryExists(settings.RecordingCacheDirectory))
        {
            messages.Add("Recording cache directory was not found.");
        }

        return new SettingsValidationResult(
            messages.Count == 0,
            wowRetailPathExists,
            combatLogDirectoryExists,
            addonDirectoryExists,
            addonStatus.Installed,
            messages);
    }
}
