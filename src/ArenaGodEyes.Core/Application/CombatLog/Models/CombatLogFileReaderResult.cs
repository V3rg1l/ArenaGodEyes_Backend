namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record CombatLogFileReaderResult(
    string SourceFile,
    IReadOnlyList<CombatLogLineEnvelope> Lines);
