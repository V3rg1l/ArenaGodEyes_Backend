namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record CoachKnowledgeParameterItem(
    string Scope,
    string? SpecLabel,
    string Category,
    string Metric,
    string? TargetValue,
    string? Unit,
    string? Note,
    string Source,
    int EvidenceCount,
    DateTimeOffset UpdatedAt);
