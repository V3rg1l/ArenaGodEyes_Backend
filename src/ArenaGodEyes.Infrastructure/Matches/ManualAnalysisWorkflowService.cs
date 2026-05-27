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

    public ManualAnalysisWorkflowService(ArenaGodEyesDbContext dbContext, LocalDataPaths localDataPaths)
    {
        _dbContext = dbContext;
        _localDataPaths = localDataPaths;
    }

    public async Task<ChatGptPromptExport?> ExportPromptAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return null;
        }

        var matchJson = await File.ReadAllTextAsync(match.MatchJsonPath, cancellationToken);
        var promptText = BuildPrompt(match, matchJson);
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

        match.HasManualAnalysis = true;
        match.UpdatedAt = DateTimeOffset.UtcNow;
        _dbContext.TimelineMarkers.RemoveRange(match.TimelineMarkers.Where(marker => marker.Source == "manual_chatgpt"));

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ManualAnalysisImportResult(matchId, responsePath, markers.Count, "manual_chatgpt");
    }

    private static string BuildPrompt(MatchRecordEntity match, string matchJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ArenaGodEyes Manual ChatGPT Review");
        builder.AppendLine();
        builder.AppendLine("Return a JSON response with:");
        builder.AppendLine("- summary");
        builder.AppendLine("- mainMistakes");
        builder.AppendLine("- bestPlays");
        builder.AppendLine("- timelineMarkers");
        builder.AppendLine("- trainingFocus");
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Use only the provided JSON and data.");
        builder.AppendLine("- Do not invent events.");
        builder.AppendLine("- If the data is uncertain, say \"possible\".");
        builder.AppendLine("- Be short, direct, and actionable.");
        builder.AppendLine("- Use timestamps whenever possible.");
        builder.AppendLine();
        builder.AppendLine($"MatchId: {match.MatchId}");
        builder.AppendLine($"Bracket: {match.Bracket}");
        builder.AppendLine($"Map: {match.MapName}");
        builder.AppendLine($"DurationSeconds: {match.DurationSeconds}");
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
}
