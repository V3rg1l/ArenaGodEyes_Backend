namespace ArenaGodEyes.Core.Application.Settings.Models;

public sealed class StorageOverview
{
    public string RecordingDirectory { get; set; } = string.Empty;

    public string RecordingCacheDirectory { get; set; } = string.Empty;

    public string DefaultRecordingDirectory { get; set; } = string.Empty;

    public string DefaultRecordingCacheDirectory { get; set; } = string.Empty;

    public long RecordingDirectoryBytes { get; set; }

    public long RecordingCacheDirectoryBytes { get; set; }

    public long TotalBytes { get; set; }

    public int RecordingFileCount { get; set; }

    public int CacheFileCount { get; set; }

    public double TotalGigabytes { get; set; }

    public int MaxDiskStorageGb { get; set; }

    public double? UsagePercent { get; set; }

    public bool RecordingDirectoryExists { get; set; }

    public bool RecordingCacheDirectoryExists { get; set; }
}
