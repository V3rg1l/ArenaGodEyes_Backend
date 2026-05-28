using System.Text.Json;
using ArenaGodEyes.Core.Application.Matches.Models;
using ArenaGodEyes.Infrastructure.Settings;

namespace ArenaGodEyes.Infrastructure.Matches;

public sealed class WowKnowledgeService
{
    private readonly Lazy<WowKnowledgeIndex> _index;

    public WowKnowledgeService(WorkspacePaths workspacePaths)
    {
        _index = new Lazy<WowKnowledgeIndex>(() => LoadIndex(workspacePaths.WorkspaceRootPath));
    }

    public EnrichedSpellProfile EnrichSpell(
        string spellName,
        string? preferredClassName,
        string? preferredSpecLabel)
    {
        var normalized = Normalize(spellName);
        var entries = _index.Value.GetEntries(normalized);
        if (entries.Count == 0)
        {
            return new EnrichedSpellProfile(spellName, normalized, null, null, null, null, false);
        }

        var best = entries
            .OrderByDescending(entry => Score(entry, preferredClassName, preferredSpecLabel))
            .ThenByDescending(entry => entry.SpecLabel is not null)
            .First();

        return new EnrichedSpellProfile(
            spellName,
            normalized,
            best.ClassName,
            best.SpecLabel,
            best.PrimaryCategory,
            best.TacticalPhase,
            best.IsSignatureSpell);
    }

    public InferredPlayerProfile InferPlayerProfile(
        IReadOnlyList<string> playerSpellNames,
        string? existingSpecLabel)
    {
        if (playerSpellNames.Count == 0)
        {
            return new InferredPlayerProfile(null, existingSpecLabel, null);
        }

        var classScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var specScores = new Dictionary<(string ClassName, string SpecLabel), int>();

        foreach (var spellName in playerSpellNames)
        {
            var normalized = Normalize(spellName);
            foreach (var entry in _index.Value.GetEntries(normalized))
            {
                classScores[entry.ClassName] = classScores.TryGetValue(entry.ClassName, out var classScore)
                    ? classScore + 1 + (entry.IsSignatureSpell ? 2 : 0)
                    : 1 + (entry.IsSignatureSpell ? 2 : 0);

                if (entry.SpecLabel is null)
                {
                    continue;
                }

                var key = (entry.ClassName, entry.SpecLabel);
                var extra = entry.IsSignatureSpell ? 4 : 2;
                specScores[key] = specScores.TryGetValue(key, out var specScore)
                    ? specScore + extra
                    : extra;
            }
        }

        var bestSpec = specScores
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key.ClassName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var bestClass = classScores
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        var inferredClassName = bestSpec.Key.ClassName ?? bestClass.Key;
        var inferredSpecLabel = !string.IsNullOrWhiteSpace(existingSpecLabel) &&
                                !string.Equals(existingSpecLabel, "Unknown Spec", StringComparison.OrdinalIgnoreCase)
            ? existingSpecLabel
            : bestSpec.Key.SpecLabel;

        var snapshot = BuildSnapshot(playerSpellNames, inferredClassName, inferredSpecLabel);
        return new InferredPlayerProfile(inferredClassName, inferredSpecLabel, snapshot);
    }

    private SpecPerformanceSnapshotItem BuildSnapshot(
        IReadOnlyList<string> playerSpellNames,
        string? inferredClassName,
        string? inferredSpecLabel)
    {
        var recognizedSpellCount = 0;
        var core = 0;
        var burst = 0;
        var defensive = 0;
        var control = 0;
        var interrupt = 0;
        var mobility = 0;

        foreach (var spellName in playerSpellNames)
        {
            var enriched = EnrichSpell(spellName, inferredClassName, inferredSpecLabel);
            if (enriched.ClassName is null)
            {
                continue;
            }

            recognizedSpellCount += 1;

            switch (enriched.TacticalPhase)
            {
                case "core":
                    core += 1;
                    break;
                case "burst":
                case "cooldowns":
                    burst += 1;
                    break;
                case "defensives":
                    defensive += 1;
                    break;
                case "cc":
                case "utility":
                    control += 1;
                    break;
                case "interrupts":
                    interrupt += 1;
                    break;
                case "mobility":
                    mobility += 1;
                    break;
            }

            switch (enriched.PrimaryCategory)
            {
                case "interrupt":
                    interrupt += 1;
                    break;
                case "defensive":
                    defensive += 1;
                    break;
                case "stun":
                case "fear":
                case "silence":
                case "disorient":
                case "incapacitate":
                case "horror":
                case "root":
                case "cc":
                    control += 1;
                    break;
                case "mobility":
                    mobility += 1;
                    break;
            }
        }

        return new SpecPerformanceSnapshotItem(
            inferredClassName,
            inferredSpecLabel,
            recognizedSpellCount,
            core,
            burst,
            defensive,
            control,
            interrupt,
            mobility);
    }

    private static int Score(SpellKnowledgeEntry entry, string? preferredClassName, string? preferredSpecLabel)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(preferredClassName) &&
            string.Equals(entry.ClassName, preferredClassName, StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        if (!string.IsNullOrWhiteSpace(preferredSpecLabel) &&
            string.Equals(entry.SpecLabel, preferredSpecLabel, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (entry.IsSignatureSpell)
        {
            score += 2;
        }

        return score;
    }

    private static WowKnowledgeIndex LoadIndex(string workspaceRootPath)
    {
        var bundlePath = Path.Combine(
            workspaceRootPath,
            "ArenaGodEyes.Docs",
            "src",
            "WoWInfo",
            "Json",
            "wow-knowledge-bundle.json");

        if (!File.Exists(bundlePath))
        {
            return new WowKnowledgeIndex(new Dictionary<string, List<SpellKnowledgeEntry>>(StringComparer.OrdinalIgnoreCase));
        }

        using var stream = File.OpenRead(bundlePath);
        using var document = JsonDocument.Parse(stream);
        var entriesBySpell = new Dictionary<string, List<SpellKnowledgeEntry>>(StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty("interrupts", out var interrupts))
        {
            LoadNamedEntries(entriesBySpell, interrupts, "interrupt");
        }

        if (document.RootElement.TryGetProperty("defensives", out var defensives))
        {
            LoadNamedEntries(entriesBySpell, defensives, "defensive");
        }

        if (document.RootElement.TryGetProperty("crowdControl", out var crowdControl))
        {
            LoadNamedEntries(entriesBySpell, crowdControl, null);
        }

        if (document.RootElement.TryGetProperty("healingReduction", out var healingReduction))
        {
            LoadNamedEntries(entriesBySpell, healingReduction, "heal_reduction");
        }

        if (!document.RootElement.TryGetProperty("spellKnowledge", out var spellKnowledge))
        {
            return new WowKnowledgeIndex(entriesBySpell);
        }

        foreach (var classNode in spellKnowledge.EnumerateObject())
        {
            var className = classNode.Name;

            foreach (var classOrSpecNode in classNode.Value.EnumerateObject())
            {
                if (classOrSpecNode.NameEquals("classSpells"))
                {
                    foreach (var groupNode in classOrSpecNode.Value.EnumerateObject())
                    {
                        foreach (var spellElement in groupNode.Value.EnumerateArray())
                        {
                            AddEntry(
                                entriesBySpell,
                                spellElement.GetString(),
                                className,
                                null,
                                null,
                                groupNode.Name,
                                false);
                        }
                    }

                    continue;
                }

                var specLabel = classOrSpecNode.Name;
                foreach (var specGroupNode in classOrSpecNode.Value.EnumerateObject())
                {
                    if (specGroupNode.NameEquals("role"))
                    {
                        continue;
                    }

                    foreach (var spellElement in specGroupNode.Value.EnumerateArray())
                    {
                        AddEntry(
                            entriesBySpell,
                            spellElement.GetString(),
                            className,
                            specLabel,
                            null,
                            specGroupNode.Name,
                            true);
                    }
                }
            }
        }

        return new WowKnowledgeIndex(entriesBySpell);
    }

    private static void LoadNamedEntries(
        IDictionary<string, List<SpellKnowledgeEntry>> entriesBySpell,
        JsonElement rootElement,
        string? fallbackCategory)
    {
        foreach (var classNode in rootElement.EnumerateObject())
        {
            var className = classNode.Name;
            if (classNode.Value.ValueKind == JsonValueKind.Array)
            {
                LoadNamedEntryArray(entriesBySpell, className, null, classNode.Value, fallbackCategory);
                continue;
            }

            if (classNode.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var specNode in classNode.Value.EnumerateObject())
            {
                if (specNode.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                LoadNamedEntryArray(entriesBySpell, className, specNode.Name, specNode.Value, fallbackCategory);
            }
        }
    }

    private static void LoadNamedEntryArray(
        IDictionary<string, List<SpellKnowledgeEntry>> entriesBySpell,
        string className,
        string? specLabelFromGroup,
        JsonElement arrayElement,
        string? fallbackCategory)
    {
        foreach (var entry in arrayElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            var spellName = nameElement.GetString();
            var category = entry.TryGetProperty("category", out var categoryElement)
                ? categoryElement.GetString()
                : fallbackCategory;
            var specs = entry.TryGetProperty("specs", out var specsElement) && specsElement.ValueKind == JsonValueKind.Array
                ? specsElement.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList()
                : [];

            if (specs.Count > 0)
            {
                foreach (var specLabel in specs)
                {
                    AddEntry(entriesBySpell, spellName, className, specLabel, category, null, true);
                }

                continue;
            }

            AddEntry(
                entriesBySpell,
                spellName,
                className,
                specLabelFromGroup,
                category,
                null,
                !string.IsNullOrWhiteSpace(specLabelFromGroup));
        }
    }

    private static void AddEntry(
        IDictionary<string, List<SpellKnowledgeEntry>> entriesBySpell,
        string? spellName,
        string className,
        string? specLabel,
        string? primaryCategory,
        string? tacticalPhase,
        bool isSignatureSpell)
    {
        if (string.IsNullOrWhiteSpace(spellName))
        {
            return;
        }

        var normalized = Normalize(spellName);
        if (!entriesBySpell.TryGetValue(normalized, out var list))
        {
            list = [];
            entriesBySpell[normalized] = list;
        }

        var existing = list.FirstOrDefault(entry =>
            string.Equals(entry.ClassName, className, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.SpecLabel, specLabel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.PrimaryCategory, primaryCategory, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.TacticalPhase, tacticalPhase, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return;
        }

        list.Add(new SpellKnowledgeEntry(
            spellName,
            className,
            specLabel,
            primaryCategory,
            tacticalPhase,
            isSignatureSpell));
    }

    private static string Normalize(string spellName)
    {
        return spellName.Trim().Trim('"').ToLowerInvariant();
    }

    public sealed record InferredPlayerProfile(
        string? ClassName,
        string? SpecLabel,
        SpecPerformanceSnapshotItem? Snapshot);

    public sealed record EnrichedSpellProfile(
        string SpellName,
        string NormalizedSpellName,
        string? ClassName,
        string? SpecLabel,
        string? PrimaryCategory,
        string? TacticalPhase,
        bool IsSignatureSpell);

    private sealed record SpellKnowledgeEntry(
        string SpellName,
        string ClassName,
        string? SpecLabel,
        string? PrimaryCategory,
        string? TacticalPhase,
        bool IsSignatureSpell);

    private sealed class WowKnowledgeIndex
    {
        private readonly IReadOnlyDictionary<string, List<SpellKnowledgeEntry>> _entriesBySpell;

        public WowKnowledgeIndex(IReadOnlyDictionary<string, List<SpellKnowledgeEntry>> entriesBySpell)
        {
            _entriesBySpell = entriesBySpell;
        }

        public IReadOnlyList<SpellKnowledgeEntry> GetEntries(string normalizedSpellName)
        {
            return _entriesBySpell.TryGetValue(normalizedSpellName, out var entries)
                ? entries
                : [];
        }
    }
}
