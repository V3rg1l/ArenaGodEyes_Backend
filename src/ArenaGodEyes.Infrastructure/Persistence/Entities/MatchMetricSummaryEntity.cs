namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class MatchMetricSummaryEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int TotalCasts { get; set; }

    public long TotalDamage { get; set; }

    public long TotalHealing { get; set; }

    public int InterruptCount { get; set; }

    public int DeathCount { get; set; }

    public double DamagePerSecond { get; set; }

    public double HealingPerSecond { get; set; }

    public double CastsPerMinute { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity Match { get; set; } = null!;
}
