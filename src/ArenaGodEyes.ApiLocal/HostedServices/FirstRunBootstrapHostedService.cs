using ArenaGodEyes.Core.Application.Settings.Abstractions;

namespace ArenaGodEyes.ApiLocal.HostedServices;

public sealed class FirstRunBootstrapHostedService : IHostedService
{
    private readonly IFirstRunBootstrapService _firstRunBootstrapService;
    private readonly ILogger<FirstRunBootstrapHostedService> _logger;

    public FirstRunBootstrapHostedService(
        IFirstRunBootstrapService firstRunBootstrapService,
        ILogger<FirstRunBootstrapHostedService> logger)
    {
        _firstRunBootstrapService = firstRunBootstrapService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var status = await _firstRunBootstrapService.RunAsync(cancellationToken);
        _logger.LogInformation(
            "First-run bootstrap completed. Fixes: {Fixes}. Pending: {Pending}.",
            string.Join(",", status.AppliedFixes),
            string.Join(",", status.PendingActions));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
