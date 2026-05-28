namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class CoachKnowledgeParameterEntity
{
    public int Id { get; set; }

    public string Scope { get; set; } = "spec";

    public string? SpecLabel { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Metric { get; set; } = string.Empty;

    public string? TargetValue { get; set; }

    public string? Unit { get; set; }

    public string? Note { get; set; }

    public string Source { get; set; } = string.Empty;

    public int EvidenceCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
