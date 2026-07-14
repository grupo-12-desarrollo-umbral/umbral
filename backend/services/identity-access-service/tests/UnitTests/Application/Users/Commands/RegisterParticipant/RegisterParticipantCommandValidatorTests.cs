using umbral_backend.Application.Users.Commands.RegisterParticipant;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.RegisterParticipant;

public sealed class RegisterParticipantCommandValidatorTests
{
    private readonly RegisterParticipantCommandValidator _validator = new();

    private static RegisterParticipantCommand ValidCommand(
        string displayName = "New Participant",
        string email = "participant@example.com",
        string password = "sup3rsecret") =>
        new(displayName, email, password);

    [Fact]
    public async Task Validate_WithWellFormedInput_Passes()
    {
        var result = await _validator.ValidateAsync(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithBlankDisplayName_Fails()
    {
        var result = await _validator.ValidateAsync(ValidCommand(displayName: " "));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(RegisterParticipantCommand.DisplayName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Validate_WithInvalidEmail_Fails(string email)
    {
        var result = await _validator.ValidateAsync(ValidCommand(email: email));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(RegisterParticipantCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")] // below the 8-char floor
    public async Task Validate_WithMissingOrTooShortPassword_Fails(string password)
    {
        var result = await _validator.ValidateAsync(ValidCommand(password: password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(RegisterParticipantCommand.Password));
    }
}
