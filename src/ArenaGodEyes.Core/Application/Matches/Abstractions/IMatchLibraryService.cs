using ArenaGodEyes.Core.Application.Matches.Models;

namespace ArenaGodEyes.Core.Application.Matches.Abstractions;

public interface IMatchLibraryService
{
    Task<IReadOnlyList<MatchLibraryItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<MatchReviewDetails?> GetAsync(string matchId, CancellationToken cancellationToken = default);

    Task<bool> AttachVideoAsync(string matchId, string videoPath, CancellationToken cancellationToken = default);
}
