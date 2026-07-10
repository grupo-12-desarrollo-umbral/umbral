using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Exercises the umbrella base EvidenceSubmission through its concrete trivia specialization:
// the base reference invariant (session + team + active substage) is enforced for every form.
public sealed class EvidenceSubmissionTests
{
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
}
