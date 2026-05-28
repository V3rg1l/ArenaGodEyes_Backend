using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IStorageOverviewService
{
    Task<StorageOverview> GetAsync(CancellationToken cancellationToken = default);
}
