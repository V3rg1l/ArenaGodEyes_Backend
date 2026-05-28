namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record RuleCoachFindingItem(
    string Title,
    string Category,
    string Severity,
    string Scope,
    string Summary,
    string Recommendation,
    string Evidence,
    string? RelatedMetric);
