namespace ArenaGodEyes.ApiLocal.Contracts;

public sealed record LiveArenaSessionStatusResponse(
    bool IsActive,
    string? Bracket,
    bool IsRanked,
    bool ShouldTrack,
    string? SourceFile,
    DateTimeOffset? StartedAt,
    bool StartedRecordingAutomatically,
    string? LastCompletedMatchId,
    DateTimeOffset? LastCompletedAt);
