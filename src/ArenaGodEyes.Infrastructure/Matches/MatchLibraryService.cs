using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Core.Application.Video.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json.Nodes;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class MatchLibraryService : IMatchLibraryService
{
    private readonly ArenaGodEyesDbContext _dbContext;
    private readonly MatchAnalysisContextService _matchAnalysisContextService;

    public MatchLibraryService(ArenaGodEyesDbContext dbContext, MatchAnalysisContextService matchAnalysisContextService)
    {
        _dbContext = dbContext;
        _matchAnalysisContextService = matchAnalysisContextService;
    }

    public async Task<bool> AttachVideoAsync(string matchId, string videoPath, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        match.VideoLocalPath = videoPath;
        match.RecordingStatus ??= "attached";
        match.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateVideoProcessingAsync(
        string matchId,
        VideoProcessingResult result,
        CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        match.VideoLocalPath = result.VideoPath;
        match.ThumbnailPath = result.ThumbnailPath;
        match.VideoDurationSeconds = result.DurationSeconds;
        match.VideoFileSizeBytes = result.FileSizeBytes;
        match.VideoFramesPerSecond = result.FramesPerSecond;
        match.VideoCodec = result.Codec;
        match.VideoResolution = result.Resolution;
        match.LastVideoProcessedAt = result.ProcessedAt;
        match.RecordingStatus = string.IsNullOrWhiteSpace(match.RecordingStatus) ? "processed" : match.RecordingStatus;
        match.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MatchReviewDetails?> GetAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .Include(item => item.ManualAnalyses)
            .Include(item => item.AnalysisInsights)
            .Include(item => item.ValidationTargets)
            .Include(item => item.VideoClips)
            .Include(item => item.MetricSummary)
            .Include(item => item.SpellMetrics)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var matchJson = await File.ReadAllTextAsync(match.MatchJsonPath, cancellationToken);
        var promptText = match.ManualAnalyses
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.PromptText)
            .FirstOrDefault();
        var manualAnalysisText = match.ManualAnalyses
            .OrderByDescending(item => item.ImportedAt)
            .Select(item => item.ResponseText)
            .FirstOrDefault();

        var knowledgeParameters = (await _dbContext.CoachKnowledgeParameters
            .Where(item => item.Scope == "global" ||
                           (!string.IsNullOrWhiteSpace(match.PlayerClassName) &&
                            item.Scope == "class" &&
                            item.ClassName == match.PlayerClassName) ||
                           (!string.IsNullOrWhiteSpace(match.PlayerSpecLabel) &&
                            item.Scope == "spec" &&
                            item.SpecLabel == match.PlayerSpecLabel))
            .OrderByDescending(item => item.EvidenceCount)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.EvidenceCount)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(24)
            .ToList();
        var coachSkills = (await _dbContext.CoachSkills
            .Where(item => item.Scope == "global" ||
                           (!string.IsNullOrWhiteSpace(match.PlayerClassName) &&
                            item.Scope == "class" &&
                            item.ClassName == match.PlayerClassName) ||
                           (!string.IsNullOrWhiteSpace(match.PlayerSpecLabel) &&
                            item.Scope == "spec" &&
                            item.SpecLabel == match.PlayerSpecLabel))
            .OrderByDescending(item => item.EvidenceCount)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.EvidenceCount)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(24)
            .ToList();
        var participants = await _matchAnalysisContextService.BuildParticipantReviewItemsAsync(match, matchJson, cancellationToken);
        var specPerformanceSnapshot = BuildSpecPerformanceSnapshot(match);
        var benchmarkComparisons = BuildBenchmarkComparisons(match, knowledgeParameters, specPerformanceSnapshot);
        var ruleCoachFindings = BuildRuleCoachFindings(match, benchmarkComparisons, coachSkills, specPerformanceSnapshot);

        var details = new MatchReviewDetails(
            ToLibraryItem(
                match,
                participants
                    .Select(item => new MatchParticipantSummaryItem(
                        item.Name,
                        item.TeamId,
                        item.ClassName,
                        item.SpecLabel,
                        item.IsTrackedPlayer))
                    .ToList()),
            matchJson,
            promptText,
            manualAnalysisText,
            specPerformanceSnapshot,
            match.MetricSummary is null
                ? null
                : new MatchMetricSummaryItem(
                    match.MetricSummary.TotalCasts,
                    match.MetricSummary.TotalDamage,
                    match.MetricSummary.TotalHealing,
                    match.MetricSummary.InterruptCount,
                    match.MetricSummary.DeathCount,
                    match.MetricSummary.DamagePerSecond,
                    match.MetricSummary.HealingPerSecond,
                    match.MetricSummary.CastsPerMinute),
            match.SpellMetrics
                .OrderByDescending(metric => metric.TotalDamage)
                .ThenByDescending(metric => metric.CastCount)
                .ThenBy(metric => metric.SpellName)
                .Select(metric => new MatchSpellMetricItem(
                    metric.SpellId,
                    metric.SpellName,
                    metric.NormalizedSpellName,
                    metric.CastCount,
                    metric.TotalDamage,
                    metric.TotalHealing,
                    metric.ClassName,
                    metric.SpecLabel,
                    metric.PrimaryCategory,
                    metric.TacticalPhase,
                    metric.IsSignatureSpell))
                .ToList(),
            knowledgeParameters
                .Select(item => new CoachKnowledgeParameterItem(
                    item.Scope,
                    item.ClassName,
                    item.SpecLabel,
                    item.Category,
                    item.Metric,
                    item.TargetValue,
                    item.Unit,
                    item.Note,
                    item.Source,
                    item.EvidenceCount,
                    item.UpdatedAt))
                .ToList(),
            coachSkills
                .Select(item => new CoachSkillItem(
                    item.Scope,
                    item.ClassName,
                    item.SpecLabel,
                    item.Area,
                    item.Goal,
                    item.Drill,
                    item.Source,
                    item.EvidenceCount,
                    item.UpdatedAt))
                .ToList(),
            benchmarkComparisons,
            ruleCoachFindings,
            match.TimelineMarkers
                .OrderBy(marker => marker.VideoSecond)
                .Select(marker => new TimelineMarkerItem(
                    marker.VideoSecond,
                    marker.Category,
                    marker.Severity,
                    marker.Label,
                    marker.Description,
                    marker.Source))
                .ToList(),
            match.AnalysisInsights
                .OrderBy(insight => insight.VideoSecond)
                .ThenBy(insight => insight.Id)
                .Select(insight => new AnalysisInsightItem(
                    insight.VideoSecond,
                    insight.Category,
                    insight.Severity,
                    insight.Title,
                    insight.Summary,
                    insight.Evidence,
                    insight.Recommendation,
                    insight.Source))
                .ToList(),
            match.ValidationTargets
                .OrderBy(target => target.VideoSecond)
                .ThenBy(target => target.Id)
                .Select(target => new ValidationTargetItem(
                    target.VideoSecond,
                    target.Category,
                    target.Metric,
                    target.CurrentValue,
                    target.ExpectedValue,
                    target.Unit,
                    target.Note,
                    target.Source))
                .ToList(),
            match.VideoClips
                .OrderBy(clip => clip.VideoSecond)
                .ThenBy(clip => clip.Id)
                .Select(clip => new VideoClipItem(
                    clip.VideoSecond,
                    clip.StartSecond,
                    clip.EndSecond,
                    clip.Label,
                    clip.Category,
                    clip.Source,
                    clip.ClipPath,
                    clip.CreatedAt))
                .ToList(),
            participants);

        return details;
    }

    public async Task<IReadOnlyList<MatchLibraryItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var matches = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .ToListAsync(cancellationToken);

        var participantMap = await BuildParticipantSummaryMapAsync(matches, cancellationToken);

        return matches
            .OrderByDescending(item => item.StartedAt.UtcDateTime)
            .Select(item => ToLibraryItem(
                item,
                participantMap.TryGetValue(item.MatchId, out var participants)
                    ? participants
                    : []))
            .ToList();
    }

    public async Task ReplaceVideoClipsAsync(
        string matchId,
        IReadOnlyList<GeneratedVideoClip> clips,
        CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .Include(item => item.VideoClips)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return;
        }

        _dbContext.VideoClips.RemoveRange(match.VideoClips);

        match.VideoClips = clips
            .Select(clip => new VideoClipEntity
            {
                MatchId = matchId,
                VideoSecond = clip.VideoSecond,
                StartSecond = clip.StartSecond,
                EndSecond = clip.EndSecond,
                Label = clip.Label,
                Category = clip.Category,
                Source = clip.Source,
                ClipPath = clip.ClipPath,
                CreatedAt = clip.CreatedAt
            })
            .ToList();

        match.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MatchLibraryItem ToLibraryItem(
        MatchRecordEntity match,
        IReadOnlyList<MatchParticipantSummaryItem> participants) =>
        new(
            match.MatchId,
            match.StartedAt,
            match.Bracket,
            match.MapName,
            match.DurationSeconds,
            match.ResultForPlayer,
            match.PlayerName,
            match.PlayerClassName,
            match.PlayerSpecLabel,
            !string.IsNullOrWhiteSpace(match.VideoLocalPath),
            match.HasManualAnalysis,
            match.TimelineMarkers.Count,
            match.MatchJsonPath,
            match.VideoLocalPath,
            match.ThumbnailPath,
            match.RecordingStatus,
            match.VideoDurationSeconds,
            match.VideoResolution,
            participants);

    private async Task<Dictionary<string, IReadOnlyList<MatchParticipantSummaryItem>>> BuildParticipantSummaryMapAsync(
        IReadOnlyList<MatchRecordEntity> matches,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, IReadOnlyList<MatchParticipantSummaryItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in matches)
        {
            if (string.IsNullOrWhiteSpace(match.MatchJsonPath) || !File.Exists(match.MatchJsonPath))
            {
                map[match.MatchId] = [];
                continue;
            }

            var matchJson = await File.ReadAllTextAsync(match.MatchJsonPath, cancellationToken);
            map[match.MatchId] = ParseParticipantSummaries(matchJson, match);
        }

        return map;
    }

    private static IReadOnlyList<MatchParticipantSummaryItem> ParseParticipantSummaries(
        string matchJson,
        MatchRecordEntity match)
    {
        var players = JsonNode.Parse(matchJson)?["players"]?.AsArray();
        if (players is null)
        {
            return [];
        }

        return players
            .OfType<JsonObject>()
            .Select(player => new MatchParticipantSummaryItem(
                player["name"]?.GetValue<string>() ?? "Unknown",
                player["teamId"]?.GetValue<int?>() ?? -1,
                NormalizeUnknown(player["className"]?.GetValue<string>()),
                NormalizeUnknown(player["specLabel"]?.GetValue<string>()),
                string.Equals(player["guid"]?.GetValue<string>(), match.PlayerGuid, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.TeamId)
            .ThenByDescending(item => item.IsTrackedPlayer)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "Unknown Spec", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;

    private static SpecPerformanceSnapshotItem? BuildSpecPerformanceSnapshot(MatchRecordEntity match)
    {
        if (match.SpellMetrics.Count == 0)
        {
            return null;
        }

        var recognized = match.SpellMetrics.Where(metric => !string.IsNullOrWhiteSpace(metric.ClassName)).ToList();
        if (recognized.Count == 0)
        {
            return null;
        }

        return new SpecPerformanceSnapshotItem(
            match.PlayerClassName,
            match.PlayerSpecLabel,
            recognized.Count,
            recognized.Where(metric => metric.TacticalPhase == "core").Sum(metric => metric.CastCount),
            recognized.Where(metric => metric.TacticalPhase is "burst" or "cooldowns").Sum(metric => metric.CastCount),
            recognized.Where(metric => metric.TacticalPhase == "defensives" || metric.PrimaryCategory == "defensive").Sum(metric => metric.CastCount),
            recognized.Where(metric => metric.TacticalPhase is "cc" or "utility" ||
                                       metric.PrimaryCategory is "stun" or "fear" or "silence" or "disorient" or "incapacitate" or "horror" or "root" or "cc")
                .Sum(metric => metric.CastCount),
            recognized.Where(metric => metric.PrimaryCategory == "interrupt" || metric.TacticalPhase == "interrupts").Sum(metric => metric.CastCount),
            recognized.Where(metric => metric.PrimaryCategory == "mobility" || metric.TacticalPhase == "mobility").Sum(metric => metric.CastCount));
    }

    private static List<MatchBenchmarkComparisonItem> BuildBenchmarkComparisons(
        MatchRecordEntity match,
        IReadOnlyList<CoachKnowledgeParameterEntity> coachKnowledgeParameters,
        SpecPerformanceSnapshotItem? specPerformanceSnapshot)
    {
        var observedValues = BuildObservedValues(match, specPerformanceSnapshot);
        var comparisons = new List<MatchBenchmarkComparisonItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in match.ValidationTargets
                     .OrderByDescending(item => item.VideoSecond.HasValue)
                     .ThenBy(item => item.VideoSecond)
                     .ThenBy(item => item.Metric))
        {
            var key = BuildComparisonKey("match", target.Category, target.Metric);
            if (!seen.Add(key))
            {
                continue;
            }

            comparisons.Add(new MatchBenchmarkComparisonItem(
                "match",
                match.PlayerClassName,
                match.PlayerSpecLabel,
                target.Category,
                target.Metric,
                target.CurrentValue,
                target.ExpectedValue,
                target.Unit,
                EvaluateStatus(target.Metric, target.CurrentValue, target.ExpectedValue),
                target.Note,
                target.Source,
                1));
        }

        foreach (var parameter in coachKnowledgeParameters)
        {
            var key = BuildComparisonKey(parameter.Scope, parameter.Category, parameter.Metric);
            if (!seen.Add(key))
            {
                continue;
            }

            observedValues.TryGetValue(NormalizeMetricKey(parameter.Metric), out var observedValue);
            comparisons.Add(new MatchBenchmarkComparisonItem(
                parameter.Scope,
                parameter.ClassName,
                parameter.SpecLabel,
                parameter.Category,
                parameter.Metric,
                observedValue?.Value,
                parameter.TargetValue,
                parameter.Unit ?? observedValue?.Unit,
                EvaluateStatus(parameter.Metric, observedValue?.Value, parameter.TargetValue),
                parameter.Note,
                parameter.Source,
                parameter.EvidenceCount));
        }

        return comparisons
            .OrderBy(item => StatusRank(item.Status))
            .ThenByDescending(item => item.EvidenceCount)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Metric)
            .Take(18)
            .ToList();
    }

    private static List<RuleCoachFindingItem> BuildRuleCoachFindings(
        MatchRecordEntity match,
        IReadOnlyList<MatchBenchmarkComparisonItem> benchmarkComparisons,
        IReadOnlyList<CoachSkillEntity> coachSkills,
        SpecPerformanceSnapshotItem? specPerformanceSnapshot)
    {
        var findings = new List<RuleCoachFindingItem>();

        foreach (var comparison in benchmarkComparisons
                     .Where(item => item.Status is "below_target" or "above_target" or "needs_review")
                     .Take(8))
        {
            var matchingSkill = FindMatchingCoachSkill(comparison, coachSkills);
            var recommendation = matchingSkill?.Goal
                ?? comparison.Note
                ?? comparison.ExpectedValue
                ?? "Review this sequence and tighten the repeatable response.";
            var evidence = BuildEvidenceText(comparison, matchingSkill);

            findings.Add(new RuleCoachFindingItem(
                $"{Titleize(comparison.Metric)} audit",
                comparison.Category,
                comparison.Status switch
                {
                    "below_target" => "high",
                    "above_target" => MetricPrefersLowerValues(comparison.Metric) ? "high" : "medium",
                    _ => "medium"
                },
                BuildScopeLabel(comparison.Scope, comparison.ClassName, comparison.SpecLabel),
                BuildSummaryText(comparison),
                recommendation,
                evidence,
                comparison.Metric));
        }

        if (specPerformanceSnapshot is not null)
        {
            AddSnapshotFinding(
                findings,
                specPerformanceSnapshot.DefensiveSpellUsageCount == 0,
                "Defensive coverage was not recognized in this review window.",
                "defensive",
                "high",
                match.PlayerClassName,
                match.PlayerSpecLabel,
                "defensive_spell_usage_count",
                coachSkills);
            AddSnapshotFinding(
                findings,
                specPerformanceSnapshot.InterruptSpellUsageCount == 0,
                "No interrupt usage was recognized for the inferred profile.",
                "interrupt",
                "medium",
                match.PlayerClassName,
                match.PlayerSpecLabel,
                "interrupt_spell_usage_count",
                coachSkills);
            AddSnapshotFinding(
                findings,
                specPerformanceSnapshot.ControlSpellUsageCount == 0,
                "No control pattern was recognized in the inferred spell profile.",
                "cc",
                "medium",
                match.PlayerClassName,
                match.PlayerSpecLabel,
                "control_spell_usage_count",
                coachSkills);
        }

        if (findings.Count == 0)
        {
            foreach (var skill in coachSkills.Take(3))
            {
                findings.Add(new RuleCoachFindingItem(
                    $"{Titleize(skill.Area)} drill",
                    "coach_memory",
                    "low",
                    BuildScopeLabel(skill.Scope, skill.ClassName, skill.SpecLabel),
                    $"Coach memory already has reusable guidance for {skill.Area}.",
                    skill.Goal,
                    $"Evidence count {skill.EvidenceCount}. {(string.IsNullOrWhiteSpace(skill.Drill) ? "No drill text saved yet." : skill.Drill)}",
                    skill.Area));
            }
        }

        return findings
            .GroupBy(item => new { item.Title, item.Scope, item.RelatedMetric })
            .Select(group => group.First())
            .Take(8)
            .ToList();
    }

    private static Dictionary<string, ObservedMetricValue> BuildObservedValues(
        MatchRecordEntity match,
        SpecPerformanceSnapshotItem? specPerformanceSnapshot)
    {
        var values = new Dictionary<string, ObservedMetricValue>(StringComparer.OrdinalIgnoreCase);

        if (match.MetricSummary is not null)
        {
            AddObservedValue(values, "total_casts", match.MetricSummary.TotalCasts, null);
            AddObservedValue(values, "damage_per_second", match.MetricSummary.DamagePerSecond, "dps");
            AddObservedValue(values, "healing_per_second", match.MetricSummary.HealingPerSecond, "hps");
            AddObservedValue(values, "casts_per_minute", match.MetricSummary.CastsPerMinute, "cpm");
            AddObservedValue(values, "interrupt_count", match.MetricSummary.InterruptCount, null);
            AddObservedValue(values, "death_count", match.MetricSummary.DeathCount, null);
        }

        if (specPerformanceSnapshot is not null)
        {
            AddObservedValue(values, "recognized_spell_count", specPerformanceSnapshot.RecognizedSpellCount, null);
            AddObservedValue(values, "core_spell_usage_count", specPerformanceSnapshot.CoreSpellUsageCount, null);
            AddObservedValue(values, "burst_spell_usage_count", specPerformanceSnapshot.BurstSpellUsageCount, null);
            AddObservedValue(values, "defensive_spell_usage_count", specPerformanceSnapshot.DefensiveSpellUsageCount, null);
            AddObservedValue(values, "control_spell_usage_count", specPerformanceSnapshot.ControlSpellUsageCount, null);
            AddObservedValue(values, "interrupt_spell_usage_count", specPerformanceSnapshot.InterruptSpellUsageCount, null);
            AddObservedValue(values, "mobility_spell_usage_count", specPerformanceSnapshot.MobilitySpellUsageCount, null);
        }

        return values;
    }

    private static void AddObservedValue(
        IDictionary<string, ObservedMetricValue> values,
        string metric,
        double numericValue,
        string? unit)
    {
        values[NormalizeMetricKey(metric)] = new ObservedMetricValue(
            numericValue.ToString("0.##", CultureInfo.InvariantCulture),
            numericValue,
            unit);
    }

    private static void AddSnapshotFinding(
        ICollection<RuleCoachFindingItem> findings,
        bool shouldAdd,
        string summary,
        string category,
        string severity,
        string? className,
        string? specLabel,
        string relatedMetric,
        IReadOnlyList<CoachSkillEntity> coachSkills)
    {
        if (!shouldAdd)
        {
            return;
        }

        var comparison = new MatchBenchmarkComparisonItem(
            "spec",
            className,
            specLabel,
            category,
            relatedMetric,
            "0",
            null,
            null,
            "needs_review",
            null,
            "local_rule_coach",
            0);
        var matchingSkill = FindMatchingCoachSkill(comparison, coachSkills);

        findings.Add(new RuleCoachFindingItem(
            $"{Titleize(category)} pressure check",
            category,
            severity,
            BuildScopeLabel("spec", className, specLabel),
            summary,
            matchingSkill?.Goal ?? "Review the timeline and reinforce this phase with a repeatable drill.",
            matchingSkill is null
                ? "No direct stored drill matched this category yet."
                : $"Evidence count {matchingSkill.EvidenceCount}. {(matchingSkill.Drill ?? "No drill text saved yet.")}",
            relatedMetric));
    }

    private static CoachSkillEntity? FindMatchingCoachSkill(
        MatchBenchmarkComparisonItem comparison,
        IReadOnlyList<CoachSkillEntity> coachSkills)
    {
        var metricTokens = TokenizeForMatching(comparison.Metric);
        var categoryTokens = TokenizeForMatching(comparison.Category);

        return coachSkills
            .Where(skill =>
                string.Equals(skill.Scope, comparison.Scope, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skill.Scope, "global", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(skill => skill.EvidenceCount)
            .FirstOrDefault(skill =>
            {
                var areaTokens = TokenizeForMatching(skill.Area);
                var goalTokens = TokenizeForMatching(skill.Goal);
                return areaTokens.Overlaps(metricTokens) ||
                       goalTokens.Overlaps(metricTokens) ||
                       areaTokens.Overlaps(categoryTokens) ||
                       goalTokens.Overlaps(categoryTokens);
            });
    }

    private static string BuildEvidenceText(MatchBenchmarkComparisonItem comparison, CoachSkillEntity? matchingSkill)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(comparison.CurrentValue) || !string.IsNullOrWhiteSpace(comparison.ExpectedValue))
        {
            parts.Add($"Observed {comparison.CurrentValue ?? "unknown"} vs target {comparison.ExpectedValue ?? "unknown"}.");
        }

        if (comparison.EvidenceCount > 0)
        {
            parts.Add($"Coach memory evidence count {comparison.EvidenceCount}.");
        }

        if (matchingSkill is not null)
        {
            parts.Add($"Matched skill area {matchingSkill.Area} ({matchingSkill.EvidenceCount} reviews).");
        }

        return string.Join(" ", parts);
    }

    private static string BuildSummaryText(MatchBenchmarkComparisonItem comparison)
    {
        return comparison.Status switch
        {
            "below_target" => $"{Titleize(comparison.Metric)} is below the learned benchmark for this scope.",
            "above_target" when MetricPrefersLowerValues(comparison.Metric) =>
                $"{Titleize(comparison.Metric)} is above the preferred ceiling for this scope.",
            "above_target" => $"{Titleize(comparison.Metric)} exceeded the stored benchmark and should be reviewed for consistency.",
            _ => $"{Titleize(comparison.Metric)} has a stored benchmark, but this match still needs a human evidence check."
        };
    }

    private static string BuildScopeLabel(string scope, string? className, string? specLabel)
    {
        if (string.Equals(scope, "spec", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(specLabel))
        {
            return string.IsNullOrWhiteSpace(className) ? specLabel : $"{className} / {specLabel}";
        }

        if (string.Equals(scope, "class", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(className))
        {
            return className;
        }

        return string.Equals(scope, "match", StringComparison.OrdinalIgnoreCase) ? "Current match" : "Global";
    }

    private static string BuildComparisonKey(string scope, string category, string metric)
    {
        return $"{scope}|{NormalizeMetricKey(category)}|{NormalizeMetricKey(metric)}";
    }

    private static string EvaluateStatus(string metric, string? currentValue, string? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue) || string.IsNullOrWhiteSpace(expectedValue))
        {
            return "needs_review";
        }

        if (TryParseNumericValue(currentValue, out var currentNumeric) &&
            TryParseNumericValue(expectedValue, out var expectedNumeric))
        {
            if (MetricPrefersLowerValues(metric))
            {
                return currentNumeric <= expectedNumeric ? "aligned" : "above_target";
            }

            return currentNumeric >= expectedNumeric ? "aligned" : "below_target";
        }

        return string.Equals(currentValue.Trim(), expectedValue.Trim(), StringComparison.OrdinalIgnoreCase)
            ? "aligned"
            : "needs_review";
    }

    private static int StatusRank(string status)
    {
        return status switch
        {
            "below_target" => 0,
            "above_target" => 1,
            "needs_review" => 2,
            _ => 3
        };
    }

    private static bool MetricPrefersLowerValues(string metric)
    {
        var normalized = NormalizeMetricKey(metric);
        return normalized.Contains("death", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("damage_taken", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("late", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("delay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseNumericValue(string rawValue, out double value)
    {
        var sanitized = new string(rawValue
            .Where(character => char.IsDigit(character) || character is '.' or ',' or '-')
            .ToArray())
            .Replace(',', '.');

        return double.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeMetricKey(string value)
    {
        var buffer = new List<char>(value.Length);
        var lastWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Add(character);
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator)
            {
                continue;
            }

            buffer.Add('_');
            lastWasSeparator = true;
        }

        return new string(buffer.ToArray()).Trim('_');
    }

    private static HashSet<string> TokenizeForMatching(string? value)
    {
        return value is null
            ? []
            : NormalizeMetricKey(value)
                .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Titleize(string value)
    {
        return string.Join(
            " ",
            NormalizeMetricKey(value)
                .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..]));
    }

    private sealed record ObservedMetricValue(
        string Value,
        double NumericValue,
        string? Unit);
}
