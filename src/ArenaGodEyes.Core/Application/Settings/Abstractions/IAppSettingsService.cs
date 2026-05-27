using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IAppSettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task<AppSettings> UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
