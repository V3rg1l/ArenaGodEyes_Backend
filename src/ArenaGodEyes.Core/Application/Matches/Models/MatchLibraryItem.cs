namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchLibraryItem(
    string MatchId,
    DateTimeOffset StartedAt,
    string Bracket,
    string MapName,
    int DurationSeconds,
    string? ResultForPlayer,
    string? PlayerName,
    string? PlayerSpecLabel,
    bool HasVideo,
    bool HasManualAnalysis,
    int TimelineMarkerCount,
    string MatchJsonPath,
    string? VideoLocalPath);
