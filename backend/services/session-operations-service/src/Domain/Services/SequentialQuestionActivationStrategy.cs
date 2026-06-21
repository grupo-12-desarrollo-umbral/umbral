using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Services;

public sealed class SequentialQuestionActivationStrategy : IQuestionActivationStrategy
{
    public int? Next(LiveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Indices are positions into the questions ordered by SequenceOrder — the same
        // ordering every consumer applies (LiveSession.ActivateQuestion,
        // SessionTimerSnapshotDtoFactory, TriviaRoundOrchestratorFacade). ActiveQuestionIndex
        // therefore stores an ordered position, not a position in the stored collection.
        var questionCount = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Count;

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
