namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record ImportedMatchSummary(
    string MatchId,
    string Bracket,
    int MapId,
    string MapName,
    int DurationSeconds,
    string ChunkFilePath,
    string MatchJsonPath);
