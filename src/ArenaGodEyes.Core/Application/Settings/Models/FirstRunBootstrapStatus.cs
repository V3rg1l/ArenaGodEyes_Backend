namespace ArenaGodEyes.Core.Application.Settings.Models;

public sealed record FirstRunBootstrapStatus(
    bool WowDetected,
    bool AddonInstalled,
    bool ObsInstalled,
    bool ObsWebSocketConfigured,
    bool RecordingDirectoriesReady,
    IReadOnlyList<string> AppliedFixes,
    IReadOnlyList<string> PendingActions);
