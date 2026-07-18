using umbral_backend.Application.Users.Commands.DeactivateUser;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandValidatorTests
{
    private readonly DeactivateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsPositiveUserId()
    {
        var result = _validator.Validate(new DeactivateUserCommand(10));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsNonPositiveUserId()
    {
        var result = _validator.Validate(new DeactivateUserCommand(0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(DeactivateUserCommand.UserId));
    }
}
