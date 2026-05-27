using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IAddonStatusService
{
    Task<AddonStatus> GetStatusAsync(string? addonDirectory, CancellationToken cancellationToken = default);
}
