namespace ArenaGodEyes.Infrastructure.Persistence.Entities;

public sealed class MatchRecordEntity
{
    public int Id { get; set; }

    public string MatchId { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string Bracket { get; set; } = "unknown";

    public string MatchType { get; set; } = "unknown";

    public int MapId { get; set; }

    public string MapName { get; set; } = "Unknown Arena";

    public int DurationSeconds { get; set; }

    public int? WinningTeamId { get; set; }

    public string? PlayerGuid { get; set; }

    public string? PlayerName { get; set; }

    public string? PlayerClassName { get; set; }

    public string? PlayerSpecLabel { get; set; }

    public string? ResultForPlayer { get; set; }

    public string SourceCombatLogFile { get; set; } = string.Empty;

    public string ChunkFilePath { get; set; } = string.Empty;

    public string MatchJsonPath { get; set; } = string.Empty;

    public string? VideoLocalPath { get; set; }

    public string? ThumbnailPath { get; set; }

    public string? RecordingStatus { get; set; }

    public string? RecordingProvider { get; set; }

    public DateTimeOffset? RecordingStartedAt { get; set; }

    public DateTimeOffset? RecordingStoppedAt { get; set; }

    public double? VideoDurationSeconds { get; set; }

    public long? VideoFileSizeBytes { get; set; }

    public double? VideoFramesPerSecond { get; set; }

    public string? VideoCodec { get; set; }

    public string? VideoResolution { get; set; }

    public DateTimeOffset? LastVideoProcessedAt { get; set; }

    public bool HasManualAnalysis { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<TimelineMarkerEntity> TimelineMarkers { get; set; } = [];

    public List<ManualAnalysisEntity> ManualAnalyses { get; set; } = [];

    public List<AnalysisInsightEntity> AnalysisInsights { get; set; } = [];

    public List<ValidationTargetEntity> ValidationTargets { get; set; } = [];

    public List<VideoClipEntity> VideoClips { get; set; } = [];

    public MatchMetricSummaryEntity? MetricSummary { get; set; }

    public List<MatchSpellMetricEntity> SpellMetrics { get; set; } = [];
}
