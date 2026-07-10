using umbral_backend.Application.Missions.Commands.AddMissionNode;

namespace umbral_backend.Application.UnitTests.Application.Missions.Commands;

// Covers the conditional Must() predicates the happy-path validator tests skip: a Substage with a
// null PlayMode and a Clue with a null / invalid ClueVisibilityPolicy.
public sealed class AddMissionNodeCommandValidatorBranchTests
{
    private static readonly AddMissionNodeCommandValidator Validator = new();

    [Fact]
    public void Substage_WithNullPlayMode_IsInvalid()
    {
        var result = Validator.Validate(
            new AddMissionNodeCommand(1, "Substage", "Sub", 1, StageId: 5, PlayMode: null));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddMissionNodeCommand.PlayMode));
    }

    [Fact]
    public void Clue_WithNullVisibilityPolicy_IsAllowed()
    {
        var result = Validator.Validate(
            new AddMissionNodeCommand(1, "Clue", "Clue", 1, StageId: 5, SubstageId: 6, ClueText: "Hint", ClueVisibilityPolicy: null));

        result.Errors.Should().NotContain(error => error.PropertyName == nameof(AddMissionNodeCommand.ClueVisibilityPolicy));
    }

    [Fact]
    public void Clue_WithInvalidVisibilityPolicy_IsInvalid()
    {
        var result = Validator.Validate(
            new AddMissionNodeCommand(1, "Clue", "Clue", 1, StageId: 5, SubstageId: 6, ClueText: "Hint", ClueVisibilityPolicy: "Nonsense"));

        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddMissionNodeCommand.ClueVisibilityPolicy));
    }
}
