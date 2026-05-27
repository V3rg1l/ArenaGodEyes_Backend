using System.Text.RegularExpressions;
using ArenaGodEyes.Core.Application.Settings.Abstractions;
using ArenaGodEyes.Core.Application.Settings.Models;
using ArenaGodEyes.Infrastructure.FileSystem;

namespace ArenaGodEyes.Infrastructure.Settings.Services;

public sealed class AddonStatusService : IAddonStatusService
{
    private static readonly Regex VersionRegex = new(@"^##\s*Version:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private readonly IFileSystem _fileSystem;

    public AddonStatusService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task<AddonStatus> GetStatusAsync(string? addonDirectory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(addonDirectory) || !_fileSystem.DirectoryExists(addonDirectory))
        {
            return Task.FromResult(new AddonStatus(false, false, false, null, addonDirectory));
        }

        var tocPath = Path.Combine(addonDirectory, "ArenaGodEyes.toc");
        var luaPath = Path.Combine(addonDirectory, "src", "bootstrap.lua");

        var tocFound = _fileSystem.FileExists(tocPath);
        var luaFound = _fileSystem.FileExists(luaPath);
        var installed = tocFound && luaFound;

        string? version = null;
        if (tocFound)
        {
            var tocContents = _fileSystem.ReadAllText(tocPath);
            var versionMatch = VersionRegex.Match(tocContents);
            version = versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : null;
        }

        return Task.FromResult(new AddonStatus(installed, tocFound, luaFound, version, addonDirectory));
    }
}
