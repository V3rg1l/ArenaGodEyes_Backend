using System.Threading.Channels;
using ArenaGodEyes.Core.Application.CombatLog.Abstractions;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class MatchLogWatcher : IMatchLogWatcher, IDisposable
{
    private readonly ICombatLogEventSink _combatLogEventSink;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<MatchLogWatcher> _logger;
    private readonly MatchLogWatcherOptions _options;
    private readonly IAppSettingsService _settingsService;
    private readonly CombatLogTailReader _tailReader;
    private readonly Channel<string> _changeQueue = Channel.CreateUnbounded<string>();
    private FileSystemWatcher? _fileSystemWatcher;

    public MatchLogWatcher(
        ICombatLogEventSink combatLogEventSink,
        IFileSystem fileSystem,
        ILogger<MatchLogWatcher> logger,
        MatchLogWatcherOptions options,
        IAppSettingsService settingsService,
        CombatLogTailReader tailReader)
    {
        _combatLogEventSink = combatLogEventSink;
        _fileSystem = fileSystem;
        _logger = logger;
        _options = options;
        _settingsService = settingsService;
        _tailReader = tailReader;
    }

    public MatchLogWatcherState State { get; } = new();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        var watchedDirectory = settings.CombatLogDirectory;

        if (string.IsNullOrWhiteSpace(watchedDirectory))
        {
            _logger.LogInformation("Combat log watcher is idle because no combat log directory is configured.");
            return;
        }

        if (!_fileSystem.DirectoryExists(watchedDirectory))
        {
            _logger.LogInformation("Combat log watcher is idle because directory was not found: {Directory}", watchedDirectory);
            return;
        }

        State.WatchedDirectory = watchedDirectory;
        State.IsWatching = true;

        using var timer = new PeriodicTimer(_options.IdlePollInterval);
        InitializeWatcher(watchedDirectory);
        await EnqueueActiveFileAsync(watchedDirectory, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_changeQueue.Reader.TryRead(out var changedPath))
                {
                    await ProcessPathAsync(watchedDirectory, changedPath, cancellationToken);
                }

                var ticked = await timer.WaitForNextTickAsync(cancellationToken);
                if (!ticked)
                {
                    break;
                }

                await EnqueueActiveFileAsync(watchedDirectory, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            State.IsWatching = false;
            DisposeWatcher();
        }
    }

    public void Dispose()
    {
        DisposeWatcher();
    }

    private async Task EnqueueActiveFileAsync(string watchedDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var activeFile = GetActiveCombatLogFile(watchedDirectory);
        if (activeFile is null)
        {
            return;
        }

        if (!string.Equals(State.ActiveSourceFile, activeFile.Path, StringComparison.OrdinalIgnoreCase))
        {
            State.ActiveSourceFile = activeFile.Path;
            State.ActiveSourceDetectedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("New combat log source selected: {Path}", activeFile.Path);
            await _combatLogEventSink.OnNewCombatLogFileDetectedAsync(
                new NewCombatLogFileDetected(activeFile.Path, DateTimeOffset.UtcNow),
                cancellationToken);
        }

        await _changeQueue.Writer.WriteAsync(activeFile.Path, cancellationToken);
    }

    private WatchedCombatLogFile? GetActiveCombatLogFile(string watchedDirectory)
    {
        var candidates = new List<WatchedCombatLogFile>();

        foreach (var pattern in _options.FilePatterns)
        {
            foreach (var path in _fileSystem.EnumerateFiles(watchedDirectory, pattern, SearchOption.TopDirectoryOnly))
            {
                var fileInfo = new FileInfo(path);
                candidates.Add(new WatchedCombatLogFile(path, fileInfo.LastWriteTimeUtc, fileInfo.Length));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.LastWriteTimeUtc)
            .ThenByDescending(candidate => candidate.Length)
            .FirstOrDefault();
    }

    private void InitializeWatcher(string watchedDirectory)
    {
        _fileSystemWatcher = new FileSystemWatcher(watchedDirectory)
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _fileSystemWatcher.Created += OnWatcherActivity;
        _fileSystemWatcher.Changed += OnWatcherActivity;
        _fileSystemWatcher.Renamed += OnWatcherRenamed;

        _logger.LogInformation("Combat log watcher started for directory: {Directory}", watchedDirectory);
    }

    private async Task ProcessPathAsync(string watchedDirectory, string changedPath, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsKnownCombatLogPath(changedPath))
            {
                await EnqueueActiveFileAsync(watchedDirectory, cancellationToken);
                return;
            }

            var lines = await _tailReader.ReadNewLinesAsync(
                changedPath,
                startFromEnd: _options.ResumeFromEndOnStartup,
                cancellationToken);

            if (lines.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Read {LineCount} new combat log lines from {Path}", lines.Count, changedPath);

            foreach (var line in lines)
            {
                State.TotalLinesRead++;
                await _combatLogEventSink.OnCombatLogLineReadAsync(
                    new CombatLogLineRead(line.SourceFile, line.LineNumber, line.RawLine),
                    cancellationToken);
            }
        }
        catch (IOException exception)
        {
            State.LastError = exception.Message;
            _logger.LogWarning(exception, "Combat log read error for {Path}", changedPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            State.LastError = exception.Message;
            _logger.LogWarning(exception, "Combat log access error for {Path}", changedPath);
        }
    }

    private bool IsKnownCombatLogPath(string path)
    {
        var fileName = Path.GetFileName(path);
        return _options.FilePatterns.Any(pattern => MatchesPattern(fileName, pattern));
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (string.Equals(pattern, fileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!pattern.Contains('*'))
        {
            return false;
        }

        var segments = pattern.Split('*');
        var prefix = segments[0];
        var suffix = segments[^1];

        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private void OnWatcherActivity(object sender, FileSystemEventArgs eventArgs)
    {
        _changeQueue.Writer.TryWrite(eventArgs.FullPath);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs eventArgs)
    {
        _changeQueue.Writer.TryWrite(eventArgs.FullPath);
    }

    private void DisposeWatcher()
    {
        if (_fileSystemWatcher is null)
        {
            return;
        }

        _fileSystemWatcher.EnableRaisingEvents = false;
        _fileSystemWatcher.Created -= OnWatcherActivity;
        _fileSystemWatcher.Changed -= OnWatcherActivity;
        _fileSystemWatcher.Renamed -= OnWatcherRenamed;
        _fileSystemWatcher.Dispose();
        _fileSystemWatcher = null;
    }
}
