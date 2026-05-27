using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class AddonInstallerService : IAddonInstallerService
{
    private readonly IAddonStatusService _addonStatusService;
    private readonly IFileSystem _fileSystem;
    private readonly WorkspacePaths _workspacePaths;

    public AddonInstallerService(
        IAddonStatusService addonStatusService,
        IFileSystem fileSystem,
        WorkspacePaths workspacePaths)
    {
        _addonStatusService = addonStatusService;
        _fileSystem = fileSystem;
        _workspacePaths = workspacePaths;
    }

    public async Task<AddonStatus> InstallAsync(string wowRetailPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wowRetailPath))
        {
            throw new ArgumentException("WoW retail path is required.", nameof(wowRetailPath));
        }

        var sourceDirectory = Path.Combine(_workspacePaths.WorkspaceRootPath, "ArenaGodEyes.Addon", "src", "ArenaGodEyes");
        var destinationDirectory = Path.Combine(wowRetailPath, "Interface", "AddOns", "ArenaGodEyes");

        if (!_fileSystem.DirectoryExists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Addon source directory was not found: {sourceDirectory}");
        }

        CopyDirectory(sourceDirectory, destinationDirectory);

        return await _addonStatusService.GetStatusAsync(destinationDirectory, cancellationToken);
    }

    private void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!_fileSystem.DirectoryExists(destinationDirectory))
        {
            _fileSystem.CreateDirectory(destinationDirectory);
        }

        foreach (var filePath in _fileSystem.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var destinationFolderPath = Path.GetDirectoryName(destinationFilePath)!;

            if (!_fileSystem.DirectoryExists(destinationFolderPath))
            {
                _fileSystem.CreateDirectory(destinationFolderPath);
            }

            _fileSystem.CopyFile(filePath, destinationFilePath, overwrite: true);
        }
    }
}
