using ArenaGodEyes.Core.Application.CombatLog.Abstractions;
using ArenaGodEyes.Core.Application.CombatLog.Models;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class CombatLogImportService : ICombatLogImportService
{
    private readonly CombatLogFileReader _combatLogFileReader;

    public CombatLogImportService(CombatLogFileReader combatLogFileReader)
    {
        _combatLogFileReader = combatLogFileReader;
    }

    public async Task<CombatLogImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await _combatLogFileReader.ReadAllLinesAsync(filePath, cancellationToken);

        return new CombatLogImportResult(
            result.SourceFile,
            result.Lines.Count,
            result.Lines.FirstOrDefault()?.RawLine,
            result.Lines.LastOrDefault()?.RawLine);
    }
}
