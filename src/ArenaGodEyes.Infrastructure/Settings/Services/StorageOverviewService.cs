using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class StorageOverviewService : IStorageOverviewService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IFileSystem _fileSystem;

    public StorageOverviewService(IAppSettingsService appSettingsService, IFileSystem fileSystem)
    {
        _appSettingsService = appSettingsService;
        _fileSystem = fileSystem;
    }

    public async Task<StorageOverview> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var recordingDirectory = settings.RecordingDirectory ?? string.Empty;
        var recordingCacheDirectory = settings.RecordingCacheDirectory ?? string.Empty;

        var recordingDirectoryBytes = GetDirectoryBytes(recordingDirectory);
        var recordingCacheDirectoryBytes = GetDirectoryBytes(recordingCacheDirectory);
        var totalBytes = recordingDirectoryBytes + recordingCacheDirectoryBytes;
        var totalGigabytes = totalBytes / 1024d / 1024d / 1024d;

        return new StorageOverview
        {
            RecordingDirectory = recordingDirectory,
            RecordingCacheDirectory = recordingCacheDirectory,
            DefaultRecordingDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "ArenaGodEyes",
                "Recordings"),
            DefaultRecordingCacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ArenaGodEyes",
                "temp"),
            RecordingDirectoryBytes = recordingDirectoryBytes,
            RecordingCacheDirectoryBytes = recordingCacheDirectoryBytes,
            TotalBytes = totalBytes,
            RecordingFileCount = GetFileCount(recordingDirectory),
            CacheFileCount = GetFileCount(recordingCacheDirectory),
            TotalGigabytes = totalGigabytes,
            MaxDiskStorageGb = settings.MaxDiskStorageGb,
            UsagePercent = settings.MaxDiskStorageGb <= 0
                ? null
                : Math.Clamp((totalGigabytes / settings.MaxDiskStorageGb) * 100d, 0d, 100d),
            RecordingDirectoryExists = !string.IsNullOrWhiteSpace(recordingDirectory) && _fileSystem.DirectoryExists(recordingDirectory),
            RecordingCacheDirectoryExists = !string.IsNullOrWhiteSpace(recordingCacheDirectory) && _fileSystem.DirectoryExists(recordingCacheDirectory)
        };
    }

    private int GetFileCount(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.DirectoryExists(path))
        {
            return 0;
        }

        return _fileSystem.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
    }

    private long GetDirectoryBytes(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.DirectoryExists(path))
        {
            return 0;
        }

        long totalBytes = 0;

        foreach (var filePath in _fileSystem.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                totalBytes += new FileInfo(filePath).Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return totalBytes;
    }
}
