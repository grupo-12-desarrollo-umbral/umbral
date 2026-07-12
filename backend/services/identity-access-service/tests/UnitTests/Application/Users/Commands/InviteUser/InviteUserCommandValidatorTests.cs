using umbral_backend.Application.Users.Commands.InviteUser;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.InviteUser;

public sealed class InviteUserCommandValidatorTests
{
    private readonly InviteUserCommandValidator _validator = new();

    [Theory]
    [InlineData("Operator")]
    [InlineData("Administrator")]
    [InlineData("participant")] // known role: the Participant *rule* is a domain concern, not validation
    public async Task Validate_WithKnownRoleAndValidEmail_Passes(string role)
    {
        var result = await _validator.ValidateAsync(new InviteUserCommand("invitee@example.com", role));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Validate_WithInvalidEmail_Fails(string email)
    {
        var result = await _validator.ValidateAsync(new InviteUserCommand(email, "Operator"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(InviteUserCommand.Email));
    }

    [Fact]
    public async Task Validate_WithEmailExceedingDisplayNameLimit_Fails()
    {
        var longLocalPart = new string('a', 200);
        var result = await _validator.ValidateAsync(new InviteUserCommand($"{longLocalPart}@example.com", "Operator"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(InviteUserCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SuperAdmin")]
    public async Task Validate_WithUnknownOrEmptyRole_Fails(string role)
    {
        var result = await _validator.ValidateAsync(new InviteUserCommand("invitee@example.com", role));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(InviteUserCommand.Role));
    }
}
