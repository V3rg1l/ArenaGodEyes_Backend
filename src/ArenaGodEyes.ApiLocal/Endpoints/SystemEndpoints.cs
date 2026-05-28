using ArenaGodEyes.ApiLocal.Contracts;
using ArenaGodEyes.Core.Application.Abstractions.Time;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Domain.Product;
using ArenaGodEyes.Core.Domain.Safety;

namespace ArenaGodEyes.ApiLocal.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/status", (IUtcNowProvider utcNowProvider) =>
        {
            return Results.Ok(new SystemStatusResponse(
                ProductMetadata.Name,
                ProductMetadata.Version,
                ProductMetadata.Tagline,
                "ready",
                SafetyBoundary.Summary,
                utcNowProvider.UtcNow));
        });

        endpoints.MapGet("/api/system/bootstrap-status", async (
            IFirstRunBootstrapService firstRunBootstrapService,
            CancellationToken cancellationToken) =>
        {
            var status = await firstRunBootstrapService.GetStatusAsync(cancellationToken);
            return Results.Ok(status);
        });

        endpoints.MapGet("/", () => Results.Redirect("/api/system/status"));

        return endpoints;
    }
}
