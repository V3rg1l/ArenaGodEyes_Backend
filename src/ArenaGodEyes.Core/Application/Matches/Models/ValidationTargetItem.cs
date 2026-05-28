namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record ValidationTargetItem(
    int? VideoSecond,
    string Category,
    string Metric,
    string? CurrentValue,
    string? ExpectedValue,
    string? Unit,
    string? Note,
    string Source);
