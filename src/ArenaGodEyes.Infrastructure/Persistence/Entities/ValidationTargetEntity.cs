namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class ValidationTargetEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int? VideoSecond { get; set; }

    public string Category { get; set; } = "validation";

    public string Metric { get; set; } = string.Empty;

    public string? CurrentValue { get; set; }

    public string? ExpectedValue { get; set; }

    public string? Unit { get; set; }

    public string? Note { get; set; }

    public string Source { get; set; } = "manual_chatgpt";

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity? Match { get; set; }
}
