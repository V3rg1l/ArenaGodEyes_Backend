using ArenaGodEyes.Core.Application.CombatLog.Models;

namespace ArenaGodEyes.Core.Application.CombatLog.Abstractions;

public interface ICombatLogImportService
{
    Task<CombatLogImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
