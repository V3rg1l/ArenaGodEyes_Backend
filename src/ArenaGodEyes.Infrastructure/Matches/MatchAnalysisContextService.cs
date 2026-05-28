using System.Text.Json.Nodes;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class MatchAnalysisContextService
{
    private readonly ArenaGodEyesDbContext _dbContext;

    public MatchAnalysisContextService(ArenaGodEyesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MatchParticipantReviewItem>> BuildParticipantReviewItemsAsync(
        MatchRecordEntity match,
        string matchJson,
        CancellationToken cancellationToken = default)
    {
        var participants = ParseParticipants(matchJson, match);
        if (participants.Count == 0)
        {
            return [];
        }

        var classNames = participants
            .Select(item => item.ClassName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var specLabels = participants
            .Select(item => item.SpecLabel)
            .Where(item => !string.IsNullOrWhiteSpace(item) && !string.Equals(item, "Unknown Spec", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var knowledgeEntities = await _dbContext.CoachKnowledgeParameters
            .Where(item => item.Scope == "global" ||
                           (item.Scope == "class" && item.ClassName != null && classNames.Contains(item.ClassName)) ||
                           (item.Scope == "spec" && item.SpecLabel != null && specLabels.Contains(item.SpecLabel)))
            .ToListAsync(cancellationToken);
        var skillEntities = await _dbContext.CoachSkills
            .Where(item => item.Scope == "global" ||
                           (item.Scope == "class" && item.ClassName != null && classNames.Contains(item.ClassName)) ||
                           (item.Scope == "spec" && item.SpecLabel != null && specLabels.Contains(item.SpecLabel)))
            .ToListAsync(cancellationToken);

        return participants
            .Select(participant => new MatchParticipantReviewItem(
                participant.Guid,
                participant.Name,
                participant.Realm,
                participant.Region,
                participant.TeamId,
                participant.ClassName,
                participant.SpecLabel,
                participant.PersonalRating,
                participant.HighestPvpTier,
                participant.IsTrackedPlayer,
                participant.ProfileSnapshot,
                knowledgeEntities
                    .Where(item => MatchesParticipant(item.Scope, item.ClassName, item.SpecLabel, participant))
                    .OrderByDescending(item => item.EvidenceCount)
                    .ThenByDescending(item => item.UpdatedAt)
                    .Take(12)
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
                skillEntities
                    .Where(item => MatchesParticipant(item.Scope, item.ClassName, item.SpecLabel, participant))
                    .OrderByDescending(item => item.EvidenceCount)
                    .ThenByDescending(item => item.UpdatedAt)
                    .Take(12)
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
                    .ToList()))
            .OrderBy(item => item.TeamId)
            .ThenByDescending(item => item.IsTrackedPlayer)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesParticipant(
        string scope,
        string? className,
        string? specLabel,
        ParsedParticipant participant)
    {
        if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(scope, "class", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(className) &&
                   string.Equals(className, participant.ClassName, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(scope, "spec", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(specLabel) &&
                   string.Equals(specLabel, participant.SpecLabel, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static List<ParsedParticipant> ParseParticipants(string matchJson, MatchRecordEntity match)
    {
        var root = JsonNode.Parse(matchJson)?.AsObject();
        var players = root?["players"]?.AsArray();
        if (players is null)
        {
            return [];
        }

        var participants = new List<ParsedParticipant>();
        foreach (var playerNode in players)
        {
            if (playerNode is not JsonObject player)
            {
                continue;
            }

            var guid = player["guid"]?.GetValue<string>();
            var name = player["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            participants.Add(new ParsedParticipant(
                guid,
                name,
                player["realm"]?.GetValue<string>(),
                player["region"]?.GetValue<string>(),
                player["teamId"]?.GetValue<int?>() ?? -1,
                NormalizeUnknown(player["className"]?.GetValue<string>()),
                NormalizeUnknown(player["specLabel"]?.GetValue<string>()),
                player["personalRating"]?.GetValue<int?>() ?? 0,
                player["highestPvpTier"]?.GetValue<int?>() ?? 0,
                string.Equals(guid, match.PlayerGuid, StringComparison.OrdinalIgnoreCase),
                ParseProfileSnapshot(player["profileSnapshot"])));
        }

        if (participants.Count > 0)
        {
            return participants;
        }

        if (!string.IsNullOrWhiteSpace(match.PlayerGuid) && !string.IsNullOrWhiteSpace(match.PlayerName))
        {
            return
            [
                new ParsedParticipant(
                    match.PlayerGuid,
                    match.PlayerName,
                    null,
                    null,
                    match.WinningTeamId ?? -1,
                    NormalizeUnknown(match.PlayerClassName),
                    NormalizeUnknown(match.PlayerSpecLabel),
                    0,
                    0,
                    true,
                    null)
            ];
        }

        return [];
    }

    private static SpecPerformanceSnapshotItem? ParseProfileSnapshot(JsonNode? node)
    {
        if (node is not JsonObject snapshot)
        {
            return null;
        }

        return new SpecPerformanceSnapshotItem(
            snapshot["className"]?.GetValue<string>(),
            snapshot["specLabel"]?.GetValue<string>(),
            snapshot["recognizedSpellCount"]?.GetValue<int?>() ?? 0,
            snapshot["coreSpellUsageCount"]?.GetValue<int?>() ?? 0,
            snapshot["burstSpellUsageCount"]?.GetValue<int?>() ?? 0,
            snapshot["defensiveSpellUsageCount"]?.GetValue<int?>() ?? 0,
            snapshot["controlSpellUsageCount"]?.GetValue<int?>() ?? 0,
            snapshot["interruptSpellUsageCount"]?.GetValue<int?>() ?? 0,
            snapshot["mobilitySpellUsageCount"]?.GetValue<int?>() ?? 0);
    }

    private static string? NormalizeUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "Unknown Spec", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;

    private sealed record ParsedParticipant(
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
        SpecPerformanceSnapshotItem? ProfileSnapshot);
}
