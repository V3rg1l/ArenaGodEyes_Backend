namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed record NewCombatLogFileDetected(
    string Path,
    DateTimeOffset DetectedAt);
