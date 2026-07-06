using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Services;

public sealed class SequentialQuestionActivationStrategy : IQuestionActivationStrategy
{
    public int? Next(LiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Indices are positions into the ACTIVE SUBSTAGE's questions ordered by SequenceOrder — the
        // same substage-scoped ordering every consumer applies (LiveSession.ActivateQuestion,
        // SessionTimerSnapshotDtoFactory, TriviaRoundOrchestratorFacade). ActiveQuestionIndex stores
        // an ordered position within the active substage, not a position in the flat collection.
        // Null when exhausted -> the signal to advance the substage.
        var questionCount = session.ActiveSubstageId is Guid activeSubstageId
            ? session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
                .Count(question => question.SubstageSnapshotId == activeSubstageId)
            : 0;

        if (questionCount == 0)
        {
            return null;
        }

        if (session.ActiveQuestionIndex is null)
        {
            return 0;
        }

        var currentPosition = session.ActiveQuestionIndex.Value;

        if (currentPosition < 0 || currentPosition >= questionCount - 1)
        {
            return null;
        }

        return currentPosition + 1;
    }
}
