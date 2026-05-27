namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class TimelineMarkerEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int VideoSecond { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = "note";

    public string Severity { get; set; } = "medium";

    public string Source { get; set; } = "manual_chatgpt";

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity? Match { get; set; }
}
