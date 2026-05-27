using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IAddonInstallerService
{
    Task<AddonStatus> InstallAsync(string wowRetailPath, CancellationToken cancellationToken = default);
}
