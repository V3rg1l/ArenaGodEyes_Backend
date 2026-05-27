namespace ArenaGodEyes.Infrastructure.FileSystem;

public interface IFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    void CreateDirectory(string path);

    string ReadAllText(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    void CopyFile(string sourceFileName, string destinationFileName, bool overwrite);
}
