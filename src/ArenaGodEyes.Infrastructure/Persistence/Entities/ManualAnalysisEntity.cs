namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class ManualAnalysisEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public string Provider { get; set; } = "manual_chatgpt";

    public string PromptVersion { get; set; } = "manual-chatgpt-v1";

    public string PromptText { get; set; } = string.Empty;

    public string ResponseText { get; set; } = string.Empty;

    public string? ResponseJson { get; set; }

    public string? PromptPath { get; set; }

    public string? ResponsePath { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity? Match { get; set; }
}
