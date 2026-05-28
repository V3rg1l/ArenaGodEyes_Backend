namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchSpellMetricItem(
    string SpellName,
    string NormalizedSpellName,
    int CastCount,
    long TotalDamage,
    long TotalHealing,
    string? ClassName,
    string? SpecLabel,
    string? PrimaryCategory,
    string? TacticalPhase,
    bool IsSignatureSpell);
