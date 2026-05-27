using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Models;
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
        match.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MatchReviewDetails?> GetAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .Include(item => item.ManualAnalyses)
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

        var details = new MatchReviewDetails(
            ToLibraryItem(match),
            matchJson,
            promptText,
            manualAnalysisText,
            match.TimelineMarkers
                .OrderBy(marker => marker.VideoSecond)
                .Select(marker => new TimelineMarkerItem(
                    marker.VideoSecond,
                    marker.Category,
                    marker.Severity,
                    marker.Label,
                    marker.Description,
                    marker.Source))
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
            match.VideoLocalPath);
}
