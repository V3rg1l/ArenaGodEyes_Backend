using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class WowInstallationDetector : IWowInstallationDetector
{
    private static readonly string[] CandidatePaths =
    [
        @"C:\Program Files (x86)\World of Warcraft\_retail_",
        @"C:\Program Files\World of Warcraft\_retail_"
    ];

    private readonly IFileSystem _fileSystem;

    public WowInstallationDetector(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task<string?> DetectWowRetailPathAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var detectedPath = CandidatePaths.FirstOrDefault(_fileSystem.DirectoryExists);
        return Task.FromResult(detectedPath);
    }
}
