namespace ArenaGodEyes.Infrastructure.Persistence;

public sealed class LocalDataPaths
{
    public LocalDataPaths(string workspaceRootPath)
    {
        RootPath = Path.Combine(workspaceRootPath, "ArenaGodEyesData");
        ChunksPath = Path.Combine(RootPath, "chunks");
        MatchesPath = Path.Combine(RootPath, "matches");
        VideosPath = Path.Combine(RootPath, "videos");
        ThumbnailsPath = Path.Combine(RootPath, "thumbnails");
        PromptsPath = Path.Combine(RootPath, "prompts");
        AiResponsesPath = Path.Combine(RootPath, "ai-responses");
        ExportsPath = Path.Combine(RootPath, "exports");
        DatabasePath = Path.Combine(RootPath, "arenagodeyes.db");
    }

    public string RootPath { get; }

    public string ChunksPath { get; }

    public string MatchesPath { get; }

    public string VideosPath { get; }

    public string ThumbnailsPath { get; }

    public string PromptsPath { get; }

    public string AiResponsesPath { get; }

    public string ExportsPath { get; }

    public string DatabasePath { get; }

    public IReadOnlyList<string> AllDirectories =>
    [
        RootPath,
        ChunksPath,
        MatchesPath,
        VideosPath,
        ThumbnailsPath,
        PromptsPath,
        AiResponsesPath,
        ExportsPath
    ];
}
