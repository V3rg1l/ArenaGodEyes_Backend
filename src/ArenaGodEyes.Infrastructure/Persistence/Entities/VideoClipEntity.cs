namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class VideoClipEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public int VideoSecond { get; set; }

    public int StartSecond { get; set; }

    public int EndSecond { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string ClipPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public int MatchRecordEntityId { get; set; }

    public MatchRecordEntity Match { get; set; } = null!;
}
