using ArenaGodEyes.Core.Application.CombatLog.Models;

namespace ArenaGodEyes.Core.Application.CombatLog.Abstractions;

public interface IMatchLogWatcher
{
    MatchLogWatcherState State { get; }

    Task RunAsync(CancellationToken cancellationToken = default);
}
