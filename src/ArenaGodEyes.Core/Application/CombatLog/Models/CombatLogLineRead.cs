namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record CombatLogLineRead(
    string SourceFile,
    long LineNumber,
    string RawLine);
