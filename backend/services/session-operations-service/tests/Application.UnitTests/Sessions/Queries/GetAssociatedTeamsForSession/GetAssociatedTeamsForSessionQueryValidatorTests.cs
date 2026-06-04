using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetAssociatedTeamsForSession;

public sealed class GetAssociatedTeamsForSessionQueryValidatorTests
{
    private readonly GetAssociatedTeamsForSessionQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WhenQueryIsValid_Succeeds()
    {
        var query = new GetAssociatedTeamsForSessionQuery(Guid.NewGuid());

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var query = new GetAssociatedTeamsForSessionQuery(Guid.Empty);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetAssociatedTeamsForSessionQuery.LiveSessionId));
    }
}
