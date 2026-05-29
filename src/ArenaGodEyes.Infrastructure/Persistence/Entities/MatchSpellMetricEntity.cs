namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class MatchSpellMetricEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int? SpellId { get; set; }

    public string SpellName { get; set; } = string.Empty;

    public string NormalizedSpellName { get; set; } = string.Empty;

    public int CastCount { get; set; }

    public long TotalDamage { get; set; }

    public long TotalHealing { get; set; }

    public string? ClassName { get; set; }

    public string? SpecLabel { get; set; }

    public string? PrimaryCategory { get; set; }

    public string? TacticalPhase { get; set; }

    public bool IsSignatureSpell { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity Match { get; set; } = null!;
}
