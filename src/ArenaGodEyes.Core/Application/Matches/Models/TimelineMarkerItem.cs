namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record TimelineMarkerItem(
    int VideoSecond,
    string Category,
    string Severity,
    string Label,
    string Description,
    string Source);
