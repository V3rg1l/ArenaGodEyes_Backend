using ArenaGodEyes.ApiLocal.Contracts;
using ArenaGodEyes.Core.Application.CombatLog.Abstractions;

namespace ArenaGodEyes.ApiLocal.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/import-log", async (
            ImportLogRequest request,
            ICombatLogImportService combatLogImportService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return Results.BadRequest(new { message = "filePath is required." });
            }

            var result = await combatLogImportService.ImportAsync(request.FilePath, cancellationToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
