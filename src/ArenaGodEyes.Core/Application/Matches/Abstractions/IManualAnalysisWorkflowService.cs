using ArenaGodEyes.Core.Application.Matches.Models;

namespace ArenaGodEyes.Core.Application.Matches.Abstractions;

public interface IManualAnalysisWorkflowService
{
    Task<ChatGptPromptExport?> ExportPromptAsync(string matchId, CancellationToken cancellationToken = default);

    Task<ManualAnalysisImportResult?> ImportResponseAsync(
        string matchId,
        string responseText,
        CancellationToken cancellationToken = default);
}
