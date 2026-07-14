using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Commands.RecordScoreEntry;

public sealed class RecordScoreEntryCommandValidatorTests
{
    private readonly RecordScoreEntryCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenCommandIsValid_Succeeds()
    {
        var result = await _validator.ValidateAsync(CreateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenLiveSessionIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { LiveSessionId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RecordScoreEntryCommand.LiveSessionId));
    }

    [Fact]
    public async Task ValidateAsync_WhenTeamIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { TeamId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RecordScoreEntryCommand.TeamId));
    }

    [Fact]
    public async Task ValidateAsync_WhenReasonCodeIsMissing_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { ReasonCode = string.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RecordScoreEntryCommand.ReasonCode));
    }

    [Fact]
    public async Task ValidateAsync_WhenScoreValueIsNegative_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { ScoreValue = -1 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RecordScoreEntryCommand.ScoreValue));
    }

    [Fact]
    public async Task ValidateAsync_WhenSourceEntityIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { SourceEntityId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(RecordScoreEntryCommand.SourceEntityId));
    }

    private static RecordScoreEntryCommand CreateCommand()
    {
        return new RecordScoreEntryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "trivia-answer-correct",
            100,
            DateTimeOffset.UtcNow,
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid());
    }
}
