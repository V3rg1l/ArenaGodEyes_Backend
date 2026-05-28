namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchMetricSummaryItem(
    int TotalCasts,
    long TotalDamage,
    long TotalHealing,
    int InterruptCount,
    int DeathCount,
    double DamagePerSecond,
    double HealingPerSecond,
    double CastsPerMinute);
