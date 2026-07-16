using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class ClosedTriviaQuestionResultReader
{
    public static TriviaQuestionSnapshot GetClosedQuestion(
        LiveSession session,
        int questionSequenceOrder)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.State is SessionState.Scheduled or SessionState.Preparing or SessionState.Cancelled)
        {
            throw new TriviaQuestionResultNotAvailableException();
        }

        var orderedSubstages = session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToArray();

        var activeSubstageIndex = Array.FindIndex(
            orderedSubstages,
            substage => substage.SubstageSnapshotId == session.ActiveSubstageId);

        if (session.State == SessionState.Active &&
            activeSubstageIndex >= 0 &&
            orderedSubstages[activeSubstageIndex].PlayMode == SubstagePlayMode.Trivia)
        {
            return GetFromActiveTriviaSubstage(
                session,
                orderedSubstages[activeSubstageIndex].SubstageSnapshotId,
                questionSequenceOrder);
        }

        var completedSubstageCount = session.State == SessionState.Finished
            ? orderedSubstages.Length
            : Math.Max(activeSubstageIndex, 0);

        var question = orderedSubstages
            .Take(completedSubstageCount)
            .Reverse()
            .Where(substage => substage.PlayMode == SubstagePlayMode.Trivia)
            .Select(substage => session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
                .SingleOrDefault(candidate =>
                    candidate.SubstageSnapshotId == substage.SubstageSnapshotId &&
                    candidate.SequenceOrder == questionSequenceOrder))
            .FirstOrDefault(candidate => candidate is not null);

        return question ?? throw new TriviaQuestionResultNotAvailableException();
    }

    private static TriviaQuestionSnapshot GetFromActiveTriviaSubstage(
        LiveSession session,
        Guid activeSubstageId,
        int questionSequenceOrder)
    {
        var orderedQuestions = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Where(question => question.SubstageSnapshotId == activeSubstageId)
            .OrderBy(question => question.SequenceOrder)
            .ToArray();
        var requestedIndex = Array.FindIndex(
            orderedQuestions,
            question => question.SequenceOrder == questionSequenceOrder);

        // Exclusive upper bound of the substage-local positions whose result is readable. While a
        // question is active it is ActiveQuestionIndex (only strictly-earlier questions have closed).
        // During the post-close reveal window (HU-35) ActiveQuestionIndex is null but the just-closed
        // question is now readable: PendingNextQuestionIndex is that question's position + 1, or — when
        // the substage's last question just closed (null) — the whole substage is closed and readable.
        int? readableBelow = session.ActiveQuestionIndex
            ?? (session.IsAwaitingQuestionReveal
                ? session.PendingNextQuestionIndex ?? orderedQuestions.Length
                : null);

        if (requestedIndex < 0 ||
            readableBelow is null ||
            requestedIndex >= readableBelow.Value)
        {
            throw new TriviaQuestionResultNotAvailableException();
        }

        return orderedQuestions[requestedIndex];
    }
}
