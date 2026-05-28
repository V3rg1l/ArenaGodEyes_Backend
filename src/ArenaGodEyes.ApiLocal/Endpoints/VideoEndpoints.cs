using ArenaGodEyes.ApiLocal.Contracts;
using ArenaGodEyes.Core.Application.Video.Abstractions;

namespace ArenaGodEyes.ApiLocal.Endpoints;

public static class VideoEndpoints
{
    public static IEndpointRouteBuilder MapVideoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/video/obs/status", async (
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.GetObsStatusAsync(cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/video/obs/test-connection", async (
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.TestObsConnectionAsync(cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/video/obs/ensure-wow-scene", async (
            ObsEnsureWowSceneRequest request,
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.EnsureWowSceneAsync(
                request.WindowTitle,
                request.ExecutableName,
                request.WindowClassName,
                request.CaptureMode,
                request.CaptureCursor,
                request.SceneName,
                request.SourceName,
                cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/video/obs/start-recording", async (
            ObsStartRecordingRequest request,
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.StartRecordingAsync(request.MatchId, cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/video/obs/stop-recording", async (
            ObsStopRecordingRequest request,
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.StopRecordingAsync(request.MatchId, cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/api/matches/{matchId}/process-video", async (
            string matchId,
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.ProcessMatchVideoAsync(matchId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        endpoints.MapPost("/api/matches/{matchId}/generate-review-clips", async (
            string matchId,
            IVideoWorkflowService videoWorkflowService,
            CancellationToken cancellationToken) =>
        {
            var result = await videoWorkflowService.GenerateReviewClipsAsync(matchId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return endpoints;
    }
}
