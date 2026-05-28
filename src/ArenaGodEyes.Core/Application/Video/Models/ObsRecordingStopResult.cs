namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record ObsRecordingStopResult(
    bool Stopped,
    string? MatchId,
    string? OutputPath,
    string? FinalVideoPath,
    bool AttachedToMatch,
    string? Message);
