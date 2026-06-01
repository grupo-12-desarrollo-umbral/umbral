using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandValidatorTests
{
    private readonly AuthenticateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsValidGatewayRoleAndClaims()
    {
        var command = new AuthenticateUserCommand(
            "user-123",
            "Ada Lovelace",
            "ada@example.com",
            Role.Administrator);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsMissingClaims()
    {
        var command = new AuthenticateUserCommand(
            string.Empty,
            string.Empty,
            "invalid-email",
            Role.Operator);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(new[]
        {
            nameof(AuthenticateUserCommand.ExternalIdentityId),
            nameof(AuthenticateUserCommand.DisplayName),
            nameof(AuthenticateUserCommand.Email)
        });
    }
}
