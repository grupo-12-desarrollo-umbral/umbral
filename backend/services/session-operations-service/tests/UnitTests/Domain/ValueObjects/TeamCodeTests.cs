using umbral_backend.Domain.ValueObjects;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.ValueObjects;

public sealed class TeamCodeTests
{
    [Fact]
    public void Create_NormalizesCode()
    {
        var code = TeamCode.Create(" a-01 ");

        code.Value.Should().Be("A-01");
    }

    [Fact]
    public void Create_WhenCodeIsBlank_ThrowsRequiredException()
    {
        var act = () => TeamCode.Create("   ");

        act.Should().Throw<TeamCodeRequiredException>();
    }
}
