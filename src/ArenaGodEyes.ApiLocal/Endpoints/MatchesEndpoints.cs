using ArenaGodEyes.ApiLocal.Contracts;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Infrastructure.Settings;

namespace ArenaGodEyes.ApiLocal.Endpoints;

public static class MatchesEndpoints
{
    public static IEndpointRouteBuilder MapMatchesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/matches", async (
            IMatchLibraryService matchLibraryService,
            CancellationToken cancellationToken) =>
        {
            var matches = await matchLibraryService.ListAsync(cancellationToken);
            return Results.Ok(matches);
        });

        endpoints.MapGet("/api/matches/{matchId}", async (
            string matchId,
            IMatchLibraryService matchLibraryService,
            CancellationToken cancellationToken) =>
        {
            var match = await matchLibraryService.GetAsync(matchId, cancellationToken);
            return match is null ? Results.NotFound() : Results.Ok(match);
        });

        endpoints.MapPost("/api/matches/import-log", async (
            ImportLogRequest request,
            IMatchImportOrchestrator matchImportOrchestrator,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                return Results.BadRequest(new { message = "filePath is required." });
            }

            var result = await matchImportOrchestrator.ImportAsync(request.FilePath, cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/matches/import-sample", async (
            WorkspacePaths workspacePaths,
            IMatchImportOrchestrator matchImportOrchestrator,
            CancellationToken cancellationToken) =>
        {
            var sampleFile = Path.Combine(
                workspacePaths.WorkspaceRootPath,
                "ArenaGodEyes.Docs",
                "src",
                "logBase",
                "ArenaCoach-Logs-2026-05-27T18-58-55-893Z-1779899588000_980",
                "chunks",
                "1779899588000_980.txt");

            var result = await matchImportOrchestrator.ImportAsync(sampleFile, cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/matches/{matchId}/attach-video", async (
            string matchId,
            AttachVideoRequest request,
            IMatchLibraryService matchLibraryService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.VideoPath))
            {
                return Results.BadRequest(new { message = "videoPath is required." });
            }

            var updated = await matchLibraryService.AttachVideoAsync(matchId, request.VideoPath, cancellationToken);
            return updated ? Results.Ok() : Results.NotFound();
        });

        endpoints.MapPost("/api/matches/{matchId}/export-chatgpt-prompt", async (
            string matchId,
            IManualAnalysisWorkflowService manualAnalysisWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var export = await manualAnalysisWorkflowService.ExportPromptAsync(matchId, cancellationToken);
            return export is null ? Results.NotFound() : Results.Ok(export);
        });

        endpoints.MapPost("/api/matches/{matchId}/manual-analysis", async (
            string matchId,
            ManualAnalysisImportRequest request,
            IManualAnalysisWorkflowService manualAnalysisWorkflowService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.ResponseText))
            {
                return Results.BadRequest(new { message = "responseText is required." });
            }

            var result = await manualAnalysisWorkflowService.ImportResponseAsync(
                matchId,
                request.ResponseText,
                cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return endpoints;
    }
}
