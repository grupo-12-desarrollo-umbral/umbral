using umbral_backend.Application.Users.Commands.ForgotPassword;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithWellFormedEmail_Passes()
    {
        var result = await _validator.ValidateAsync(new ForgotPasswordCommand("participant@example.com"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Validate_WithMissingOrMalformedEmail_Fails(string email)
    {
        var result = await _validator.ValidateAsync(new ForgotPasswordCommand(email));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(ForgotPasswordCommand.Email));
    }
}
