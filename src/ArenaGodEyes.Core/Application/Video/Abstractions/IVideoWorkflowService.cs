using ArenaGodEyes.Core.Application.Video.Models;

namespace ArenaGodEyes.Core.Application.Video.Abstractions;

public interface IVideoWorkflowService
{
    Task<ObsConnectionStatus> GetObsStatusAsync(CancellationToken cancellationToken = default);

    Task<ObsConnectionStatus> TestObsConnectionAsync(CancellationToken cancellationToken = default);

    Task<ObsSceneSetupResult> EnsureWowSceneAsync(
        string windowTitle,
        string executableName,
        string? windowClassName,
        string captureMode,
        bool captureCursor,
        string? sceneName,
        string? sourceName,
        CancellationToken cancellationToken = default);

    Task<ObsRecordingStartResult> StartRecordingAsync(string? matchId, CancellationToken cancellationToken = default);

    Task<ObsRecordingStopResult> StopRecordingAsync(string? matchId, CancellationToken cancellationToken = default);

    Task<VideoProcessingResult?> ProcessMatchVideoAsync(string matchId, CancellationToken cancellationToken = default);

    Task<VideoClipGenerationResult?> GenerateReviewClipsAsync(string matchId, CancellationToken cancellationToken = default);
}
