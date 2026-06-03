using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

namespace umbral_backend.Application.UnitTests.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public sealed class ValidateParticipantMembershipAccessQueryValidatorTests
{
    private readonly ValidateParticipantMembershipAccessQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidIdentifiers_Succeeds()
    {
        var result = await _validator.ValidateAsync(new ValidateParticipantMembershipAccessQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "join-token"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMissingIdentifiers_Fails()
    {
        var result = await _validator.ValidateAsync(new ValidateParticipantMembershipAccessQuery(
            Guid.Empty,
            Guid.Empty,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(ValidateParticipantMembershipAccessQuery.LiveSessionId),
            nameof(ValidateParticipantMembershipAccessQuery.TeamId));
    }
}
