namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record ManualAnalysisImportResult(
    string MatchId,
    string ResponsePath,
    int MarkerCount,
    string StoredProvider);
