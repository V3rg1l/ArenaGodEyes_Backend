namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record ObsConnectionStatus(
    bool IsConfigured,
    bool IsReachable,
    bool IsAuthenticated,
    bool IsRecording,
    string? ObsVersion,
    string? OutputPath,
    string? ErrorMessage);
