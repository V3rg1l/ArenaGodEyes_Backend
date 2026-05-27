namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed class MatchLogWatcherState
{
    public string? WatchedDirectory { get; set; }

    public string? ActiveSourceFile { get; set; }

    public DateTimeOffset? ActiveSourceDetectedAt { get; set; }

    public long TotalLinesRead { get; set; }

    public bool IsWatching { get; set; }

    public string? LastError { get; set; }
}
