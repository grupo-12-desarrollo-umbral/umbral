using umbral_backend.Application.Common.Models;

namespace umbral_backend.Application.UnitTests.Common.Models;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSucceededTrue()
    {
        var result = Result.Success();

        result.Succeeded.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ReturnsSucceededFalseWithErrors()
    {
        var result = Result.Failure(new[] { "error one", "error two" });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(new[] { "error one", "error two" });
    }
}
