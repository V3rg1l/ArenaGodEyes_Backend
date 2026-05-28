using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Core.Application.Video.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class MatchLibraryService : IMatchLibraryService
{
    private readonly ArenaGodEyesDbContext _dbContext;

    public MatchLibraryService(ArenaGodEyesDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var knowledgeParameters = await _dbContext.CoachKnowledgeParameters
            .Where(item => item.Scope == "global" ||
                           (!string.IsNullOrWhiteSpace(match.PlayerSpecLabel) && item.SpecLabel == match.PlayerSpecLabel))
            .OrderByDescending(item => item.EvidenceCount)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(24)
            .ToListAsync(cancellationToken);
        var coachSkills = await _dbContext.CoachSkills
            .Where(item => item.Scope == "global" ||
                           (!string.IsNullOrWhiteSpace(match.PlayerSpecLabel) && item.SpecLabel == match.PlayerSpecLabel))
            .OrderByDescending(item => item.EvidenceCount)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(24)
            .ToListAsync(cancellationToken);

        var details = new MatchReviewDetails(
            ToLibraryItem(match),
            matchJson,
            promptText,
            manualAnalysisText,
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
                    metric.SpellName,
                    metric.CastCount,
                    metric.TotalDamage,
                    metric.TotalHealing))
                .ToList(),
            knowledgeParameters
                .Select(item => new CoachKnowledgeParameterItem(
                    item.Scope,
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
                    item.SpecLabel,
                    item.Area,
                    item.Goal,
                    item.Drill,
                    item.Source,
                    item.EvidenceCount,
                    item.UpdatedAt))
                .ToList(),
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
                .ToList());

        return details;
    }

    public async Task<IReadOnlyList<MatchLibraryItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var matches = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .ToListAsync(cancellationToken);

        return matches
            .OrderByDescending(item => item.StartedAt.UtcDateTime)
            .Select(ToLibraryItem)
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

    private static MatchLibraryItem ToLibraryItem(MatchRecordEntity match) =>
        new(
            match.MatchId,
            match.StartedAt,
            match.Bracket,
            match.MapName,
            match.DurationSeconds,
            match.ResultForPlayer,
            match.PlayerName,
            match.PlayerSpecLabel,
            !string.IsNullOrWhiteSpace(match.VideoLocalPath),
            match.HasManualAnalysis,
            match.TimelineMarkers.Count,
            match.MatchJsonPath,
            match.VideoLocalPath,
            match.ThumbnailPath,
            match.RecordingStatus,
            match.VideoDurationSeconds,
            match.VideoResolution);
}
