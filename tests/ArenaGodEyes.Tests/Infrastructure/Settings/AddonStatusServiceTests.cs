using ArenaGodEyes.Infrastructure.FileSystem;
using ArenaGodEyes.Infrastructure.Settings.Services;

namespace ArenaGodEyes.Tests.Infrastructure.Settings;

public sealed class AddonStatusServiceTests : IDisposable
{
    private readonly string _tempDirectoryPath;

    public AddonStatusServiceTests()
    {
        _tempDirectoryPath = Path.Combine(Path.GetTempPath(), "ArenaGodEyes.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectoryPath);
    }

    [Fact]
    public async Task GetStatusAsync_WhenAddonFilesExist_ReturnsInstalledStatus()
    {
        var addonDirectoryPath = Path.Combine(_tempDirectoryPath, "ArenaGodEyes");
        var sourceDirectoryPath = Path.Combine(addonDirectoryPath, "src");

        Directory.CreateDirectory(sourceDirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(addonDirectoryPath, "ArenaGodEyes.toc"), "## Version: 0.1.0");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectoryPath, "bootstrap.lua"), "-- bootstrap");

        var service = new AddonStatusService(new PhysicalFileSystem());

        var result = await service.GetStatusAsync(addonDirectoryPath);

        Assert.True(result.Installed);
        Assert.True(result.TocFound);
        Assert.True(result.LuaFound);
        Assert.Equal("0.1.0", result.Version);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectoryPath))
        {
            Directory.Delete(_tempDirectoryPath, recursive: true);
        }
    }
}
