namespace ArenaGodEyes.ApiLocal.Contracts;

public sealed record SystemStatusResponse(
    string Name,
    string Version,
    string Tagline,
    string Status,
    string Safety,
    DateTimeOffset UtcNow);
