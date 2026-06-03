using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class MaximumTimeTests
{
    [Fact]
    public void Create_WithNonPositiveMinutes_ThrowsException()
    {
        var act = () => MaximumTime.Create(0);

        act.Should().Throw<MaximumTimeMustBePositiveException>();
    }
}
