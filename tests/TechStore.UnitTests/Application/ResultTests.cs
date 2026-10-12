using TechStore.Application.Common;

namespace TechStore.UnitTests.Application;

public class ResultTests
{
    [Fact]
    public void Success_SetsIsSuccess()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsError()
    {
        var result = Result.Failure<int>("broken");

        Assert.True(result.IsFailure);
        Assert.Equal("broken", result.Error);
    }
}
