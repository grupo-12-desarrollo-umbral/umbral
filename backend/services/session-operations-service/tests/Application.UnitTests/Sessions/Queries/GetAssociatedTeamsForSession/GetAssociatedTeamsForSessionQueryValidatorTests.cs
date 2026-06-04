using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetAssociatedTeamsForSession;

public sealed class GetAssociatedTeamsForSessionQueryValidatorTests
{
    private readonly GetAssociatedTeamsForSessionQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidQuery_Passes()
    {
        var result = await _validator.ValidateAsync(
            new GetAssociatedTeamsForSessionQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyLiveSessionId_Fails()
    {
        var result = await _validator.ValidateAsync(
            new GetAssociatedTeamsForSessionQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetAssociatedTeamsForSessionQuery.LiveSessionId));
    }
}
