namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record CoachSkillItem(
    string Scope,
    string? SpecLabel,
    string Area,
    string Goal,
    string? Drill,
    string Source,
    int EvidenceCount,
    DateTimeOffset UpdatedAt);
