using FluentValidation;
using umbral_backend.Application.Common.Security;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Common.Security;

public sealed class GatewayRoleParserTests
{
    [Theory]
    [InlineData("Administrator", Role.Administrator)]
    [InlineData("Operator", Role.Operator)]
    [InlineData("Participant", Role.Participant)]
    public void Parse_WithSupportedRole_ReturnsDomainRole(string value, Role expected)
    {
        GatewayRoleParser.Parse(value).Should().Be(expected);
    }

    [Fact]
    public void TryParse_WithMissingRole_ReturnsFalse()
    {
        GatewayRoleParser.TryParse(" ", out var role).Should().BeFalse();
        role.Should().Be(default);
    }

    [Fact]
    public void Parse_WithUnsupportedRole_ThrowsValidationException()
    {
        var act = () => GatewayRoleParser.Parse("Operador");

        act.Should().Throw<ValidationException>()
            .WithMessage("Unsupported role 'Operador'.");
    }
}
