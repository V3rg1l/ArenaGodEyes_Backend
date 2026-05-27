using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Core.Application.Abstractions.Time;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly IAppSettingsRepository _repository;
    private readonly IUtcNowProvider _utcNowProvider;

    public AppSettingsService(IAppSettingsRepository repository, IUtcNowProvider utcNowProvider)
    {
        _repository = repository;
        _utcNowProvider = utcNowProvider;
    }

    public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAsync(cancellationToken);

    public async Task<AppSettings> UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var existingSettings = await _repository.GetAsync(cancellationToken);

        settings.Id = existingSettings.Id;
        settings.CreatedAt = existingSettings.CreatedAt == default
            ? _utcNowProvider.UtcNow
            : existingSettings.CreatedAt;
        settings.UpdatedAt = _utcNowProvider.UtcNow;

        if (!string.IsNullOrWhiteSpace(settings.WowRetailPath))
        {
            settings.CombatLogDirectory ??= Path.Combine(settings.WowRetailPath, "Logs");
            settings.AddonDirectory ??= Path.Combine(settings.WowRetailPath, "Interface", "AddOns", "ArenaGodEyes");
        }

        return await _repository.SaveAsync(settings, cancellationToken);
    }
}
