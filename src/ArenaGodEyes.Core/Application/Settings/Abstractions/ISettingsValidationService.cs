using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface ISettingsValidationService
{
    Task<SettingsValidationResult> ValidateAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
