using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class SessionSourceTests
{
    [Fact]
    public void Create_WithEmptySourceId_ThrowsException()
    {
        var act = () => SessionSource.Create(SessionSourceType.Mission, Guid.Empty);

        act.Should().Throw<SessionSourceEntityRequiredException>();
    }
}
