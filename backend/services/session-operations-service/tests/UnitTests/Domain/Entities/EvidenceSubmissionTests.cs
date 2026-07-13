using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Exercises the umbrella base EvidenceSubmission through its concrete trivia specialization:
// the base reference invariant (session + team + active substage) is enforced for every form.
public sealed class EvidenceSubmissionTests
{
    [Fact]
    public void CreatePending_WithValidUmbrellaContext_StartsPending()
    {
        var submission = new PendingEvidenceSubmission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        submission.ValidationState.Should().Be(EvidenceValidationState.Pending);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_WithoutSessionTeamOrActiveSubstage_ThrowsContextRequired(bool emptySession, bool emptyTeam, bool emptySubstage)
    {
        var liveSessionId = emptySession ? Guid.Empty : Guid.NewGuid();
        var teamId = emptyTeam ? Guid.Empty : Guid.NewGuid();
        var substageId = emptySubstage ? Guid.Empty : Guid.NewGuid();

        var act = () => TriviaAnswerSubmission.Accept(
            liveSessionId,
            teamId,
            substageId,
            questionSequenceOrder: 1,
            selectedOptionSequenceOrder: 1,
            submittedByParticipantId: Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            isCorrect: true,
            scoreValue: 100);

        act.Should().Throw<EvidenceSubmissionContextRequiredException>();
    }

    private sealed class PendingEvidenceSubmission : EvidenceSubmission
    {
        public PendingEvidenceSubmission(
            Guid evidenceSubmissionId,
            Guid liveSessionId,
            Guid teamId,
            Guid activeSubstageId,
            Guid submittedByParticipantId,
            DateTimeOffset submittedAt)
            : base(
                evidenceSubmissionId,
                liveSessionId,
                teamId,
                activeSubstageId,
                EvidenceSubmissionType.TriviaAnswer,
                submittedByParticipantId,
                submittedAt)
        {
        }
    }
}
