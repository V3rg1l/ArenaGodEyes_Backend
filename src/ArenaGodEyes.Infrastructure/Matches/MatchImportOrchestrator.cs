using System.Globalization;
using System.Text;
using System.Text.Json;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class MatchImportOrchestrator : IMatchImportOrchestrator
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ArenaGodEyesDbContext _dbContext;
    private readonly LocalDataPaths _localDataPaths;
    private readonly ILogger<MatchImportOrchestrator> _logger;
    private readonly WowKnowledgeService _wowKnowledgeService;

    public MatchImportOrchestrator(
        ArenaGodEyesDbContext dbContext,
        LocalDataPaths localDataPaths,
        ILogger<MatchImportOrchestrator> logger,
        WowKnowledgeService wowKnowledgeService)
    {
        _dbContext = dbContext;
        _localDataPaths = localDataPaths;
        _logger = logger;
        _wowKnowledgeService = wowKnowledgeService;
    }

    public async Task<ImportedMatchesResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Combat log file was not found.", sourceFilePath);
        }

        var rawLines = await File.ReadAllLinesAsync(sourceFilePath, cancellationToken);
        var parsedLines = rawLines
            .Select((rawLine, index) => CombatLogLineParser.TryParse(rawLine, index + 1, sourceFilePath))
            .Where(line => line is not null)
            .Cast<ParsedCombatLogLine>()
            .ToList();

        var matches = BuildMatchCandidates(parsedLines);
        var importedMatches = new List<ImportedMatchSummary>();
        var parseErrorCount = parsedLines.Count(line => !line.IsTimestampParsed);

        foreach (var candidate in matches)
        {
            var imported = await PersistMatchAsync(candidate, sourceFilePath, cancellationToken);
            importedMatches.Add(imported);
        }

        return new ImportedMatchesResult(
            sourceFilePath,
            rawLines.Length,
            importedMatches.Count,
            parseErrorCount,
            importedMatches);
    }

    private async Task<ImportedMatchSummary> PersistMatchAsync(
        MatchCandidate candidate,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        candidate.ApplyInferredPlayerProfiles(_wowKnowledgeService);
        var matchId = BuildMatchId(candidate);
        var chunkPath = Path.Combine(_localDataPaths.ChunksPath, $"{matchId}.txt");
        var jsonPath = Path.Combine(_localDataPaths.MatchesPath, $"{matchId}.json");

        await File.WriteAllLinesAsync(chunkPath, candidate.Lines.Select(line => line.RawLine), cancellationToken);

        var jsonDocument = MatchJsonBuilder.Build(candidate, matchId, sourceFilePath, chunkPath);
        var json = JsonSerializer.Serialize(jsonDocument, JsonSerializerOptions);
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

        var existing = await _dbContext.Matches
            .Include(match => match.TimelineMarkers)
            .Include(match => match.ManualAnalyses)
            .SingleOrDefaultAsync(match => match.MatchId == matchId, cancellationToken);

        var entity = existing ?? new MatchRecordEntity
        {
            MatchId = matchId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        entity.StartedAt = candidate.StartedAt;
        entity.EndedAt = candidate.EndedAt;
        entity.Bracket = candidate.Bracket;
        entity.MatchType = candidate.IsRanked ? "rated" : "skirmish";
        entity.MapId = candidate.MapId;
        entity.MapName = candidate.MapName;
        entity.DurationSeconds = candidate.DurationSeconds;
        entity.WinningTeamId = candidate.WinningTeamId;
        entity.PlayerGuid = candidate.PlayerGuid;
        entity.PlayerName = candidate.PlayerName;
        entity.PlayerSpecLabel = candidate.PlayerSpecLabel;
        entity.ResultForPlayer = candidate.ResultForPlayer;
        entity.SourceCombatLogFile = sourceFilePath;
        entity.ChunkFilePath = chunkPath;
        entity.MatchJsonPath = jsonPath;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _dbContext.Matches.Add(entity);
        }

        if (string.IsNullOrWhiteSpace(entity.PlayerSpecLabel) ||
            string.Equals(entity.PlayerSpecLabel, "Unknown Spec", StringComparison.OrdinalIgnoreCase))
        {
            entity.PlayerSpecLabel = candidate.PlayerSpecLabel;
        }

        entity.PlayerClassName ??= candidate.PlayerClassName;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PersistMetricsAsync(entity, candidate.Lines, candidate.StartedAt, cancellationToken);

        _logger.LogInformation("Imported match {MatchId} from {SourceFile}", matchId, sourceFilePath);

        return new ImportedMatchSummary(
            matchId,
            entity.Bracket,
            entity.MapId,
            entity.MapName,
            entity.DurationSeconds,
            chunkPath,
            jsonPath);
    }

    private async Task PersistMetricsAsync(
        MatchRecordEntity match,
        IReadOnlyList<ParsedCombatLogLine> lines,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var metrics = MatchMetricsCalculator.CalculateStructured(lines, startedAt, match.DurationSeconds);
        var playerSpellNames = metrics.PlayerSpellNamesBySourceGuid.TryGetValue(match.PlayerGuid ?? string.Empty, out var trackedPlayerSpellNames)
            ? trackedPlayerSpellNames
            : [];
        var inferredProfile = _wowKnowledgeService.InferPlayerProfile(playerSpellNames, match.PlayerSpecLabel);

        if (!string.IsNullOrWhiteSpace(inferredProfile.ClassName))
        {
            match.PlayerClassName = inferredProfile.ClassName;
        }

        if (!string.IsNullOrWhiteSpace(inferredProfile.SpecLabel))
        {
            match.PlayerSpecLabel = inferredProfile.SpecLabel;
        }

        var existingSummary = await _dbContext.MatchMetricSummaries
            .SingleOrDefaultAsync(item => item.MatchId == match.MatchId, cancellationToken);
        if (existingSummary is not null)
        {
            _dbContext.MatchMetricSummaries.Remove(existingSummary);
        }

        var existingSpellMetrics = await _dbContext.MatchSpellMetrics
            .Where(item => item.MatchId == match.MatchId)
            .ToListAsync(cancellationToken);
        if (existingSpellMetrics.Count > 0)
        {
            _dbContext.MatchSpellMetrics.RemoveRange(existingSpellMetrics);
        }

        _dbContext.MatchMetricSummaries.Add(new MatchMetricSummaryEntity
        {
            Match = match,
            MatchId = match.MatchId,
            TotalCasts = metrics.Summary.TotalCasts,
            TotalDamage = metrics.Summary.TotalDamage,
            TotalHealing = metrics.Summary.TotalHealing,
            InterruptCount = metrics.Summary.InterruptCount,
            DeathCount = metrics.Summary.DeathCount,
            DamagePerSecond = metrics.Summary.DamagePerSecond,
            HealingPerSecond = metrics.Summary.HealingPerSecond,
            CastsPerMinute = metrics.Summary.CastsPerMinute,
            CreatedAt = DateTimeOffset.UtcNow
        });

        foreach (var spellMetric in metrics.SpellMetrics)
        {
            var enriched = _wowKnowledgeService.EnrichSpell(
                spellMetric.SpellName,
                match.PlayerClassName,
                match.PlayerSpecLabel);

            _dbContext.MatchSpellMetrics.Add(new MatchSpellMetricEntity
            {
                Match = match,
                MatchId = match.MatchId,
                SpellName = spellMetric.SpellName,
                NormalizedSpellName = enriched.NormalizedSpellName,
                CastCount = spellMetric.CastCount,
                TotalDamage = spellMetric.TotalDamage,
                TotalHealing = spellMetric.TotalHealing,
                ClassName = enriched.ClassName,
                SpecLabel = enriched.SpecLabel,
                PrimaryCategory = enriched.PrimaryCategory,
                TacticalPhase = enriched.TacticalPhase,
                IsSignatureSpell = enriched.IsSignatureSpell,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<MatchCandidate> BuildMatchCandidates(IReadOnlyList<ParsedCombatLogLine> parsedLines)
    {
        var matches = new List<MatchCandidate>();
        MatchCandidate? current = null;

        foreach (var line in parsedLines)
        {
            if (line.EventName == "ARENA_MATCH_START")
            {
                if (current is not null && current.Lines.Count > 0)
                {
                    current.FinalizeFromLines();
                    matches.Add(current);
                }

                current = MatchCandidate.Create(line);
            }

            if (current is null)
            {
                continue;
            }

            current.AddLine(line);

            if (line.EventName == "ARENA_MATCH_END")
            {
                current.FinalizeFromLines();
                matches.Add(current);
                current = null;
            }
        }

        if (current is not null && current.Lines.Count > 0)
        {
            current.FinalizeFromLines();
            matches.Add(current);
        }

        return matches;
    }

    private static string BuildMatchId(MatchCandidate candidate)
    {
        var shortGuid = candidate.PlayerGuid?.Split('-').LastOrDefault()?.ToLowerInvariant() ?? "unknown";
        return $"{candidate.StartedAt:yyyyMMdd-HHmmss}-{candidate.Bracket}-{candidate.MapId}-{shortGuid}";
    }

    private sealed class MatchCandidate
    {
        private readonly Dictionary<string, string> _guidToName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CombatantRecord> _combatants = [];

        private MatchCandidate(ParsedCombatLogLine startLine)
        {
            StartedAt = startLine.Timestamp ?? DateTimeOffset.UtcNow;
            MapId = ParseInt(startLine.Fields.ElementAtOrDefault(1));
            Season = ParseInt(startLine.Fields.ElementAtOrDefault(2));
            Bracket = SanitizeBracket(startLine.Fields.ElementAtOrDefault(3));
            IsRanked = string.Equals(startLine.Fields.ElementAtOrDefault(4), "1", StringComparison.OrdinalIgnoreCase);
        }

        public string Bracket { get; private set; } = "unknown";

        public int DurationSeconds { get; private set; }

        public DateTimeOffset? EndedAt { get; private set; }

        public bool IsRanked { get; }

        public List<ParsedCombatLogLine> Lines { get; } = [];

        public int MapId { get; }

        public string MapName { get; private set; } = ArenaMapCatalog.GetMapName(0);

        public string? PlayerGuid { get; private set; }

        public string? PlayerName { get; private set; }

        public string? PlayerClassName { get; private set; }

        public string? PlayerSpecLabel { get; private set; }

        public string? ResultForPlayer { get; private set; }

        public int Season { get; }

        public DateTimeOffset StartedAt { get; }

        public int? WinningTeamId { get; private set; }

        public void AddLine(ParsedCombatLogLine line)
        {
            Lines.Add(line);
            CaptureNames(line);
            CaptureCombatantInfo(line);

            if (line.EventName == "ARENA_MATCH_END")
            {
                EndedAt = line.Timestamp ?? StartedAt;
                WinningTeamId = ParseNullableInt(line.Fields.ElementAtOrDefault(1));
            }
        }

        public void FinalizeFromLines()
        {
            MapName = ArenaMapCatalog.GetMapName(MapId);

            if (EndedAt is null)
            {
                EndedAt = Lines.LastOrDefault()?.Timestamp ?? StartedAt;
            }

            DurationSeconds = (int)Math.Max(0, Math.Round(((EndedAt ?? StartedAt) - StartedAt).TotalSeconds));

            foreach (var combatant in _combatants)
            {
                if (_guidToName.TryGetValue(combatant.Guid, out var fullName))
                {
                    combatant.FullName = fullName;
                }
            }

            var candidatePlayer = _combatants
                .OrderByDescending(combatant => combatant.TeamId)
                .ThenByDescending(combatant => combatant.PersonalRating)
                .FirstOrDefault();

            PlayerGuid = candidatePlayer?.Guid;
            PlayerName = candidatePlayer?.GetDisplayName();
            PlayerClassName = candidatePlayer?.ClassName;
            PlayerSpecLabel = candidatePlayer?.SpecLabel;

            if (candidatePlayer is not null && WinningTeamId.HasValue)
            {
                ResultForPlayer = candidatePlayer.TeamId == WinningTeamId.Value ? "win" : "loss";
            }
        }

        public void ApplyInferredPlayerProfiles(WowKnowledgeService wowKnowledgeService)
        {
            var structured = MatchMetricsCalculator.CalculateStructured(Lines, StartedAt, DurationSeconds);

            foreach (var combatant in _combatants)
            {
                if (!structured.PlayerSpellNamesBySourceGuid.TryGetValue(combatant.Guid, out var spellNames) || spellNames.Count == 0)
                {
                    continue;
                }

                var inferred = wowKnowledgeService.InferPlayerProfile(spellNames, combatant.SpecLabel);
                if (!string.IsNullOrWhiteSpace(inferred.ClassName))
                {
                    combatant.ClassName = inferred.ClassName;
                }

                if (!string.IsNullOrWhiteSpace(inferred.SpecLabel))
                {
                    combatant.SpecLabel = inferred.SpecLabel;
                }

                combatant.ProfileSnapshot = inferred.Snapshot;
            }

            var trackedPlayer = _combatants.FirstOrDefault(item => string.Equals(item.Guid, PlayerGuid, StringComparison.OrdinalIgnoreCase));
            if (trackedPlayer is not null)
            {
                PlayerClassName = trackedPlayer.ClassName;
                PlayerSpecLabel = trackedPlayer.SpecLabel;
            }
        }

        public object BuildJsonMatchObject()
        {
            var playerObjects = _combatants.Select(combatant => new
            {
                guid = combatant.Guid,
                name = combatant.GetDisplayName(),
                realm = combatant.GetRealm(),
                region = combatant.GetRegion(),
                teamId = combatant.TeamId,
                classId = combatant.ClassId,
                className = combatant.ClassName,
                specId = combatant.SpecId,
                specLabel = combatant.SpecLabel,
                personalRating = combatant.PersonalRating,
                highestPvpTier = combatant.HighestPvpTier,
                profileSnapshot = combatant.ProfileSnapshot is null
                    ? null
                    : new
                    {
                        className = combatant.ProfileSnapshot.ClassName,
                        specLabel = combatant.ProfileSnapshot.SpecLabel,
                        recognizedSpellCount = combatant.ProfileSnapshot.RecognizedSpellCount,
                        coreSpellUsageCount = combatant.ProfileSnapshot.CoreSpellUsageCount,
                        burstSpellUsageCount = combatant.ProfileSnapshot.BurstSpellUsageCount,
                        defensiveSpellUsageCount = combatant.ProfileSnapshot.DefensiveSpellUsageCount,
                        controlSpellUsageCount = combatant.ProfileSnapshot.ControlSpellUsageCount,
                        interruptSpellUsageCount = combatant.ProfileSnapshot.InterruptSpellUsageCount,
                        mobilitySpellUsageCount = combatant.ProfileSnapshot.MobilitySpellUsageCount
                    }
            }).ToList();

            var metrics = MatchMetricsCalculator.Calculate(Lines, StartedAt);

            return new
            {
                schemaVersion = "1.0",
                generatedAt = DateTimeOffset.UtcNow,
                match = new
                {
                    matchId = string.Empty,
                    mapId = MapId,
                    mapName = MapName,
                    season = Season,
                    bracket = Bracket,
                    matchType = IsRanked ? "rated" : "skirmish",
                    isRanked = IsRanked,
                    startedAt = StartedAt,
                    endedAt = EndedAt,
                    durationSeconds = DurationSeconds,
                    winningTeamId = WinningTeamId,
                    playerGuid = PlayerGuid,
                    playerName = PlayerName,
                    resultForPlayer = ResultForPlayer
                },
                players = playerObjects,
                metrics
            };
        }

        public IReadOnlyList<CombatantRecord> GetCombatants() => _combatants;

        private void CaptureCombatantInfo(ParsedCombatLogLine line)
        {
            if (!string.Equals(line.EventName, "COMBATANT_INFO", StringComparison.Ordinal))
            {
                return;
            }

            if (line.Fields.Count < 6)
            {
                return;
            }

            var guid = line.Fields[1];
            if (_combatants.Any(existing => string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var personalRating = ParseFromEnd(line.Fields, 2);
            var highestPvpTier = ParseFromEnd(line.Fields, 1);

            _combatants.Add(new CombatantRecord
            {
                Guid = guid,
                TeamId = ParseInt(line.Fields[2]),
                ClassId = 0,
                ClassName = null,
                SpecId = 0,
                SpecLabel = "Unknown Spec",
                PersonalRating = personalRating,
                HighestPvpTier = highestPvpTier
            });
        }

        private void CaptureNames(ParsedCombatLogLine line)
        {
            TryRememberName(line.Fields.ElementAtOrDefault(1), line.Fields.ElementAtOrDefault(2));
            TryRememberName(line.Fields.ElementAtOrDefault(5), line.Fields.ElementAtOrDefault(6));
        }

        private void TryRememberName(string? guid, string? rawName)
        {
            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(rawName) || rawName == "nil")
            {
                return;
            }

            _guidToName[guid] = rawName.Trim('"');
        }

        private static int ParseFromEnd(IReadOnlyList<string> fields, int offsetFromEnd)
        {
            var index = fields.Count - offsetFromEnd;
            return index >= 0 ? ParseInt(fields[index]) : 0;
        }

        private static int ParseInt(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;

        private static int? ParseNullableInt(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

        private static string SanitizeBracket(string? rawBracket) =>
            string.IsNullOrWhiteSpace(rawBracket) ? "unknown" : rawBracket.Trim('"').ToLowerInvariant();

        public static MatchCandidate Create(ParsedCombatLogLine line) => new(line);
    }

    private sealed class CombatantRecord
    {
        public int ClassId { get; set; }

        public string? ClassName { get; set; }

        public string? FullName { get; set; }

        public string Guid { get; set; } = string.Empty;

        public int HighestPvpTier { get; set; }

        public int PersonalRating { get; set; }

        public int SpecId { get; set; }

        public string SpecLabel { get; set; } = "Unknown Spec";

        public int TeamId { get; set; }

        public SpecPerformanceSnapshotItem? ProfileSnapshot { get; set; }

        public string GetDisplayName()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                return Guid;
            }

            return FullName.Split('-')[0];
        }

        public string? GetRealm()
        {
            if (string.IsNullOrWhiteSpace(FullName) || !FullName.Contains('-', StringComparison.Ordinal))
            {
                return null;
            }

            return FullName.Split('-').Skip(1).FirstOrDefault();
        }

        public string? GetRegion()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                return null;
            }

            return FullName.Split('-').LastOrDefault();
        }
    }

    private sealed record ParsedCombatLogLine(
        int LineNumber,
        string SourceFile,
        string RawLine,
        DateTimeOffset? Timestamp,
        bool IsTimestampParsed,
        string EventName,
        IReadOnlyList<string> Fields);

    private static class CombatLogLineParser
    {
        private static readonly string[] TimestampSeparators = ["  "];

        public static ParsedCombatLogLine? TryParse(string rawLine, int lineNumber, string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                return null;
            }

            var parts = rawLine.Split(TimestampSeparators, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return new ParsedCombatLogLine(
                    lineNumber,
                    sourceFile,
                    rawLine,
                    null,
                    false,
                    "UNKNOWN",
                    [rawLine]);
            }

            var timestampText = parts[0];
            var fields = SplitCsv(parts[1]);
            var eventName = fields.Count > 0 ? fields[0] : "UNKNOWN";

            return new ParsedCombatLogLine(
                lineNumber,
                sourceFile,
                rawLine,
                TryParseTimestamp(timestampText),
                TryParseTimestamp(timestampText) is not null,
                eventName,
                fields);
        }

        private static DateTimeOffset? TryParseTimestamp(string timestampText)
        {
            return DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed)
                ? parsed
                : null;
        }

        private static IReadOnlyList<string> SplitCsv(string input)
        {
            var values = new List<string>();
            var builder = new StringBuilder();
            var inQuotes = false;
            var bracketDepth = 0;
            var parenthesisDepth = 0;

            foreach (var character in input)
            {
                switch (character)
                {
                    case '"':
                        inQuotes = !inQuotes;
                        builder.Append(character);
                        break;
                    case '[' when !inQuotes:
                        bracketDepth++;
                        builder.Append(character);
                        break;
                    case ']' when !inQuotes && bracketDepth > 0:
                        bracketDepth--;
                        builder.Append(character);
                        break;
                    case '(' when !inQuotes:
                        parenthesisDepth++;
                        builder.Append(character);
                        break;
                    case ')' when !inQuotes && parenthesisDepth > 0:
                        parenthesisDepth--;
                        builder.Append(character);
                        break;
                    case ',' when !inQuotes && bracketDepth == 0 && parenthesisDepth == 0:
                        values.Add(builder.ToString());
                        builder.Clear();
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            values.Add(builder.ToString());
            return values;
        }
    }

    private static class ArenaMapCatalog
    {
        private static readonly Dictionary<int, string> Maps = new()
        {
            [980] = "Tol'viron Arena",
            [1552] = "Ashamane's Fall",
            [1825] = "Hook Point",
            [2167] = "The Robodrome"
        };

        public static string GetMapName(int mapId) =>
            Maps.TryGetValue(mapId, out var name) ? name : "Unknown Arena";
    }

    private static class MatchMetricsCalculator
    {
        public static object Calculate(IReadOnlyList<ParsedCombatLogLine> lines, DateTimeOffset startedAt)
        {
            var structured = CalculateStructured(lines, startedAt, null);

            return new
            {
                summary = new
                {
                    totalCasts = structured.Summary.TotalCasts,
                    totalDamage = structured.Summary.TotalDamage,
                    totalHealing = structured.Summary.TotalHealing,
                    interruptCount = structured.Summary.InterruptCount,
                    deathCount = structured.Summary.DeathCount
                },
                casts = structured.SpellMetrics
                    .Where(item => item.CastCount > 0)
                    .OrderByDescending(item => item.CastCount)
                    .Take(15)
                    .Select(item => new { spell = item.SpellName, count = item.CastCount })
                    .ToList(),
                damageDone = structured.SpellMetrics
                    .Where(item => item.TotalDamage > 0)
                    .OrderByDescending(item => item.TotalDamage)
                    .Take(15)
                    .Select(item => new { spell = item.SpellName, amount = item.TotalDamage })
                    .ToList(),
                healingDone = structured.SpellMetrics
                    .Where(item => item.TotalHealing > 0)
                    .OrderByDescending(item => item.TotalHealing)
                    .Take(15)
                    .Select(item => new { spell = item.SpellName, amount = item.TotalHealing })
                    .ToList(),
                interrupts = structured.Interrupts,
                deaths = structured.Deaths
            };
        }

        public static StructuredMetricsResult CalculateStructured(
            IReadOnlyList<ParsedCombatLogLine> lines,
            DateTimeOffset startedAt,
            int? durationSeconds)
        {
            var casts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var damage = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var healing = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var playerSpellNamesBySourceGuid = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var interrupts = new List<object>();
            var deaths = new List<object>();

            foreach (var line in lines)
            {
                TrackPlayerSpell(playerSpellNamesBySourceGuid, line);

                if (string.Equals(line.EventName, "SPELL_CAST_SUCCESS", StringComparison.Ordinal))
                {
                    var key = line.Fields.ElementAtOrDefault(10)?.Trim('"') ?? "Unknown Spell";
                    casts[key] = casts.TryGetValue(key, out var count) ? count + 1 : 1;
                }

                if (line.EventName.Contains("DAMAGE", StringComparison.Ordinal))
                {
                    var spellName = line.Fields.ElementAtOrDefault(10)?.Trim('"') ?? line.EventName;
                    var amount = ParseTrailingInt(line.Fields);
                    damage[spellName] = damage.TryGetValue(spellName, out var current) ? current + amount : amount;
                }

                if (line.EventName.Contains("HEAL", StringComparison.Ordinal))
                {
                    var spellName = line.Fields.ElementAtOrDefault(10)?.Trim('"') ?? line.EventName;
                    var amount = ParseTrailingInt(line.Fields);
                    healing[spellName] = healing.TryGetValue(spellName, out var current) ? current + amount : amount;
                }

                if (string.Equals(line.EventName, "SPELL_INTERRUPT", StringComparison.Ordinal))
                {
                    interrupts.Add(new
                    {
                        timestamp = ToRelativeSeconds(line.Timestamp, startedAt),
                        source = line.Fields.ElementAtOrDefault(2)?.Trim('"'),
                        target = line.Fields.ElementAtOrDefault(6)?.Trim('"'),
                        spell = line.Fields.ElementAtOrDefault(10)?.Trim('"')
                    });
                }

                if (string.Equals(line.EventName, "UNIT_DIED", StringComparison.Ordinal))
                {
                    deaths.Add(new
                    {
                        timestamp = ToRelativeSeconds(line.Timestamp, startedAt),
                        target = line.Fields.ElementAtOrDefault(6)?.Trim('"') ?? line.Fields.ElementAtOrDefault(2)?.Trim('"')
                    });
                }
            }

            var allSpellNames = casts.Keys
                .Concat(damage.Keys)
                .Concat(healing.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var effectiveDuration = Math.Max(1, durationSeconds ?? ToRelativeSeconds(lines.LastOrDefault()?.Timestamp, startedAt));

            return new StructuredMetricsResult(
                new StructuredMetricSummary(
                    casts.Values.Sum(),
                    damage.Values.Sum(),
                    healing.Values.Sum(),
                    interrupts.Count,
                    deaths.Count,
                    Math.Round(damage.Values.Sum() / (double)effectiveDuration, 2),
                    Math.Round(healing.Values.Sum() / (double)effectiveDuration, 2),
                    Math.Round(casts.Values.Sum() / Math.Max(1d, effectiveDuration / 60d), 2)),
                allSpellNames
                    .Select(spellName => new StructuredSpellMetric(
                        spellName,
                        casts.TryGetValue(spellName, out var castCount) ? castCount : 0,
                        damage.TryGetValue(spellName, out var totalDamage) ? totalDamage : 0,
                        healing.TryGetValue(spellName, out var totalHealing) ? totalHealing : 0))
                    .OrderByDescending(item => item.TotalDamage + item.TotalHealing)
                    .ThenByDescending(item => item.CastCount)
                    .ToList(),
                playerSpellNamesBySourceGuid,
                interrupts,
                deaths);
        }

        private static void TrackPlayerSpell(
            IDictionary<string, List<string>> playerSpellNamesBySourceGuid,
            ParsedCombatLogLine line)
        {
            var sourceGuid = line.Fields.ElementAtOrDefault(1)?.Trim('"');
            var spellName = line.Fields.ElementAtOrDefault(10)?.Trim('"');
            if (string.IsNullOrWhiteSpace(sourceGuid) || string.IsNullOrWhiteSpace(spellName))
            {
                return;
            }

            if (!line.EventName.Contains("SPELL_", StringComparison.Ordinal) &&
                !line.EventName.Contains("RANGE_", StringComparison.Ordinal))
            {
                return;
            }

            if (!playerSpellNamesBySourceGuid.TryGetValue(sourceGuid, out var spellNames))
            {
                spellNames = [];
                playerSpellNamesBySourceGuid[sourceGuid] = spellNames;
            }

            spellNames.Add(spellName);
        }

        private static int ToRelativeSeconds(DateTimeOffset? timestamp, DateTimeOffset startedAt)
        {
            if (timestamp is null)
            {
                return 0;
            }

            return (int)Math.Max(0, Math.Round((timestamp.Value - startedAt).TotalSeconds));
        }

        private static long ParseTrailingInt(IReadOnlyList<string> fields)
        {
            foreach (var field in fields.Reverse())
            {
                if (long.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }
    }

    private sealed record StructuredMetricsResult(
        StructuredMetricSummary Summary,
        IReadOnlyList<StructuredSpellMetric> SpellMetrics,
        IReadOnlyDictionary<string, List<string>> PlayerSpellNamesBySourceGuid,
        IReadOnlyList<object> Interrupts,
        IReadOnlyList<object> Deaths);

    private sealed record StructuredMetricSummary(
        int TotalCasts,
        long TotalDamage,
        long TotalHealing,
        int InterruptCount,
        int DeathCount,
        double DamagePerSecond,
        double HealingPerSecond,
        double CastsPerMinute);

    private sealed record StructuredSpellMetric(
        string SpellName,
        int CastCount,
        long TotalDamage,
        long TotalHealing);

    private static class MatchJsonBuilder
    {
        public static object Build(MatchCandidate candidate, string matchId, string sourceFilePath, string chunkPath)
        {
            var baseObject = candidate.BuildJsonMatchObject();
            var metricsProperty = baseObject.GetType().GetProperty("metrics")?.GetValue(baseObject);
            var playersProperty = baseObject.GetType().GetProperty("players")?.GetValue(baseObject);
            var matchProperty = baseObject.GetType().GetProperty("match")?.GetValue(baseObject);

            return new
            {
                schemaVersion = "1.0",
                generatedAt = DateTimeOffset.UtcNow,
                app = new
                {
                    name = "ArenaGodEyes",
                    version = "0.1.0"
                },
                source = new
                {
                    combatLogFile = sourceFilePath,
                    chunkFile = chunkPath,
                    lineStart = candidate.Lines.FirstOrDefault()?.LineNumber ?? 0,
                    lineEnd = candidate.Lines.LastOrDefault()?.LineNumber ?? 0,
                    parseErrorCount = candidate.Lines.Count(line => !line.IsTimestampParsed),
                    parseErrors = candidate.Lines
                        .Where(line => !line.IsTimestampParsed)
                        .Select(line => new { line.LineNumber, line.RawLine })
                        .ToList()
                },
                match = WithMatchId(matchProperty, matchId),
                teams = candidate.GetCombatants()
                    .GroupBy(combatant => combatant.TeamId)
                    .Select(group => new
                    {
                        teamId = group.Key,
                        players = group.Select(combatant => combatant.Guid).ToList(),
                        averageRating = group.Any() ? (int)group.Average(combatant => combatant.PersonalRating) : 0
                    })
                    .ToList(),
                players = playersProperty,
                metrics = metricsProperty,
                keyMoments = Array.Empty<object>(),
                coachFindings = Array.Empty<object>(),
                video = new
                {
                    enabled = false,
                    localPath = (string?)null,
                    thumbnailPath = (string?)null,
                    durationSeconds = 0,
                    markers = Array.Empty<object>()
                },
                manualAnalysis = new
                {
                    chatGptPromptPath = (string?)null,
                    chatGptResponsePath = (string?)null,
                    importedAt = (DateTimeOffset?)null,
                    summary = (string?)null,
                    findings = Array.Empty<object>()
                },
                aiReadyPayload = new
                {
                    shortSummary = $"Arena match on {candidate.MapName} ({candidate.Bracket}) lasting {candidate.DurationSeconds} seconds.",
                    recommendedPromptVersion = "manual-chatgpt-v1",
                    payload = new
                    {
                        matchId,
                        bracket = candidate.Bracket,
                        mapName = candidate.MapName
                    }
                }
            };
        }

        private static object WithMatchId(object? matchProperty, string matchId)
        {
            if (matchProperty is null)
            {
                return new { matchId };
            }

            var source = matchProperty.GetType();
            return new
            {
                matchId,
                mapId = source.GetProperty("mapId")?.GetValue(matchProperty),
                mapName = source.GetProperty("mapName")?.GetValue(matchProperty),
                season = source.GetProperty("season")?.GetValue(matchProperty),
                bracket = source.GetProperty("bracket")?.GetValue(matchProperty),
                matchType = source.GetProperty("matchType")?.GetValue(matchProperty),
                isRanked = source.GetProperty("isRanked")?.GetValue(matchProperty),
                startedAt = source.GetProperty("startedAt")?.GetValue(matchProperty),
                endedAt = source.GetProperty("endedAt")?.GetValue(matchProperty),
                durationSeconds = source.GetProperty("durationSeconds")?.GetValue(matchProperty),
                winningTeamId = source.GetProperty("winningTeamId")?.GetValue(matchProperty),
                playerGuid = source.GetProperty("playerGuid")?.GetValue(matchProperty),
                playerName = source.GetProperty("playerName")?.GetValue(matchProperty),
                resultForPlayer = source.GetProperty("resultForPlayer")?.GetValue(matchProperty)
            };
        }
    }
}
