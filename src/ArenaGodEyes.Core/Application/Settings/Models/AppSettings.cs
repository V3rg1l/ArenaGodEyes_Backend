namespace ArenaGodEyes.Core.Application.Settings.Models;

public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    public string? WowRetailPath { get; set; }

    public string? CombatLogDirectory { get; set; }

    public string? AddonDirectory { get; set; }

    public string? RecordingDirectory { get; set; }

    public string? RecordingCacheDirectory { get; set; }

    public string ObsHost { get; set; } = "127.0.0.1";

    public int ObsPort { get; set; } = 4455;

    public string? ObsPassword { get; set; }

    public bool EnableObsRecording { get; set; }

    public bool EnableObsAutoConnect { get; set; } = true;

    public int ObsConnectTimeoutSeconds { get; set; } = 5;

    public string? FfmpegExecutablePath { get; set; }

    public string? FfprobeExecutablePath { get; set; }

    public int VideoThumbnailSecond { get; set; } = 5;

    public int MaxDiskStorageGb { get; set; } = 100;

    public int MaxMatchFiles { get; set; } = 1000;

    public bool TrackSkirmishMatches { get; set; } = true;

    public bool EnableMatchDetection { get; set; } = true;

    public bool EnableRecording { get; set; } = false;

    public bool RunAtStartup { get; set; }

    public bool MinimizeToTrayOnClose { get; set; } = true;

    public bool ShowMmrBadge { get; set; } = true;

    public bool ShowOnlyMyMistakesByDefault { get; set; }

    public bool UseListViewForMatches { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
