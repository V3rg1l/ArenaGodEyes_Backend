namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record GeneratedVideoClip(
    int VideoSecond,
    int StartSecond,
    int EndSecond,
    string Label,
    string Category,
    string Source,
    string ClipPath,
    DateTimeOffset CreatedAt);
