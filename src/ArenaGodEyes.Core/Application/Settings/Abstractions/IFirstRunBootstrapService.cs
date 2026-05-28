using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IFirstRunBootstrapService
{
    Task<FirstRunBootstrapStatus> RunAsync(CancellationToken cancellationToken = default);

    Task<FirstRunBootstrapStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
