using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ArenaGodEyes.Infrastructure.Persistence;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly LocalDataPaths _localDataPaths;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DatabaseInitializerHostedService(
        LocalDataPaths localDataPaths,
        IServiceScopeFactory serviceScopeFactory)
    {
        _localDataPaths = localDataPaths;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var path in _localDataPaths.AllDirectories)
        {
            Directory.CreateDirectory(path);
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArenaGodEyesDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await DatabaseSchemaUpgrader.EnsureLatestAsync(_localDataPaths.DatabasePath, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
