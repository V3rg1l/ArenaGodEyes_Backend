using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArenaGodEyes.Core.Application.Matches.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Video.Abstractions;
using ArenaGodEyes.Core.Application.Video.Models;
using ArenaGodEyes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArenaGodEyes.Infrastructure.Video;

public sealed class VideoWorkflowService : IVideoWorkflowService
{
    private readonly ArenaGodEyesDbContext _dbContext;
    private readonly IAppSettingsService _appSettingsService;
    private readonly LocalDataPaths _localDataPaths;
    private readonly ILogger<VideoWorkflowService> _logger;
    private readonly IMatchLibraryService _matchLibraryService;
    private readonly ObsLocalConfigurationReader _obsLocalConfigurationReader;

    public VideoWorkflowService(
        ArenaGodEyesDbContext dbContext,
        IAppSettingsService appSettingsService,
        LocalDataPaths localDataPaths,
        ILogger<VideoWorkflowService> logger,
        IMatchLibraryService matchLibraryService,
        ObsLocalConfigurationReader obsLocalConfigurationReader)
    {
        _dbContext = dbContext;
        _appSettingsService = appSettingsService;
        _localDataPaths = localDataPaths;
        _logger = logger;
        _matchLibraryService = matchLibraryService;
        _obsLocalConfigurationReader = obsLocalConfigurationReader;
    }

    public async Task<ObsConnectionStatus> GetObsStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        if (!IsObsConfigured(settings))
        {
            return new ObsConnectionStatus(false, false, false, false, null, null, "OBS is not configured.");
        }

        try
        {
            await using var session = await ObsWebSocketSession.ConnectAsync(settings, cancellationToken);
            var versionData = await session.RequestAsync("GetVersion", null, cancellationToken);
            var recordStatusData = await session.RequestAsync("GetRecordStatus", null, cancellationToken);

            return new ObsConnectionStatus(
                true,
                true,
                true,
                recordStatusData?["outputActive"]?.GetValue<bool>() ?? false,
                versionData?["obsVersion"]?.GetValue<string>(),
                recordStatusData?["outputPath"]?.GetValue<string>(),
                null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to query OBS status.");
            return new ObsConnectionStatus(true, false, false, false, null, null, BuildObsStatusErrorMessage(exception));
        }
    }

    public Task<ObsConnectionStatus> TestObsConnectionAsync(CancellationToken cancellationToken = default) =>
        GetObsStatusAsync(cancellationToken);

    public async Task<ObsSceneSetupResult> EnsureWowSceneAsync(
        string windowTitle,
        string executableName,
        string? windowClassName,
        string captureMode,
        bool captureCursor,
        string? sceneName,
        string? sourceName,
        CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        if (!IsObsConfigured(settings))
        {
            return new ObsSceneSetupResult(false, false, null, null, null, "OBS is not configured.");
        }

        var resolvedSceneName = string.IsNullOrWhiteSpace(sceneName) ? "ArenaGodEyes Scene" : sceneName.Trim();
        var resolvedSourceName = string.IsNullOrWhiteSpace(sourceName) ? "ArenaGodEyes WoW Capture" : sourceName.Trim();

        try
        {
            await using var session = await ObsWebSocketSession.ConnectAsync(settings, cancellationToken);
            await EnsureSceneExistsAsync(session, resolvedSceneName, cancellationToken);

            var windowDescriptor = BuildObsWindowDescriptor(windowTitle, windowClassName, executableName);
            var inputKind = ResolveObsInputKind(captureMode);
            var inputSettings = BuildObsInputSettings(inputKind, windowDescriptor, captureCursor);

            var inputNames = await GetInputNamesAsync(session, cancellationToken);
            if (inputNames.Contains(resolvedSourceName, StringComparer.OrdinalIgnoreCase))
            {
                await session.RequestAsync("SetInputSettings", new
                {
                    inputName = resolvedSourceName,
                    inputSettings,
                    overlay = true
                }, cancellationToken);
            }
            else
            {
                await session.RequestAsync("CreateInput", new
                {
                    sceneName = resolvedSceneName,
                    inputName = resolvedSourceName,
                    inputKind,
                    inputSettings,
                    sceneItemEnabled = true
                }, cancellationToken);
            }

            await session.RequestAsync("SetCurrentProgramScene", new
            {
                sceneName = resolvedSceneName
            }, cancellationToken);

            return new ObsSceneSetupResult(
                true,
                true,
                resolvedSceneName,
                resolvedSourceName,
                windowDescriptor,
                null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to ensure OBS WoW scene.");
            return new ObsSceneSetupResult(
                false,
                false,
                resolvedSceneName,
                resolvedSourceName,
                null,
                exception.Message);
        }
    }

    public async Task<ObsRecordingStartResult> StartRecordingAsync(string? matchId, CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        if (!IsObsConfigured(settings))
        {
            return new ObsRecordingStartResult(false, false, matchId, "OBS is not configured.");
        }

        try
        {
            await using var session = await ObsWebSocketSession.ConnectAsync(settings, cancellationToken);
            var status = await session.RequestAsync("GetRecordStatus", null, cancellationToken);
            if (status?["outputActive"]?.GetValue<bool>() == true)
            {
                return new ObsRecordingStartResult(true, true, matchId, "OBS was already recording.");
            }

            await session.RequestAsync("StartRecord", null, cancellationToken);

            if (!string.IsNullOrWhiteSpace(matchId))
            {
                await MarkMatchRecordingStateAsync(matchId, "recording", cancellationToken);
            }

            return new ObsRecordingStartResult(true, false, matchId, "OBS recording started.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to start OBS recording.");
            return new ObsRecordingStartResult(false, false, matchId, exception.Message);
        }
    }

    public async Task<ObsRecordingStopResult> StopRecordingAsync(string? matchId, CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        if (!IsObsConfigured(settings))
        {
            return new ObsRecordingStopResult(false, matchId, null, null, false, "OBS is not configured.");
        }

        try
        {
            await using var session = await ObsWebSocketSession.ConnectAsync(settings, cancellationToken);
            var status = await session.RequestAsync("GetRecordStatus", null, cancellationToken);
            if (status?["outputActive"]?.GetValue<bool>() != true)
            {
                return new ObsRecordingStopResult(true, matchId, null, null, false, "OBS was not recording.");
            }

            var stopData = await session.RequestAsync("StopRecord", null, cancellationToken);
            var outputPath = stopData?["outputPath"]?.GetValue<string>()
                ?? status?["outputPath"]?.GetValue<string>();
            var finalVideoPath = outputPath;
            var attachedToMatch = false;

            if (!string.IsNullOrWhiteSpace(matchId))
            {
                await MarkMatchRecordingStateAsync(matchId, "recorded", cancellationToken);

                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    finalVideoPath = await MoveObsOutputIntoWorkspaceAsync(matchId, outputPath, cancellationToken);
                    attachedToMatch = await AttachAndProcessVideoAsync(matchId, finalVideoPath, cancellationToken);
                }
            }

            return new ObsRecordingStopResult(
                true,
                matchId,
                outputPath,
                finalVideoPath,
                attachedToMatch,
                "OBS recording stopped.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to stop OBS recording.");
            return new ObsRecordingStopResult(false, matchId, null, null, false, exception.Message);
        }
    }

    public async Task<VideoProcessingResult?> ProcessMatchVideoAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null || string.IsNullOrWhiteSpace(match.VideoLocalPath) || !File.Exists(match.VideoLocalPath))
        {
            return null;
        }

        var metadata = await ReadVideoMetadataAsync(settings, match.VideoLocalPath, cancellationToken);
        var thumbnailPath = await TryGenerateThumbnailAsync(
            settings,
            matchId,
            match.VideoLocalPath,
            metadata?.DurationSeconds,
            cancellationToken);

        var result = new VideoProcessingResult(
            matchId,
            match.VideoLocalPath,
            thumbnailPath,
            metadata?.DurationSeconds,
            metadata?.FileSizeBytes,
            metadata?.FramesPerSecond,
            metadata?.Codec,
            metadata?.Resolution,
            DateTimeOffset.UtcNow);

        var updated = await _matchLibraryService.UpdateVideoProcessingAsync(matchId, result, cancellationToken);
        return updated ? result : null;
    }

    public async Task<VideoClipGenerationResult?> GenerateReviewClipsAsync(
        string matchId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken);
        var match = await _dbContext.Matches
            .Include(item => item.TimelineMarkers)
            .Include(item => item.AnalysisInsights)
            .Include(item => item.ValidationTargets)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);

        if (match is null || string.IsNullOrWhiteSpace(match.VideoLocalPath) || !File.Exists(match.VideoLocalPath))
        {
            return null;
        }

        var executablePath = string.IsNullOrWhiteSpace(settings.FfmpegExecutablePath)
            ? "ffmpeg"
            : settings.FfmpegExecutablePath;

        var clipDirectory = Path.Combine(_localDataPaths.ExportsPath, "clips", matchId);
        Directory.CreateDirectory(clipDirectory);

        var clipTargets = BuildClipTargets(match);
        var generatedClips = new List<GeneratedVideoClip>();

        foreach (var target in clipTargets)
        {
            var clipPath = await TryGenerateClipAsync(
                executablePath,
                match.VideoLocalPath,
                clipDirectory,
                target,
                match.VideoDurationSeconds,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(clipPath))
            {
                continue;
            }

            generatedClips.Add(new GeneratedVideoClip(
                target.VideoSecond,
                target.StartSecond,
                target.EndSecond,
                target.Label,
                target.Category,
                target.Source,
                clipPath,
                DateTimeOffset.UtcNow));
        }

        await _matchLibraryService.ReplaceVideoClipsAsync(matchId, generatedClips, cancellationToken);
        return new VideoClipGenerationResult(matchId, generatedClips.Count, generatedClips);
    }

    private async Task<bool> AttachAndProcessVideoAsync(string matchId, string? videoPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            return false;
        }

        var attached = await _matchLibraryService.AttachVideoAsync(matchId, videoPath, cancellationToken);
        if (!attached)
        {
            return false;
        }

        await ProcessMatchVideoAsync(matchId, cancellationToken);
        return true;
    }

    private async Task MarkMatchRecordingStateAsync(
        string matchId,
        string recordingStatus,
        CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match is null)
        {
            return;
        }

        match.RecordingProvider = "obs_websocket";
        match.RecordingStatus = recordingStatus;
        match.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.Equals(recordingStatus, "recording", StringComparison.OrdinalIgnoreCase))
        {
            match.RecordingStartedAt = DateTimeOffset.UtcNow;
        }

        if (string.Equals(recordingStatus, "recorded", StringComparison.OrdinalIgnoreCase))
        {
            match.RecordingStoppedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> MoveObsOutputIntoWorkspaceAsync(
        string matchId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        var match = await _dbContext.Matches.SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var bracket = string.IsNullOrWhiteSpace(match?.Bracket) ? "unknown" : match!.Bracket.ToLowerInvariant();
        var destinationDirectory = Path.Combine(_localDataPaths.VideosPath, bracket);
        Directory.CreateDirectory(destinationDirectory);

        var extension = Path.GetExtension(outputPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp4";
        }

        var destinationPath = Path.Combine(destinationDirectory, $"{matchId}{extension}");
        if (string.Equals(outputPath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return outputPath;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(outputPath, destinationPath);
        return destinationPath;
    }

    private static bool IsObsConfigured(Core.Application.Settings.Models.AppSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ObsHost) && settings.ObsPort > 0;

    private string BuildObsStatusErrorMessage(Exception exception)
    {
        var localConfiguration = _obsLocalConfigurationReader.Read();
        if (localConfiguration.Exists && !localConfiguration.ServerEnabled)
        {
            return "OBS WebSocket server is disabled in the local OBS config. Enable it in OBS WebSocket settings.";
        }

        return exception.Message;
    }

    private static string ResolveObsInputKind(string captureMode) =>
        captureMode.Trim().ToLowerInvariant() switch
        {
            "game" => "game_capture",
            "monitor" => "monitor_capture",
            _ => "window_capture"
        };

    private static object BuildObsInputSettings(string inputKind, string windowDescriptor, bool captureCursor)
    {
        if (string.Equals(inputKind, "game_capture", StringComparison.Ordinal))
        {
            return new
            {
                capture_mode = "window",
                window = windowDescriptor,
                capture_cursor = captureCursor
            };
        }

        if (string.Equals(inputKind, "monitor_capture", StringComparison.Ordinal))
        {
            return new
            {
                capture_cursor = captureCursor
            };
        }

        return new
        {
            window = windowDescriptor,
            cursor = captureCursor
        };
    }

    private static string BuildObsWindowDescriptor(string windowTitle, string? windowClassName, string executableName)
    {
        var safeTitle = string.IsNullOrWhiteSpace(windowTitle) ? "World of Warcraft" : windowTitle.Trim();
        var safeClass = string.IsNullOrWhiteSpace(windowClassName) ? "GxWindowClass" : windowClassName.Trim();
        var safeExecutable = string.IsNullOrWhiteSpace(executableName) ? "Wow.exe" : executableName.Trim();
        return $"{safeTitle}:{safeClass}:{safeExecutable}";
    }

    private static async Task EnsureSceneExistsAsync(
        ObsWebSocketSession session,
        string sceneName,
        CancellationToken cancellationToken)
    {
        var sceneListData = await session.RequestAsync("GetSceneList", null, cancellationToken);
        var sceneNames = sceneListData?["scenes"]?.AsArray()
            .Select(item => item?["sceneName"]?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList()
            ?? [];

        if (!sceneNames.Contains(sceneName, StringComparer.OrdinalIgnoreCase))
        {
            await session.RequestAsync("CreateScene", new
            {
                sceneName
            }, cancellationToken);
        }
    }

    private static async Task<List<string>> GetInputNamesAsync(
        ObsWebSocketSession session,
        CancellationToken cancellationToken)
    {
        var inputListData = await session.RequestAsync("GetInputList", null, cancellationToken);
        return inputListData?["inputs"]?.AsArray()
            .Select(item => item?["inputName"]?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList()
            ?? [];
    }

    private static List<ClipTarget> BuildClipTargets(Persistence.Entities.MatchRecordEntity match)
    {
        var targets = new List<ClipTarget>();

        targets.AddRange(match.TimelineMarkers
            .Select(marker => new ClipTarget(
                marker.VideoSecond,
                marker.Label,
                marker.Category,
                marker.Source)));

        targets.AddRange(match.AnalysisInsights
            .Where(insight => insight.VideoSecond.HasValue)
            .Select(insight => new ClipTarget(
                insight.VideoSecond!.Value,
                insight.Title,
                insight.Category,
                insight.Source)));

        targets.AddRange(match.ValidationTargets
            .Where(target => target.VideoSecond.HasValue)
            .Select(target => new ClipTarget(
                target.VideoSecond!.Value,
                target.Metric,
                target.Category,
                target.Source)));

        return targets
            .Where(target => target.VideoSecond >= 0)
            .GroupBy(target => new { target.VideoSecond, target.Label, target.Category, target.Source })
            .Select(group =>
            {
                var videoSecond = group.Key.VideoSecond;
                var startSecond = Math.Max(0, videoSecond - 8);
                var endSecond = videoSecond + 8;
                return new ClipTarget(
                    videoSecond,
                    group.Key.Label,
                    group.Key.Category,
                    group.Key.Source,
                    startSecond,
                    endSecond);
            })
            .OrderBy(target => target.VideoSecond)
            .Take(24)
            .ToList();
    }

    private async Task<string?> TryGenerateClipAsync(
        string executablePath,
        string videoPath,
        string clipDirectory,
        ClipTarget target,
        double? videoDurationSeconds,
        CancellationToken cancellationToken)
    {
        var boundedEnd = videoDurationSeconds.HasValue
            ? Math.Min(target.EndSecond, (int)Math.Ceiling(videoDurationSeconds.Value))
            : target.EndSecond;
        var duration = Math.Max(1, boundedEnd - target.StartSecond);
        var safeLabel = SanitizeFileName(target.Label);
        var clipPath = Path.Combine(
            clipDirectory,
            $"{target.VideoSecond:D4}-{safeLabel}.mp4");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(target.StartSecond.ToString());
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(duration.ToString());
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add(clipPath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var standardError = await standardErrorTask;
                _logger.LogWarning(
                    "ffmpeg clip generation failed for {VideoPath} at second {VideoSecond}. ExitCode={ExitCode}. Error={Error}",
                    videoPath,
                    target.VideoSecond,
                    process.ExitCode,
                    standardError);
                return null;
            }

            return File.Exists(clipPath) ? clipPath : null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to generate clip for {VideoPath} at second {VideoSecond}",
                videoPath,
                target.VideoSecond);
            return null;
        }
    }

    private async Task<VideoProbeMetadata?> ReadVideoMetadataAsync(
        Core.Application.Settings.Models.AppSettings settings,
        string videoPath,
        CancellationToken cancellationToken)
    {
        var executablePath = string.IsNullOrWhiteSpace(settings.FfprobeExecutablePath)
            ? "ffprobe"
            : settings.FfprobeExecutablePath;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(videoPath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
            {
                _logger.LogWarning(
                    "ffprobe failed for {VideoPath}. ExitCode={ExitCode}. Error={Error}",
                    videoPath,
                    process.ExitCode,
                    standardError);
                return null;
            }

            var document = JsonNode.Parse(standardOutput);
            var formatNode = document?["format"];
            var videoStream = document?["streams"]?.AsArray()
                .FirstOrDefault(item => string.Equals(item?["codec_type"]?.GetValue<string>(), "video", StringComparison.OrdinalIgnoreCase));

            double? durationSeconds = null;
            if (double.TryParse(
                    formatNode?["duration"]?.GetValue<string>(),
                    out var parsedDuration))
            {
                durationSeconds = parsedDuration;
            }

            long? fileSizeBytes = null;
            if (long.TryParse(
                    formatNode?["size"]?.GetValue<string>(),
                    out var parsedSize))
            {
                fileSizeBytes = parsedSize;
            }

            return new VideoProbeMetadata(
                durationSeconds,
                fileSizeBytes,
                ParseFrameRate(videoStream?["avg_frame_rate"]?.GetValue<string>()),
                videoStream?["codec_name"]?.GetValue<string>(),
                BuildResolution(videoStream));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to inspect video metadata for {VideoPath}", videoPath);
            return null;
        }
    }

    private async Task<string?> TryGenerateThumbnailAsync(
        Core.Application.Settings.Models.AppSettings settings,
        string matchId,
        string videoPath,
        double? durationSeconds,
        CancellationToken cancellationToken)
    {
        var executablePath = string.IsNullOrWhiteSpace(settings.FfmpegExecutablePath)
            ? "ffmpeg"
            : settings.FfmpegExecutablePath;

        var thumbnailPath = Path.Combine(_localDataPaths.ThumbnailsPath, $"{matchId}.jpg");
        Directory.CreateDirectory(_localDataPaths.ThumbnailsPath);

        var captureSecond = settings.VideoThumbnailSecond > 0 ? settings.VideoThumbnailSecond : 5;
        if (durationSeconds.HasValue && durationSeconds.Value > 0)
        {
            captureSecond = Math.Min(captureSecond, Math.Max(1, (int)Math.Floor(durationSeconds.Value / 2d)));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(captureSecond.ToString());
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add(thumbnailPath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var standardError = await standardErrorTask;
                _logger.LogWarning(
                    "ffmpeg thumbnail generation failed for {VideoPath}. ExitCode={ExitCode}. Error={Error}",
                    videoPath,
                    process.ExitCode,
                    standardError);
                return null;
            }

            return File.Exists(thumbnailPath) ? thumbnailPath : null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to generate thumbnail for {VideoPath}", videoPath);
            return null;
        }
    }

    private static double? ParseFrameRate(string? rateText)
    {
        if (string.IsNullOrWhiteSpace(rateText))
        {
            return null;
        }

        var parts = rateText.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var numerator) &&
            double.TryParse(parts[1], out var denominator) &&
            denominator != 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(rateText, out var parsed) ? parsed : null;
    }

    private static string? BuildResolution(JsonNode? streamNode)
    {
        var width = streamNode?["width"]?.GetValue<int?>();
        var height = streamNode?["height"]?.GetValue<int?>();
        return width.HasValue && height.HasValue ? $"{width}x{height}" : null;
    }

    private static string SanitizeFileName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "clip";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(text
            .ToLowerInvariant()
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        sanitized = string.Join(
            "-",
            sanitized.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? "clip" : sanitized;
    }

    private sealed record VideoProbeMetadata(
        double? DurationSeconds,
        long? FileSizeBytes,
        double? FramesPerSecond,
        string? Codec,
        string? Resolution);

    private sealed record ClipTarget(
        int VideoSecond,
        string Label,
        string Category,
        string Source,
        int StartSecond = 0,
        int EndSecond = 0);

    private sealed class ObsWebSocketSession : IAsyncDisposable
    {
        private readonly ClientWebSocket _client;

        private ObsWebSocketSession(ClientWebSocket client)
        {
            _client = client;
        }

        public static async Task<ObsWebSocketSession> ConnectAsync(
            Core.Application.Settings.Models.AppSettings settings,
            CancellationToken cancellationToken)
        {
            var client = new ClientWebSocket();
            client.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.ObsConnectTimeoutSeconds)));

            await client.ConnectAsync(BuildObsUri(settings), linkedCts.Token);
            var session = new ObsWebSocketSession(client);

            var helloPayload = await session.ReceivePayloadAsync(0, linkedCts.Token);
            var authNode = helloPayload?["authentication"];
            var authResponse = BuildAuthenticationResponse(settings.ObsPassword, authNode);

            await session.SendAsync(new
            {
                op = 1,
                d = new
                {
                    rpcVersion = helloPayload?["rpcVersion"]?.GetValue<int>() ?? 1,
                    eventSubscriptions = 0,
                    authentication = authResponse
                }
            }, linkedCts.Token);

            _ = await session.ReceivePayloadAsync(2, linkedCts.Token);
            return session;
        }

        public async Task<JsonNode?> RequestAsync(
            string requestType,
            object? requestData,
            CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString("N");
            await SendAsync(new
            {
                op = 6,
                d = new
                {
                    requestType,
                    requestId,
                    requestData
                }
            }, cancellationToken);

            while (true)
            {
                var root = await ReceiveRootAsync(cancellationToken);
                if (root?["op"]?.GetValue<int>() != 7)
                {
                    continue;
                }

                var payload = root["d"];
                if (!string.Equals(payload?["requestId"]?.GetValue<string>(), requestId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (payload?["requestStatus"]?["result"]?.GetValue<bool>() != true)
                {
                    var comment = payload?["requestStatus"]?["comment"]?.GetValue<string>() ?? "OBS request failed.";
                    throw new InvalidOperationException(comment);
                }

                return payload?["responseData"];
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_client.State == WebSocketState.Open)
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }

            _client.Dispose();
        }

        private async Task<JsonNode?> ReceivePayloadAsync(int expectedOp, CancellationToken cancellationToken)
        {
            while (true)
            {
                var root = await ReceiveRootAsync(cancellationToken);
                if (root?["op"]?.GetValue<int>() == expectedOp)
                {
                    return root["d"];
                }
            }
        }

        private async Task<JsonNode?> ReceiveRootAsync(CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[16 * 1024]);
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await _client.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("OBS closed the WebSocket connection.");
                }

                stream.Write(buffer.Array!, 0, result.Count);
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return JsonNode.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        }

        private async Task SendAsync(object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _client.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        private static Uri BuildObsUri(Core.Application.Settings.Models.AppSettings settings)
        {
            if (settings.ObsHost.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                settings.ObsHost.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(settings.ObsHost);
            }

            return new Uri($"ws://{settings.ObsHost}:{settings.ObsPort}");
        }

        private static string? BuildAuthenticationResponse(string? password, JsonNode? authNode)
        {
            var challenge = authNode?["challenge"]?.GetValue<string>();
            var salt = authNode?["salt"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(challenge) || string.IsNullOrWhiteSpace(salt))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("OBS requires a password but none is configured.");
            }

            var secret = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            var authBytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge));
            return Convert.ToBase64String(authBytes);
        }
    }
}
