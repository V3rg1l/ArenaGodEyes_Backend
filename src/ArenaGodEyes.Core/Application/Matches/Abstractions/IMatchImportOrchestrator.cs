using ArenaGodEyes.Core.Application.Matches.Models;

namespace ArenaGodEyes.Core.Application.Matches.Abstractions;

public interface IMatchImportOrchestrator
{
    Task<ImportedMatchesResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default);
}
