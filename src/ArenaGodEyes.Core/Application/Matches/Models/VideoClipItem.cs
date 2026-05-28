namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record VideoClipItem(
    int VideoSecond,
    int StartSecond,
    int EndSecond,
    string Label,
    string Category,
    string Source,
    string ClipPath,
    DateTimeOffset CreatedAt);
