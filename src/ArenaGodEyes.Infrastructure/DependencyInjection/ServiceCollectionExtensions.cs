using ArenaGodEyes.Core.Application.CombatLog.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Abstractions.Time;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using ArenaGodEyes.Core.Application.Video.Abstractions;
using ArenaGodEyes.Infrastructure.CombatLog;
using ArenaGodEyes.Infrastructure.FileSystem;
using ArenaGodEyes.Infrastructure.Matches;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Settings;
using ArenaGodEyes.Infrastructure.Settings.Repositories;
using ArenaGodEyes.Infrastructure.Settings.Services;
using ArenaGodEyes.Infrastructure.Time;
using ArenaGodEyes.Infrastructure.Video;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArenaGodEyes.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArenaGodEyesInfrastructure(
        this IServiceCollection services,
        WorkspacePaths workspacePaths)
    {
        services.AddSingleton(workspacePaths);
        services.AddSingleton(new LocalDataPaths(workspacePaths.WorkspaceRootPath));
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IUtcNowProvider, SystemUtcNowProvider>();
        services.AddSingleton<ObsLocalConfigurationReader>();
        services.AddSingleton(new MatchLogWatcherOptions());
        services.AddDbContext<ArenaGodEyesDbContext>((serviceProvider, options) =>
        {
            var localDataPaths = serviceProvider.GetRequiredService<LocalDataPaths>();
            options.UseSqlite($"Data Source={localDataPaths.DatabasePath}");
        });
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IFirstRunBootstrapService, FirstRunBootstrapService>();
        services.AddSingleton<IStorageOverviewService, StorageOverviewService>();
        services.AddSingleton<IWowInstallationDetector, WowInstallationDetector>();
        services.AddSingleton<IAddonStatusService, AddonStatusService>();
        services.AddSingleton<IAddonInstallerService, AddonInstallerService>();
        services.AddSingleton<ISettingsValidationService, SettingsValidationService>();
        services.AddSingleton<CombatLogFileReader>();
        services.AddSingleton<CombatLogTailReader>();
        services.AddSingleton<ArenaLiveMatchAutomationSink>();
        services.AddSingleton<ICombatLogEventSink>(serviceProvider => serviceProvider.GetRequiredService<ArenaLiveMatchAutomationSink>());
        services.AddSingleton<ILiveArenaSessionMonitor>(serviceProvider => serviceProvider.GetRequiredService<ArenaLiveMatchAutomationSink>());
        services.AddSingleton<IMatchLogWatcher, MatchLogWatcher>();
        services.AddSingleton<ICombatLogImportService, CombatLogImportService>();
        services.AddSingleton<WowKnowledgeService>();
        services.AddScoped<MatchAnalysisContextService>();
        services.AddScoped<IMatchImportOrchestrator, MatchImportOrchestrator>();
        services.AddScoped<IMatchLibraryService, MatchLibraryService>();
        services.AddScoped<IManualAnalysisWorkflowService, ManualAnalysisWorkflowService>();
        services.AddScoped<IVideoWorkflowService, VideoWorkflowService>();
        return services;
    }
}
