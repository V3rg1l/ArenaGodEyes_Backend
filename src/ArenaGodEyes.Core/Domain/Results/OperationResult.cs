namespace ArenaGodEyes.Core.Domain.Results;

public sealed record OperationResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static OperationResult Success() => new(true);

    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
