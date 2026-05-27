namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record CombatLogImportResult(
    string SourceFile,
    int LineCount,
    string? FirstLine,
    string? LastLine);
