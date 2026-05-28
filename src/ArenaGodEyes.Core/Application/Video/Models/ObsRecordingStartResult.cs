namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record ObsRecordingStartResult(
    bool Started,
    bool WasAlreadyRecording,
    string? MatchId,
    string? Message);
