namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record AnalysisInsightItem(
    int? VideoSecond,
    string Category,
    string Severity,
    string Title,
    string Summary,
    string? Evidence,
    string? Recommendation,
    string Source);
