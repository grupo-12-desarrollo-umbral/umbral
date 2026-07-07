using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

namespace umbral_backend.Application.UnitTests.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed class ValidateParticipantMembershipAccessQueryValidatorTests
{
    private readonly ValidateParticipantMembershipAccessQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidIdentifiers_Succeeds()
    {
        var result = await _validator.ValidateAsync(new ValidateParticipantMembershipAccessQuery(
            Guid.NewGuid(),
            Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMissingIdentifiers_Fails()
    {
        var result = await _validator.ValidateAsync(new ValidateParticipantMembershipAccessQuery(
            Guid.Empty,
            Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(ValidateParticipantMembershipAccessQuery.LiveSessionId),
            nameof(ValidateParticipantMembershipAccessQuery.TeamId));
    }
}
