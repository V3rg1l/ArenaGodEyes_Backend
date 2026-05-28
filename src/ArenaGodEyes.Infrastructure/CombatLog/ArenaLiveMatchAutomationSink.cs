using ArenaGodEyes.Core.Application.CombatLog.Abstractions;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Core.Application.Video.Abstractions;
using ArenaGodEyes.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class ArenaLiveMatchAutomationSink : ICombatLogEventSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LocalDataPaths _localDataPaths;
    private readonly ILogger<ArenaLiveMatchAutomationSink> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private LiveArenaSession? _activeSession;

    public ArenaLiveMatchAutomationSink(
        LocalDataPaths localDataPaths,
        ILogger<ArenaLiveMatchAutomationSink> logger,
        IServiceScopeFactory scopeFactory)
    {
        _localDataPaths = localDataPaths;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task OnNewCombatLogFileDetectedAsync(
        NewCombatLogFileDetected @event,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("New combat log file detected: {Path} at {DetectedAt}", @event.Path, @event.DetectedAt);
        return Task.CompletedTask;
    }

    public async Task OnCombatLogLineReadAsync(
        CombatLogLineRead @event,
        CancellationToken cancellationToken = default)
    {
        var parsedLine = CombatLogEventParser.TryParse(@event.RawLine);
        if (parsedLine is null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await HandleLineAsync(@event, parsedLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandleLineAsync(
        CombatLogLineRead @event,
        CombatLogEventLine parsedLine,
        CancellationToken cancellationToken)
    {
        if (string.Equals(parsedLine.EventName, "ARENA_MATCH_START", StringComparison.Ordinal))
        {
            await StartSessionAsync(@event, parsedLine, cancellationToken);
            return;
        }

        if (_activeSession is null)
        {
            return;
        }

        if (!string.Equals(_activeSession.SourceFile, @event.SourceFile, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Ignoring combat log line from {SourceFile} because the active arena session belongs to {ActiveSourceFile}.",
                @event.SourceFile,
                _activeSession.SourceFile);
            return;
        }

        _activeSession.RawLines.Add(@event.RawLine);

        if (string.Equals(parsedLine.EventName, "ARENA_MATCH_END", StringComparison.Ordinal))
        {
            await CompleteSessionAsync(cancellationToken);
        }
    }

    private async Task StartSessionAsync(
        CombatLogLineRead @event,
        CombatLogEventLine parsedLine,
        CancellationToken cancellationToken)
    {
        if (_activeSession is not null)
        {
            _logger.LogWarning(
                "Arena match start was detected while another live session was still open. Resetting the previous session from {SourceFile}.",
                _activeSession.SourceFile);
        }

        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.EnableMatchDetection)
        {
            _activeSession = null;
            return;
        }

        var isRanked = string.Equals(parsedLine.Fields.ElementAtOrDefault(4), "1", StringComparison.OrdinalIgnoreCase);
        var bracket = SanitizeBracket(parsedLine.Fields.ElementAtOrDefault(3));
        var shouldTrack = isRanked || settings.TrackSkirmishMatches;

        _activeSession = new LiveArenaSession(
            @event.SourceFile,
            parsedLine.Timestamp ?? DateTimeOffset.UtcNow,
            bracket,
            isRanked,
            shouldTrack,
            [@event.RawLine]);

        _logger.LogInformation(
            "Arena match detected in {SourceFile}. Bracket: {Bracket}. Ranked: {IsRanked}. Tracking enabled: {ShouldTrack}.",
            @event.SourceFile,
            bracket,
            isRanked,
            shouldTrack);

        if (!shouldTrack || !settings.EnableRecording || !settings.EnableObsRecording)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var videoWorkflowService = scope.ServiceProvider.GetRequiredService<IVideoWorkflowService>();
            var result = await videoWorkflowService.StartRecordingAsync(null, cancellationToken);
            _activeSession.StartedObsAutomatically = result.Started && !result.WasAlreadyRecording;

            _logger.LogInformation(
                "OBS auto-start result for live arena session: Started={Started}, WasAlreadyRecording={WasAlreadyRecording}, Message={Message}",
                result.Started,
                result.WasAlreadyRecording,
                result.Message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "OBS auto-start failed for live arena session.");
        }
    }

    private async Task CompleteSessionAsync(CancellationToken cancellationToken)
    {
        if (_activeSession is null)
        {
            return;
        }

        var completedSession = _activeSession;
        _activeSession = null;

        if (!completedSession.ShouldTrack)
        {
            _logger.LogInformation(
                "Arena session from {SourceFile} finished but was skipped because match tracking is disabled for this bracket.",
                completedSession.SourceFile);
            return;
        }

        string? importedMatchId = null;

        try
        {
            var capturePath = await PersistLiveCaptureAsync(completedSession, cancellationToken);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var importOrchestrator = scope.ServiceProvider.GetRequiredService<IMatchImportOrchestrator>();
            var importResult = await importOrchestrator.ImportAsync(capturePath, cancellationToken);
            importedMatchId = importResult.Matches.LastOrDefault()?.MatchId;

            _logger.LogInformation(
                "Live arena session imported from {CapturePath}. Imported matches: {ImportedMatchCount}. MatchId: {MatchId}",
                capturePath,
                importResult.MatchCount,
                importedMatchId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to import completed live arena session from {SourceFile}.", completedSession.SourceFile);
        }

        if (!completedSession.StartedObsAutomatically)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var videoWorkflowService = scope.ServiceProvider.GetRequiredService<IVideoWorkflowService>();
            var stopResult = await videoWorkflowService.StopRecordingAsync(importedMatchId, cancellationToken);

            _logger.LogInformation(
                "OBS auto-stop result for live arena session: Stopped={Stopped}, MatchId={MatchId}, Attached={AttachedToMatch}, Message={Message}",
                stopResult.Stopped,
                importedMatchId,
                stopResult.AttachedToMatch,
                stopResult.Message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "OBS auto-stop failed for completed live arena session.");
        }
    }

    private async Task<string> PersistLiveCaptureAsync(LiveArenaSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_localDataPaths.ChunksPath);
        var safeBracket = string.IsNullOrWhiteSpace(session.Bracket) ? "unknown" : session.Bracket;
        var capturePath = Path.Combine(
            _localDataPaths.ChunksPath,
            $"live-{session.StartedAt:yyyyMMdd-HHmmss}-{safeBracket}.txt");

        await File.WriteAllLinesAsync(capturePath, session.RawLines, cancellationToken);
        return capturePath;
    }

    private async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
        return await settingsService.GetAsync(cancellationToken);
    }

    private static string SanitizeBracket(string? bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket))
        {
            return "unknown";
        }

        var normalized = bracket
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("\"", string.Empty);

        return normalized switch
        {
            "2v2" => "2v2",
            "3v3" => "3v3",
            "5v5" => "5v5",
            _ => normalized
        };
    }

    private sealed class LiveArenaSession
    {
        public LiveArenaSession(
            string sourceFile,
            DateTimeOffset startedAt,
            string bracket,
            bool isRanked,
            bool shouldTrack,
            List<string> rawLines)
        {
            SourceFile = sourceFile;
            StartedAt = startedAt;
            Bracket = bracket;
            IsRanked = isRanked;
            ShouldTrack = shouldTrack;
            RawLines = rawLines;
        }

        public string Bracket { get; }

        public bool IsRanked { get; }

        public List<string> RawLines { get; }

        public bool ShouldTrack { get; }

        public string SourceFile { get; }

        public bool StartedObsAutomatically { get; set; }

        public DateTimeOffset StartedAt { get; }
    }
}
