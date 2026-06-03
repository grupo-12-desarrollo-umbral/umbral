using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

namespace umbral_backend.Application.UnitTests.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class GetSessionTeamsForParticipantQueryValidatorTests
{
    private readonly GetSessionTeamsForParticipantQueryValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCode_Succeeds()
    {
        var result = await _validator.ValidateAsync(new GetSessionTeamsForParticipantQuery("RSF231"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("ABC-12")]
    public async Task Validate_WithInvalidCode_Fails(string sessionCode)
    {
        var result = await _validator.ValidateAsync(new GetSessionTeamsForParticipantQuery(sessionCode));

        result.IsValid.Should().BeFalse();
    }
}
