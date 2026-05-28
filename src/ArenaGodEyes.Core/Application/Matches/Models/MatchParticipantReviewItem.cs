namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchParticipantReviewItem(
    string Guid,
    string Name,
    string? Realm,
    string? Region,
    int TeamId,
    string? ClassName,
    string? SpecLabel,
    int PersonalRating,
    int HighestPvpTier,
    bool IsTrackedPlayer,
    SpecPerformanceSnapshotItem? ProfileSnapshot,
    IReadOnlyList<CoachKnowledgeParameterItem> CoachKnowledgeParameters,
    IReadOnlyList<CoachSkillItem> CoachSkills);
