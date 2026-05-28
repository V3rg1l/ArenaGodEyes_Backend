namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class CoachSkillEntity
{
    public int Id { get; set; }

    public string Scope { get; set; } = "spec";

    public string? SpecLabel { get; set; }

    public string Area { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public string? Drill { get; set; }

    public string Source { get; set; } = string.Empty;

    public int EvidenceCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
