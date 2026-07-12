using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.UnitTests.Common.Security;

public sealed class AuthorizeAttributeTests
{
    [Fact]
    public void Roles_DefaultsToEmptyString()
    {
        new AuthorizeAttribute().Roles.Should().BeEmpty();
    }

    [Fact]
    public void Roles_CanBeInitialised()
    {
        new AuthorizeAttribute { Roles = "Operator,Administrator" }.Roles
            .Should().Be("Operator,Administrator");
    }
}
