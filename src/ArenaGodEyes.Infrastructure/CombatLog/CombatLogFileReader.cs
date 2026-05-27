using System.Text;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class CombatLogFileReader
{
    private readonly IFileSystem _fileSystem;

    public CombatLogFileReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<CombatLogFileReaderResult> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException("Combat log file was not found.", filePath);
        }

        var lines = new List<CombatLogLineEnvelope>();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        long lineNumber = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var byteOffset = stream.Position;
            var rawLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            lineNumber++;

            lines.Add(new CombatLogLineEnvelope(filePath, lineNumber, byteOffset, rawLine));
        }

        return new CombatLogFileReaderResult(filePath, lines);
    }
}
