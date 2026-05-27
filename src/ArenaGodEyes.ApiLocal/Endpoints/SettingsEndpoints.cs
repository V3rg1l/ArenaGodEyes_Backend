using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;

namespace ArenaGodEyes.ApiLocal.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings", async (
            IAppSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            return Results.Ok(settings);
        });

        endpoints.MapPut("/api/settings", async (
            AppSettings settings,
            IAppSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var updatedSettings = await settingsService.UpdateAsync(settings, cancellationToken);
            return Results.Ok(updatedSettings);
        });

        endpoints.MapPost("/api/settings/detect-wow", async (
            IWowInstallationDetector wowInstallationDetector,
            CancellationToken cancellationToken) =>
        {
            var wowRetailPath = await wowInstallationDetector.DetectWowRetailPathAsync(cancellationToken);
            return Results.Ok(new { wowRetailPath });
        });

        endpoints.MapPost("/api/settings/validate", async (
            AppSettings settings,
            ISettingsValidationService validationService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validationService.ValidateAsync(settings, cancellationToken);
            return Results.Ok(validationResult);
        });

        endpoints.MapPost("/api/settings/install-addon", async (
            AppSettings settings,
            IAddonInstallerService addonInstallerService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(settings.WowRetailPath))
            {
                return Results.BadRequest(new { message = "WoW retail path is required." });
            }

            var status = await addonInstallerService.InstallAsync(settings.WowRetailPath, cancellationToken);
            return Results.Ok(status);
        });

        endpoints.MapGet("/api/settings/addon-status", async (
            IAppSettingsService settingsService,
            IAddonStatusService addonStatusService,
            CancellationToken cancellationToken) =>
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            var status = await addonStatusService.GetStatusAsync(settings.AddonDirectory, cancellationToken);
            return Results.Ok(status);
        });

        return endpoints;
    }
}
