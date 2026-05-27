namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record CombatLogLineEnvelope(
    string SourceFile,
    long LineNumber,
    long ByteOffset,
    string RawLine);
