using umbral_backend.Application.Scores.Commands.ApplyPenalty;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Commands.ApplyPenalty;

public sealed class ApplyPenaltyCommandValidatorTests
{
    private readonly ApplyPenaltyCommandValidator _validator = new();

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
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ApplyPenaltyCommand.LiveSessionId));
    }

    [Fact]
    public async Task ValidateAsync_WhenTeamIdIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { TeamId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ApplyPenaltyCommand.TeamId));
    }

    [Fact]
    public async Task ValidateAsync_WhenReasonIsEmpty_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { Reason = string.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ApplyPenaltyCommand.Reason));
    }

    [Fact]
    public async Task ValidateAsync_WhenReasonIsWhitespace_Fails()
    {
        var result = await _validator.ValidateAsync(CreateCommand() with { Reason = "   " });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ApplyPenaltyCommand.Reason));
    }

    private static ApplyPenaltyCommand CreateCommand()
    {
        return new ApplyPenaltyCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unsportsmanlike conduct");
    }
}
