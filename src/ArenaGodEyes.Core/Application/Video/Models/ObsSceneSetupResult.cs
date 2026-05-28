namespace ArenaGodEyes.Core.Application.Video.Models;

public sealed record ObsSceneSetupResult(
    bool IsObsReachable,
    bool SceneReady,
    string? SceneName,
    string? SourceName,
    string? MatchedWindow,
    string? ErrorMessage);
