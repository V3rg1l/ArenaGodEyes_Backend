namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record MatchReviewDetails(
    MatchLibraryItem Match,
    string MatchJson,
    string? PromptText,
    string? ManualAnalysisText,
    SpecPerformanceSnapshotItem? SpecPerformanceSnapshot,
    MatchMetricSummaryItem? MetricSummary,
    IReadOnlyList<MatchSpellMetricItem> SpellMetrics,
    IReadOnlyList<CoachKnowledgeParameterItem> CoachKnowledgeParameters,
    IReadOnlyList<CoachSkillItem> CoachSkills,
    IReadOnlyList<MatchBenchmarkComparisonItem> BenchmarkComparisons,
    IReadOnlyList<RuleCoachFindingItem> RuleCoachFindings,
    IReadOnlyList<TimelineMarkerItem> TimelineMarkers,
    IReadOnlyList<AnalysisInsightItem> Insights,
    IReadOnlyList<ValidationTargetItem> ValidationTargets,
    IReadOnlyList<VideoClipItem> VideoClips);
