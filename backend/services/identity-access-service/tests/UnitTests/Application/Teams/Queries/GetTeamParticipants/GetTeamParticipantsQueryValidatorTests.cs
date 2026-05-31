using umbral_backend.Application.Teams.Queries.GetTeamParticipants;

namespace umbral_backend.Application.UnitTests.Application.Teams.Queries.GetTeamParticipants;

public sealed class GetTeamParticipantsQueryValidatorTests
{
    private readonly GetTeamParticipantsQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidTeamId_Succeeds()
    {
        var result = await _validator.ValidateAsync(new GetTeamParticipantsQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyTeamId_Fails()
    {
        var result = await _validator.ValidateAsync(new GetTeamParticipantsQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetTeamParticipantsQuery.TeamId));
    }
}
