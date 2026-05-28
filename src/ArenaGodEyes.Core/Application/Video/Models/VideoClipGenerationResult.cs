namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record VideoClipGenerationResult(
    string MatchId,
    int GeneratedClipCount,
    IReadOnlyList<GeneratedVideoClip> Clips);
