using umbral_backend.Application.Users.Commands.ReactivateUser;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.ReactivateUser;

public sealed class ReactivateUserCommandValidatorTests
{
    private readonly ReactivateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsPositiveUserId()
    {
        var result = _validator.Validate(new ReactivateUserCommand(10));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsNonPositiveUserId()
    {
        var result = _validator.Validate(new ReactivateUserCommand(0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ReactivateUserCommand.UserId));
    }
}
