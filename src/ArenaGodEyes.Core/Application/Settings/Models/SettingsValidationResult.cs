namespace ArenaGodEyes.Core.Application.Settings.Models;

public sealed record SettingsValidationResult(
    bool IsValid,
    bool WowRetailPathExists,
    bool CombatLogDirectoryExists,
    bool AddonDirectoryExists,
    bool AddonInstalled,
    IReadOnlyList<string> Messages);
