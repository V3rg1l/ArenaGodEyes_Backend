using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class ManualAnalysisWorkflowService : IManualAnalysisWorkflowService
{
    private readonly ArenaGodEyesDbContext _dbContext;
    private readonly LocalDataPaths _localDataPaths;
    private readonly MatchAnalysisContextService _matchAnalysisContextService;

    public ManualAnalysisWorkflowService(
        ArenaGodEyesDbContext dbContext,
        LocalDataPaths localDataPaths,
        MatchAnalysisContextService matchAnalysisContextService)
    {
        _dbContext = dbContext;
        _localDataPaths = localDataPaths;
        _matchAnalysisContextService = matchAnalysisContextService;
    }

    public async Task<ChatGptPromptExport?> ExportPromptAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return null;
        }

        var matchJson = await File.ReadAllTextAsync(match.MatchJsonPath, cancellationToken);
        var participants = await _matchAnalysisContextService.BuildParticipantReviewItemsAsync(match, matchJson, cancellationToken);
        var promptText = BuildPrompt(match, matchJson, participants);
        var promptPath = Path.Combine(_localDataPaths.PromptsPath, $"{matchId}_chatgpt_prompt.md");
        await File.WriteAllTextAsync(promptPath, promptText, cancellationToken);

        return new ChatGptPromptExport(matchId, promptPath, promptText);
    }

    public async Task<ManualAnalysisImportResult?> ImportResponseAsync(
        string matchId,
        string responseText,
        CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .Include(item => item.ManualAnalyses)
            .Include(item => item.AnalysisInsights)
            .Include(item => item.ValidationTargets)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var promptPath = Path.Combine(_localDataPaths.PromptsPath, $"{matchId}_chatgpt_prompt.md");
        var promptText = File.Exists(promptPath)
            ? await File.ReadAllTextAsync(promptPath, cancellationToken)
            : string.Empty;
        var responsePath = Path.Combine(_localDataPaths.AiResponsesPath, $"{matchId}_chatgpt_response.md");
        await File.WriteAllTextAsync(responsePath, responseText, cancellationToken);

        var parsedJson = TryParseResponseJson(responseText);
        var markers = ParseMarkers(matchId, parsedJson);
        var insights = ParseInsights(parsedJson);
        var validationTargets = ParseValidationTargets(parsedJson);
        var trainingFocusItems = ParseTrainingFocus(parsedJson);

        match.HasManualAnalysis = true;
        match.UpdatedAt = DateTimeOffset.UtcNow;
        _dbContext.TimelineMarkers.RemoveRange(match.TimelineMarkers.Where(marker => marker.Source == "manual_chatgpt"));
        _dbContext.AnalysisInsights.RemoveRange(match.AnalysisInsights.Where(item => item.Source == "manual_chatgpt"));
        _dbContext.ValidationTargets.RemoveRange(match.ValidationTargets.Where(item => item.Source == "manual_chatgpt"));

        var analysis = new ManualAnalysisEntity
        {
            Match = match,
            MatchId = matchId,
            PromptText = promptText,
            ResponseText = responseText,
            ResponseJson = parsedJson?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            PromptPath = promptPath,
            ResponsePath = responsePath,
            ImportedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.ManualAnalyses.Add(analysis);

        foreach (var marker in markers)
        {
            _dbContext.TimelineMarkers.Add(new TimelineMarkerEntity
            {
                Match = match,
                MatchId = matchId,
                VideoSecond = marker.VideoSecond,
                Category = marker.Category,
                Severity = marker.Severity,
                Label = marker.Label,
                Description = marker.Description,
                Source = marker.Source,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var insight in insights)
        {
            _dbContext.AnalysisInsights.Add(new AnalysisInsightEntity
            {
                Match = match,
                MatchId = matchId,
                VideoSecond = insight.VideoSecond,
                Category = insight.Category,
                Severity = insight.Severity,
                Title = insight.Title,
                Summary = insight.Summary,
                Evidence = insight.Evidence,
                Recommendation = insight.Recommendation,
                Source = insight.Source,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var target in validationTargets)
        {
            _dbContext.ValidationTargets.Add(new ValidationTargetEntity
            {
                Match = match,
                MatchId = matchId,
                VideoSecond = target.VideoSecond,
                Category = target.Category,
                Metric = target.Metric,
                CurrentValue = target.CurrentValue,
                ExpectedValue = target.ExpectedValue,
                Unit = target.Unit,
                Note = target.Note,
                Source = target.Source,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await UpsertKnowledgeParametersAsync(match, validationTargets, cancellationToken);
        await UpsertCoachSkillsAsync(match, trainingFocusItems, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManualAnalysisImportResult(matchId, responsePath, markers.Count, "manual_chatgpt");
    }

    private async Task UpsertKnowledgeParametersAsync(
        MatchRecordEntity match,
        IReadOnlyList<ValidationTargetItem> validationTargets,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var target in validationTargets)
        {
            var className = string.IsNullOrWhiteSpace(match.PlayerClassName) ? null : match.PlayerClassName;
            var specLabel = string.IsNullOrWhiteSpace(match.PlayerSpecLabel) ? null : match.PlayerSpecLabel;

            if (!string.IsNullOrWhiteSpace(className))
            {
                await UpsertKnowledgeParameterAsync(
                    "class",
                    className,
                    null,
                    target,
                    now,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(specLabel))
            {
                await UpsertKnowledgeParameterAsync(
                    "spec",
                    className,
                    specLabel,
                    target,
                    now,
                    cancellationToken);
            }
        }
    }

    private async Task UpsertCoachSkillsAsync(
        MatchRecordEntity match,
        IReadOnlyList<TrainingFocusItem> trainingFocusItems,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var item in trainingFocusItems)
        {
            var className = string.IsNullOrWhiteSpace(match.PlayerClassName) ? null : match.PlayerClassName;
            var specLabel = string.IsNullOrWhiteSpace(match.PlayerSpecLabel) ? null : match.PlayerSpecLabel;

            if (!string.IsNullOrWhiteSpace(className))
            {
                await UpsertCoachSkillAsync(
                    "class",
                    className,
                    null,
                    item,
                    now,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(specLabel))
            {
                await UpsertCoachSkillAsync(
                    "spec",
                    className,
                    specLabel,
                    item,
                    now,
                    cancellationToken);
            }
        }
    }

    private async Task UpsertKnowledgeParameterAsync(
        string scope,
        string? className,
        string? specLabel,
        ValidationTargetItem target,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CoachKnowledgeParameters.SingleOrDefaultAsync(
            item => item.Scope == scope &&
                    item.ClassName == className &&
                    item.SpecLabel == specLabel &&
                    item.Category == target.Category &&
                    item.Metric == target.Metric,
            cancellationToken);

        if (existing is null)
        {
            _dbContext.CoachKnowledgeParameters.Add(new CoachKnowledgeParameterEntity
            {
                Scope = scope,
                ClassName = className,
                SpecLabel = specLabel,
                Category = target.Category,
                Metric = target.Metric,
                TargetValue = target.ExpectedValue,
                Unit = target.Unit,
                Note = target.Note,
                Source = target.Source,
                EvidenceCount = 1,
                CreatedAt = now,
                UpdatedAt = now
            });

            return;
        }

        existing.TargetValue = target.ExpectedValue ?? existing.TargetValue;
        existing.Unit = target.Unit ?? existing.Unit;
        existing.Note = target.Note ?? existing.Note;
        existing.Source = target.Source;
        existing.EvidenceCount += 1;
        existing.UpdatedAt = now;
    }

    private async Task UpsertCoachSkillAsync(
        string scope,
        string? className,
        string? specLabel,
        TrainingFocusItem item,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CoachSkills.SingleOrDefaultAsync(
            skill => skill.Scope == scope &&
                     skill.ClassName == className &&
                     skill.SpecLabel == specLabel &&
                     skill.Area == item.Area &&
                     skill.Goal == item.Goal,
            cancellationToken);

        if (existing is null)
        {
            _dbContext.CoachSkills.Add(new CoachSkillEntity
            {
                Scope = scope,
                ClassName = className,
                SpecLabel = specLabel,
                Area = item.Area,
                Goal = item.Goal,
                Drill = item.Drill,
                Source = item.Source,
                EvidenceCount = 1,
                CreatedAt = now,
                UpdatedAt = now
            });

            return;
        }

        existing.Drill = item.Drill ?? existing.Drill;
        existing.Source = item.Source;
        existing.EvidenceCount += 1;
        existing.UpdatedAt = now;
    }

    private static string BuildPrompt(
        MatchRecordEntity match,
        string matchJson,
        IReadOnlyList<MatchParticipantReviewItem> participants)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ArenaGodEyes Manual ChatGPT Review");
        builder.AppendLine();
        builder.AppendLine("Return only one JSON object.");
        builder.AppendLine();
        builder.AppendLine("Required top-level fields:");
        builder.AppendLine("- summary");
        builder.AppendLine("- mainMistakes");
        builder.AppendLine("- bestPlays");
        builder.AppendLine("- timelineMarkers");
        builder.AppendLine("- trainingFocus");
        builder.AppendLine("- validationTargets");
        builder.AppendLine("- participantFindings");
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Use only the provided JSON and data.");
        builder.AppendLine("- Do not invent events.");
        builder.AppendLine("- If the data is uncertain, say \"possible\".");
        builder.AppendLine("- Be short, direct, and actionable.");
        builder.AppendLine("- Use timestamps whenever possible.");
        builder.AppendLine("- If you provide timestamps, also provide `videoSecond` when possible.");
        builder.AppendLine("- Focus on things that can be validated again during video review.");
        builder.AppendLine("- Prefer coaching language that explains both the mistake and the replacement pattern.");
        builder.AppendLine();
        builder.AppendLine($"MatchId: {match.MatchId}");
        builder.AppendLine($"Bracket: {match.Bracket}");
        builder.AppendLine($"Map: {match.MapName}");
        builder.AppendLine($"DurationSeconds: {match.DurationSeconds}");
        builder.AppendLine();
        builder.AppendLine("Analysis scope:");
        builder.AppendLine("- Default to the tracked player for direct coaching.");
        builder.AppendLine("- Also use the other participants to understand peels, pressure, rotation, mitigation, defensive trades, setup quality, and missed punish windows.");
        builder.AppendLine("- When evidence is strong, mention both the player's mistake and the enemy or teammate action that created the moment.");
        builder.AppendLine();
        builder.AppendLine("Coaching priorities:");
        builder.AppendLine("- Rotation efficiency: uptime, filler misuse, burst sequencing, proc conversion, and wasted globals.");
        builder.AppendLine("- Positioning and movement: line-of-sight, pillar usage, overextension, chase discipline, and mobility trade value.");
        builder.AppendLine("- Damage mitigation: pre-wall vs panic wall, overlapping defensives, immunities, externals, and healer relief timing.");
        builder.AppendLine("- Control and peels: cross-cc, interrupt timing, stop chains, peel quality, and teammate save windows.");
        builder.AppendLine("- Win conditions: setup quality, target swaps, kill window creation, punish windows, and defensive baiting.");
        builder.AppendLine("- Team context: use every participant's class/spec toolkit when explaining what should have happened.");
        builder.AppendLine();
        builder.AppendLine("If the evidence allows it, explicitly call out:");
        builder.AppendLine("- missed defensives");
        builder.AppendLine("- bad burst sync");
        builder.AppendLine("- poor movement or positioning");
        builder.AppendLine("- weak peel support");
        builder.AppendLine("- low pressure conversion after enemy cooldowns");
        builder.AppendLine("- good trades and setups worth repeating");
        builder.AppendLine();
        builder.AppendLine("## Participant Coach Context");
        foreach (var participant in participants)
        {
            builder.AppendLine($"### {(participant.IsTrackedPlayer ? "[Tracked Player] " : string.Empty)}{participant.Name}");
            builder.AppendLine($"- TeamId: {participant.TeamId}");
            builder.AppendLine($"- Class/Spec: {participant.ClassName ?? "Unknown Class"} / {participant.SpecLabel ?? "Unknown Spec"}");
            builder.AppendLine($"- Rating: {participant.PersonalRating}");
            if (participant.ProfileSnapshot is not null)
            {
                builder.AppendLine(
                    $"- Profile snapshot: core={participant.ProfileSnapshot.CoreSpellUsageCount}, burst={participant.ProfileSnapshot.BurstSpellUsageCount}, defensive={participant.ProfileSnapshot.DefensiveSpellUsageCount}, control={participant.ProfileSnapshot.ControlSpellUsageCount}, interrupt={participant.ProfileSnapshot.InterruptSpellUsageCount}, mobility={participant.ProfileSnapshot.MobilitySpellUsageCount}");
            }

            if (participant.CoachKnowledgeParameters.Count > 0)
            {
                builder.AppendLine("- coachKnowledgeParameters:");
                foreach (var parameter in participant.CoachKnowledgeParameters.Take(8))
                {
                    builder.AppendLine(
                        $"  - [{parameter.Scope}] {parameter.Category} / {parameter.Metric} => target={parameter.TargetValue ?? "n/a"} {parameter.Unit ?? string.Empty} note={parameter.Note ?? "n/a"} evidence={parameter.EvidenceCount}");
                }
            }

            if (participant.CoachSkills.Count > 0)
            {
                builder.AppendLine("- coachSkills:");
                foreach (var skill in participant.CoachSkills.Take(8))
                {
                    builder.AppendLine(
                        $"  - [{skill.Scope}] {skill.Area} => goal={skill.Goal} drill={skill.Drill ?? "n/a"} evidence={skill.EvidenceCount}");
                }
            }

            builder.AppendLine();
        }

        builder.AppendLine("JSON schema example:");
        builder.AppendLine("```json");
        builder.AppendLine("""
{
  "summary": "Short review summary.",
  "mainMistakes": [
    {
      "timestamp": "01:24",
      "videoSecond": 84,
      "category": "defensive",
      "severity": "high",
      "title": "Held major defensive too long",
      "whatHappened": "Short factual description.",
      "whyItMatters": "Why this matters.",
      "betterPlay": "What should have happened.",
      "evidence": "Match JSON evidence used."
    }
  ],
  "bestPlays": [
    {
      "timestamp": "00:43",
      "videoSecond": 43,
      "category": "offensive",
      "title": "Strong setup",
      "summary": "What was good.",
      "evidence": "Why this was good."
    }
  ],
  "timelineMarkers": [
    {
      "timestamp": "01:24",
      "videoSecond": 84,
      "type": "mistake",
      "label": "Late defensive",
      "description": "Short timeline note."
    }
  ],
  "trainingFocus": [
    {
      "participant": "Tracked Player",
      "area": "defensive trading",
      "goal": "Trade earlier into enemy burst",
      "drill": "Review burst windows and compare cooldown timing."
    }
  ],
  "validationTargets": [
    {
      "timestamp": "01:24",
      "videoSecond": 84,
      "category": "defensive",
      "metric": "major_defensive_timing",
      "currentValue": "late",
      "expectedValue": "before lethal burst",
      "unit": "timing",
      "note": "Can be re-checked in video review."
    }
  ],
  "participantFindings": [
    {
      "participant": "Tracked Player",
      "classSpec": "Warlock / Affliction",
      "strengths": [
        "Maintained pressure during safe windows"
      ],
      "mistakes": [
        "Delayed wall into enemy burst"
      ],
      "nextFocus": [
        "Pre-wall earlier when both enemy DPS commit"
      ]
    }
  ]
}
""");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Match JSON");
        builder.AppendLine("```json");
        builder.AppendLine(matchJson);
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static JsonNode? TryParseResponseJson(string responseText)
    {
        var startIndex = responseText.IndexOf('{');
        var endIndex = responseText.LastIndexOf('}');
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return null;
        }

        var jsonText = responseText[startIndex..(endIndex + 1)];
        try
        {
            return JsonNode.Parse(jsonText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<TimelineMarkerItem> ParseMarkers(string matchId, JsonNode? responseJson)
    {
        var markers = new List<TimelineMarkerItem>();
        if (responseJson is null)
        {
            return markers;
        }

        var markerArray = responseJson["timelineMarkers"]?.AsArray();
        if (markerArray is not null)
        {
            foreach (var item in markerArray)
            {
                if (item is null)
                {
                    continue;
                }

                var second = item["videoSecond"]?.GetValue<int?>() ?? ParseTimestampToSeconds(item["timestamp"]?.GetValue<string>());
                if (second is null)
                {
                    continue;
                }

                markers.Add(new TimelineMarkerItem(
                    second.Value,
                    item["type"]?.GetValue<string>() ?? "note",
                    "medium",
                    item["label"]?.GetValue<string>() ?? "ChatGPT note",
                    item["description"]?.GetValue<string>() ?? string.Empty,
                    "manual_chatgpt"));
            }
        }

        var mistakesArray = responseJson["mainMistakes"]?.AsArray();
        if (mistakesArray is not null)
        {
            foreach (var item in mistakesArray)
            {
                if (item is null)
                {
                    continue;
                }

                var second = item["videoSecond"]?.GetValue<int?>() ?? ParseTimestampToSeconds(item["timestamp"]?.GetValue<string>());
                if (second is null)
                {
                    continue;
                }

                markers.Add(new TimelineMarkerItem(
                    second.Value,
                    item["category"]?.GetValue<string>() ?? "mistake",
                    item["severity"]?.GetValue<string>() ?? "high",
                    item["title"]?.GetValue<string>() ?? "Mistake",
                    item["betterPlay"]?.GetValue<string>()
                        ?? item["whatHappened"]?.GetValue<string>()
                        ?? string.Empty,
                    "manual_chatgpt"));
            }
        }

        return markers
            .GroupBy(marker => new { marker.VideoSecond, marker.Label, marker.Source })
            .Select(group => group.First())
            .OrderBy(marker => marker.VideoSecond)
            .ToList();
    }

    private static List<AnalysisInsightItem> ParseInsights(JsonNode? responseJson)
    {
        var results = new List<AnalysisInsightItem>();
        if (responseJson is null)
        {
            return results;
        }

        AddInsightsFromArray(results, responseJson["mainMistakes"]?.AsArray(), "mistake", "manual_chatgpt");
        AddInsightsFromArray(results, responseJson["bestPlays"]?.AsArray(), "best_play", "manual_chatgpt");

        return results
            .GroupBy(item => new { item.VideoSecond, item.Title, item.Source })
            .Select(group => group.First())
            .OrderBy(item => item.VideoSecond)
            .ThenBy(item => item.Title)
            .ToList();
    }

    private static void AddInsightsFromArray(
        List<AnalysisInsightItem> results,
        JsonArray? items,
        string defaultCategory,
        string source)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            results.Add(new AnalysisInsightItem(
                item["videoSecond"]?.GetValue<int?>() ?? ParseTimestampToSeconds(item["timestamp"]?.GetValue<string>()),
                item["category"]?.GetValue<string>() ?? defaultCategory,
                item["severity"]?.GetValue<string>() ?? "medium",
                item["title"]?.GetValue<string>() ?? "Insight",
                item["summary"]?.GetValue<string>()
                    ?? item["whatHappened"]?.GetValue<string>()
                    ?? string.Empty,
                item["evidence"]?.GetValue<string>() ?? item["whyItMatters"]?.GetValue<string>(),
                item["betterPlay"]?.GetValue<string>() ?? item["recommendation"]?.GetValue<string>(),
                source));
        }
    }

    private static List<ValidationTargetItem> ParseValidationTargets(JsonNode? responseJson)
    {
        var results = new List<ValidationTargetItem>();
        var items = responseJson?["validationTargets"]?.AsArray();
        if (items is null)
        {
            return results;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            results.Add(new ValidationTargetItem(
                item["videoSecond"]?.GetValue<int?>() ?? ParseTimestampToSeconds(item["timestamp"]?.GetValue<string>()),
                item["category"]?.GetValue<string>() ?? "validation",
                item["metric"]?.GetValue<string>() ?? "unknown_metric",
                item["currentValue"]?.GetValue<string>(),
                item["expectedValue"]?.GetValue<string>(),
                item["unit"]?.GetValue<string>(),
                item["note"]?.GetValue<string>(),
                "manual_chatgpt"));
        }

        return results
            .GroupBy(item => new { item.VideoSecond, item.Metric, item.Source })
            .Select(group => group.First())
            .OrderBy(item => item.VideoSecond)
            .ThenBy(item => item.Metric)
            .ToList();
    }

    private static List<TrainingFocusItem> ParseTrainingFocus(JsonNode? responseJson)
    {
        var results = new List<TrainingFocusItem>();
        var items = responseJson?["trainingFocus"]?.AsArray();
        if (items is null)
        {
            return results;
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            var area = item["area"]?.GetValue<string>();
            var goal = item["goal"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(area) || string.IsNullOrWhiteSpace(goal))
            {
                continue;
            }

            results.Add(new TrainingFocusItem(
                area,
                goal,
                item["drill"]?.GetValue<string>(),
                "manual_chatgpt"));
        }

        return results
            .GroupBy(item => new { item.Area, item.Goal, item.Source })
            .Select(group => group.First())
            .OrderBy(item => item.Area)
            .ThenBy(item => item.Goal)
            .ToList();
    }

    private static int? ParseTimestampToSeconds(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return null;
        }

        var parts = timestamp.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            2 when int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds)
                => minutes * 60 + seconds,
            3 when int.TryParse(parts[0], out var hours) &&
                   int.TryParse(parts[1], out var wholeMinutes) &&
                   int.TryParse(parts[2], out var wholeSeconds)
                => (hours * 3600) + (wholeMinutes * 60) + wholeSeconds,
            _ => null
        };
    }

    private sealed record TrainingFocusItem(
        string Area,
        string Goal,
        string? Drill,
        string Source);
}
