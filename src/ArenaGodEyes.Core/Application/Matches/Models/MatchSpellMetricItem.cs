namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchSpellMetricItem(
    string SpellName,
    int CastCount,
    long TotalDamage,
    long TotalHealing);
