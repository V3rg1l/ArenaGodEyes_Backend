namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchReviewDetails(
    MatchLibraryItem Match,
    string MatchJson,
    string? PromptText,
    string? ManualAnalysisText,
    IReadOnlyList<TimelineMarkerItem> TimelineMarkers);
