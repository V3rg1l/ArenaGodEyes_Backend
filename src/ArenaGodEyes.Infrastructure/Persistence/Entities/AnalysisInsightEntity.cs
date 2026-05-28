namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class AnalysisInsightEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int? VideoSecond { get; set; }

    public string Category { get; set; } = "analysis";

    public string Severity { get; set; } = "medium";

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? Evidence { get; set; }

    public string? Recommendation { get; set; }

    public string Source { get; set; } = "manual_chatgpt";

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity? Match { get; set; }
}
