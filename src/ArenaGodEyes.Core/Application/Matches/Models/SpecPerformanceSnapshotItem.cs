namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record SpecPerformanceSnapshotItem(
    string? ClassName,
    string? SpecLabel,
    int RecognizedSpellCount,
    int CoreSpellUsageCount,
    int BurstSpellUsageCount,
    int DefensiveSpellUsageCount,
    int ControlSpellUsageCount,
    int InterruptSpellUsageCount,
    int MobilitySpellUsageCount);
