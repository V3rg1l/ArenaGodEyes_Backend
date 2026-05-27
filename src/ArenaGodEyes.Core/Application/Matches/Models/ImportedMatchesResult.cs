namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record ImportedMatchesResult(
    string SourceFilePath,
    int SourceLineCount,
    int MatchCount,
    int ParseErrorCount,
    IReadOnlyList<ImportedMatchSummary> Matches);
