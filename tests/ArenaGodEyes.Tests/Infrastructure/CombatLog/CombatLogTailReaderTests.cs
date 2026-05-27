using ArenaGodEyes.Infrastructure.CombatLog;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Tests.Infrastructure.CombatLog;

public sealed class CombatLogTailReaderTests : IDisposable
{
    private readonly string _filePath;
    private readonly string _tempDirectoryPath;

    public CombatLogTailReaderTests()
    {
        _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "ArenaGodEyes.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectoryPath);
        _filePath = Path.Combine(_tempDirectoryPath, "WoWCombatLog.txt");
        File.WriteAllText(_filePath, string.Empty);
    }

    [Fact]
    public async Task ReadNewLinesAsync_WhenReadingFromStart_ReturnsAppendedLines()
    {
        await File.WriteAllLinesAsync(_filePath, ["line-1", "line-2"]);
        var reader = new CombatLogTailReader(new PhysicalFileSystem());

        var lines = await reader.ReadNewLinesAsync(_filePath, startFromEnd: false);

        Assert.Equal(2, lines.Count);
        Assert.Equal("line-1", lines[0].RawLine);
        Assert.Equal("line-2", lines[1].RawLine);
    }

    [Fact]
    public async Task ReadNewLinesAsync_WhenReadingAgain_ReturnsOnlyNewContent()
    {
        await File.WriteAllLinesAsync(_filePath, ["line-1"]);
        var reader = new CombatLogTailReader(new PhysicalFileSystem());
        _ = await reader.ReadNewLinesAsync(_filePath, startFromEnd: false);

        await File.AppendAllLinesAsync(_filePath, ["line-2", "line-3"]);

        var lines = await reader.ReadNewLinesAsync(_filePath, startFromEnd: false);

        Assert.Equal(2, lines.Count);
        Assert.Equal("line-2", lines[0].RawLine);
        Assert.Equal("line-3", lines[1].RawLine);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectoryPath))
        {
            Directory.Delete(_tempDirectoryPath, recursive: true);
        }
    }
}
