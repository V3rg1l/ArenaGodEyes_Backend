namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record VideoProcessingResult(
    string MatchId,
    string VideoPath,
    string? ThumbnailPath,
    double? DurationSeconds,
    long? FileSizeBytes,
    double? FramesPerSecond,
    string? Codec,
    string? Resolution,
    DateTimeOffset ProcessedAt);
