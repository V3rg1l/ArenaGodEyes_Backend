using System.Text;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class CombatLogTailReader
{
    private readonly Dictionary<string, TailReadCursor> _cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly IFileSystem _fileSystem;

    public CombatLogTailReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<IReadOnlyList<CombatLogLineEnvelope>> ReadNewLinesAsync(
        string filePath,
        bool startFromEnd,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(filePath))
        {
            return [];
        }

        if (!_cursors.TryGetValue(filePath, out var cursor))
        {
            cursor = await InitializeCursorAsync(filePath, startFromEnd, cancellationToken);
            _cursors[filePath] = cursor;
            if (startFromEnd)
            {
                return [];
            }
        }

        var results = new List<CombatLogLineEnvelope>();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length < cursor.ByteOffset)
        {
            cursor = new TailReadCursor(0, 0);
            _cursors[filePath] = cursor;
        }

        stream.Seek(cursor.ByteOffset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var byteOffset = stream.Position;
            var rawLine = await reader.ReadLineAsync(cancellationToken);
            if (rawLine is null)
            {
                break;
            }

            cursor = cursor with
            {
                LineNumber = cursor.LineNumber + 1,
                ByteOffset = stream.Position
            };

            results.Add(new CombatLogLineEnvelope(filePath, cursor.LineNumber, byteOffset, rawLine));
        }

        _cursors[filePath] = cursor;
        return results;
    }

    private static async Task<TailReadCursor> InitializeCursorAsync(
        string filePath,
        bool startFromEnd,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (startFromEnd)
        {
            return new TailReadCursor(stream.Length, 0);
        }

        return new TailReadCursor(0, 0);
    }

    private sealed record TailReadCursor(long ByteOffset, long LineNumber);
}
