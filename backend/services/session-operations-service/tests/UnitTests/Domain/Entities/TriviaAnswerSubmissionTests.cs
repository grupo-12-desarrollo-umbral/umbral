using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TriviaAnswerSubmissionTests
{
    [Fact]
    public void Accept_WithCorrectOption_RecordsAcceptedCorrectAnswerAndBaseEvidenceContext()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var substageId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 5, TimeSpan.Zero);

        var submission = TriviaAnswerSubmission.Accept(
            liveSessionId,
            teamId,
            substageId,
            questionSequenceOrder: 2,
            selectedOptionSequenceOrder: 1,
            participantId,
            submittedAt,
            isCorrect: true,
            scoreValue: 100);

        submission.LiveSessionId.Should().Be(liveSessionId);
        submission.TeamId.Should().Be(teamId);
        submission.ActiveSubstageId.Should().Be(substageId);
        submission.QuestionSequenceOrder.Should().Be(2);
        submission.SelectedOptionSequenceOrder.Should().Be(1);
        submission.SubmittedByParticipantId.Should().Be(participantId);
        submission.SubmittedAt.Should().Be(submittedAt);
        submission.IsCorrect.Should().BeTrue();
        submission.ScoreValue.Should().Be(100);
        submission.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.EvidenceSubmissionId.Should().NotBeEmpty();
    }

    [Fact]
    public void Accept_WithWrongOption_RecordsZeroScoreButStaysAccepted()
    {
        var participantId = Guid.NewGuid();

        var submission = TriviaAnswerSubmission.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            questionSequenceOrder: 1,
            selectedOptionSequenceOrder: 2,
            participantId,
            new DateTimeOffset(2026, 6, 3, 10, 1, 5, TimeSpan.Zero),
            isCorrect: false,
            scoreValue: 0);

        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.IsCorrect.Should().BeFalse();
        submission.ScoreValue.Should().Be(0);
        // A wrong answer is still attributed to its submitter — acceptance never means anonymous.
        submission.SubmittedByParticipantId.Should().Be(participantId);
    }

    [Fact]
    public void Accept_ProducesAnEvidenceSubmissionSpecialization()
    {
        var submission = TriviaAnswerSubmission.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            isCorrect: true,
            scoreValue: 100);

        submission.Should().BeAssignableTo<EvidenceSubmission>();
    }
}
