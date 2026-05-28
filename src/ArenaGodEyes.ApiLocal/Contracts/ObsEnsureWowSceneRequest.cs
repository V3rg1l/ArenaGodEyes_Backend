namespace ArenaGodEyes.ApiLocal.Contracts;

public sealed record ObsEnsureWowSceneRequest(
    string WindowTitle,
    string ExecutableName,
    string? WindowClassName,
    string CaptureMode,
    bool CaptureCursor,
    string? SceneName,
    string? SourceName);
