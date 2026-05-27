using ArenaGodEyes.Infrastructure.FileSystem;
using ArenaGodEyes.Infrastructure.Settings.Services;

namespace ArenaGodEyes.Tests.Infrastructure.Settings;

public sealed class WowInstallationDetectorTests
{
    [Fact]
    public async Task DetectWowRetailPathAsync_WhenKnownPathDoesNotExist_ReturnsNullOrKnownPath()
    {
        var detector = new WowInstallationDetector(new PhysicalFileSystem());

        var result = await detector.DetectWowRetailPathAsync();

        Assert.True(result is null || result.EndsWith(@"\World of Warcraft\_retail_"));
    }
}
