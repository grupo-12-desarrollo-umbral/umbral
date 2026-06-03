using umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;

namespace umbral_backend.Application.UnitTests.Application.JoinTokens.Commands.IssueJoinToken;

public sealed class IssueJoinTokenCommandValidatorTests
{
    private readonly IssueJoinTokenCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidIdentifiers_Succeeds()
    {
        var result = await _validator.ValidateAsync(new IssueJoinTokenCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(10)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMissingIdentifiers_Fails()
    {
        var result = await _validator.ValidateAsync(new IssueJoinTokenCommand(
            Guid.Empty,
            Guid.Empty,
            DateTimeOffset.UtcNow.AddMinutes(10)));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(IssueJoinTokenCommand.LiveSessionId),
            nameof(IssueJoinTokenCommand.TeamId));
    }
}
