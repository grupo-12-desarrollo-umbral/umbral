using umbral_backend.Application.Users.Commands.AuthenticateUser;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandValidatorTests
{
    private readonly AuthenticateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsNonEmptyDisplayName()
    {
        var command = new AuthenticateUserCommand("Ada Lovelace");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsEmptyDisplayName()
    {
        var command = new AuthenticateUserCommand(string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName)
            .Should().Contain(nameof(AuthenticateUserCommand.DisplayName));
    }
}
