namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchParticipantSummaryItem(
    string Name,
    int TeamId,
    string? ClassName,
    string? SpecLabel,
    bool IsTrackedPlayer);
