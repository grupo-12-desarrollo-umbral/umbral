using umbral_backend.Application.Sessions.Commands.AddOperativeClue;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AddOperativeClue;

public sealed class AddOperativeClueCommandValidatorTests
{
    private readonly AddOperativeClueCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WhenCommandIsValid_Passes()
    {
        var result = await _validator.ValidateAsync(CreateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand(liveSessionId: Guid.Empty));

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AddOperativeClueCommand.LiveSessionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WhenClueTextIsEmpty_Fails(string clueText)
    {
        var result = await _validator.ValidateAsync(CreateCommand(clueText: clueText));

        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(AddOperativeClueCommand.ClueText));
    }

    [Fact]
    public async Task Validate_WhenClueTextExceedsMaximumLength_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand(
            clueText: new string('a', 501)));

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AddOperativeClueCommand.ClueText));
    }

    [Fact]
    public async Task Validate_WhenTeamIdsAreEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand(teamIds: []));

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AddOperativeClueCommand.TeamIds));
    }

    private static AddOperativeClueCommand CreateCommand(
        Guid? liveSessionId = null,
        string clueText = "Check the clock.",
        IReadOnlyList<Guid>? teamIds = null)
        => new(liveSessionId ?? Guid.NewGuid(), clueText, teamIds ?? [Guid.NewGuid()]);
}
