using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class SessionSourceTests
{
    [Fact]
    public void Create_WithEmptySourceId_ThrowsException()
    {
        var act = () => SessionSource.Create(Guid.Empty);

        act.Should().Throw<SessionSourceEntityRequiredException>();
    }

    [Fact]
    public void Create_WithMissionId_PreservesMissionIdentity()
    {
        var missionId = Guid.NewGuid();

        var source = SessionSource.Create(missionId);

        source.SourceType.Should().Be(umbral_backend.Domain.Enums.SessionSourceType.Mission);
        source.SourceEntityId.Should().Be(missionId);
    }
}
