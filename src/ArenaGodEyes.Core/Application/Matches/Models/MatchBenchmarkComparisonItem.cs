namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchBenchmarkComparisonItem(
    string Scope,
    string? ClassName,
    string? SpecLabel,
    string Category,
    string Metric,
    string? CurrentValue,
    string? ExpectedValue,
    string? Unit,
    string Status,
    string? Note,
    string Source,
    int EvidenceCount);
