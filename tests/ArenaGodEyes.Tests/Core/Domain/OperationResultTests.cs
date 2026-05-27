using ArenaGodEyes.Core.Domain.Results;

namespace ArenaGodEyes.Tests.Core.Domain;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = OperationResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithMessage()
    {
        const string errorMessage = "settings path is invalid";

        var result = OperationResult.Failure(errorMessage);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }
}
