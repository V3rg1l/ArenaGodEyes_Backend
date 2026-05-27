namespace ArenaGodEyes.ApiLocal.Contracts;

public sealed record ManualAnalysisImportRequest(
    string ResponseText,
    string Provider = "manual_chatgpt");
