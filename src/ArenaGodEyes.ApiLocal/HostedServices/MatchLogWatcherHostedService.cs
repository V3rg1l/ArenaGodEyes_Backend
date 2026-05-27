using ArenaGodEyes.Core.Application.CombatLog.Abstractions;

namespace ArenaGodEyes.ApiLocal.HostedServices;

public sealed class MatchLogWatcherHostedService : BackgroundService
{
    private readonly IMatchLogWatcher _matchLogWatcher;

    public MatchLogWatcherHostedService(IMatchLogWatcher matchLogWatcher)
    {
        _matchLogWatcher = matchLogWatcher;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _matchLogWatcher.RunAsync(stoppingToken);
}
