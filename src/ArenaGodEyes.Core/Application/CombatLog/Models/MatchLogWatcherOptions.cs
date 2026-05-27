namespace ArenaGodEyes.Core.Application.CombatLog.Models;

public sealed class MatchLogWatcherOptions
{
    public TimeSpan IdlePollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public bool ResumeFromEndOnStartup { get; init; } = true;

    public string[] FilePatterns { get; init; } =
    [
        "WoWCombatLog.txt",
        "WoWCombatLog-*.txt"
    ];
}
