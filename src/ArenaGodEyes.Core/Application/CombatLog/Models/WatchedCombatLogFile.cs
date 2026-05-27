namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record WatchedCombatLogFile(
    string Path,
    DateTime LastWriteTimeUtc,
    long Length);
