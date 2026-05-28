namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record LiveArenaSessionStatus(
    bool IsActive,
    string? Bracket,
    bool IsRanked,
    bool ShouldTrack,
    string? SourceFile,
    DateTimeOffset? StartedAt,
    bool StartedRecordingAutomatically,
    string? LastCompletedMatchId,
    DateTimeOffset? LastCompletedAt);
