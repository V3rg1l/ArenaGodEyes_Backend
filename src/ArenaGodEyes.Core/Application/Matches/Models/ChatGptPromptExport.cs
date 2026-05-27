namespace ArenaGodEyes.Core.Application.Matches.Models;

public sealed record ChatGptPromptExport(
    string MatchId,
    string PromptPath,
    string PromptText);
