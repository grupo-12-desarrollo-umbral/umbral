using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RecordEvidenceTraceRegistration;

public sealed class RecordEvidenceTraceRegistrationCommandValidatorTests
{
    private readonly RecordEvidenceTraceRegistrationCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsValid_Passes()
    {
        var command = new RecordEvidenceTraceRegistrationCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            DateTimeOffset.UtcNow);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenEvidenceSubmissionIdEmpty_Fails()
    {
        var command = Command() with { EvidenceSubmissionId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "EvidenceSubmissionId");
    }

    [Fact]
    public void Validate_WhenLiveSessionIdEmpty_Fails()
    {
        var command = Command() with { LiveSessionId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LiveSessionId");
    }

    [Fact]
    public void Validate_WhenTeamIdEmpty_Fails()
    {
        var command = Command() with { TeamId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "TeamId");
    }

    [Fact]
    public void Validate_WhenActiveSubstageIdEmpty_Fails()
    {
        var command = Command() with { ActiveSubstageId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ActiveSubstageId");
    }

    private static RecordEvidenceTraceRegistrationCommand Command() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        EvidenceSubmissionType.TriviaAnswer,
        Guid.NewGuid(),
        "question:1",
        DateTimeOffset.UtcNow);
}
