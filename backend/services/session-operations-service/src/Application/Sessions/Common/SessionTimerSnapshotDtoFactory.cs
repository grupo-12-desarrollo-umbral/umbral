using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

public static class SessionTimerSnapshotDtoFactory
{
    public static SessionTimerSnapshotDto Create(
        LiveSession liveSession,
        Guid? teamId,
        AuthoritativeSessionTimerSnapshot snapshot)
    {
        var activeQuestion = CreateActiveQuestionSnapshot(liveSession, snapshot);

        return new SessionTimerSnapshotDto(
            liveSession.LiveSessionId,
            teamId,
            liveSession.State.ToString(),
            ToWholeSeconds(snapshot.TotalDuration),
            ToWholeSeconds(snapshot.RemainingDuration),
            ResolveStatus(snapshot),
            snapshot.IsAdvancing,
            snapshot.IsExpired,
            snapshot.ObservedAt,
            snapshot.AdvancingSince,
            snapshot.ExpiredAt,
            activeQuestion,
            ResolveAwaitingRevealSequenceOrder(liveSession));
    }

    // During the HU-35 reveal window ActiveQuestionIndex is null (the question closed) but the
    // just-closed question's result is still readable. Surface its sequence order so a client that
    // opens the session mid-reveal — with no QuestionActivated push to have captured — can fetch the
    // answer review. Returns null outside the reveal window (mirrors ClosedTriviaQuestionResultReader's
    // reveal-window upper bound: the just-closed substage-local position is PendingNextQuestionIndex - 1,
    // or the substage's last question when the last one just closed).
    private static int? ResolveAwaitingRevealSequenceOrder(LiveSession liveSession)
    {
        if (liveSession.ActiveQuestionIndex is not null ||
            !liveSession.IsAwaitingQuestionReveal ||
            liveSession.ActiveSubstageId is not { } activeSubstageId)
        {
            return null;
        }

        var orderedQuestions = liveSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Where(question => question.SubstageSnapshotId == activeSubstageId)
            .OrderBy(question => question.SequenceOrder)
            .ToArray();

        var justClosedIndex = (liveSession.PendingNextQuestionIndex ?? orderedQuestions.Length) - 1;

        if (justClosedIndex < 0 || justClosedIndex >= orderedQuestions.Length)
        {
            return null;
        }

        return orderedQuestions[justClosedIndex].SequenceOrder;
    }

    // Sources the active-substage window straight from the authoritative snapshot the caller
    // already resolved (GetAuthoritativeSessionTimerSnapshot) — no redundant re-fetch.
    private static ActiveQuestionSnapshotDto? CreateActiveQuestionSnapshot(
        LiveSession liveSession,
        AuthoritativeSessionTimerSnapshot snapshot)
    {
        if (liveSession.ActiveQuestionIndex is null)
        {
            return null;
        }

        var questionIndex = liveSession.ActiveQuestionIndex.Value;
        var (question, options) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(liveSession, questionIndex);

        return new ActiveQuestionSnapshotDto(
            liveSession.LiveSessionId,
            questionIndex,
            question.SequenceOrder,
            question.Prompt,
            options,
            question.TimeLimitSeconds,
            ToWholeSeconds(snapshot.RemainingDuration),
            snapshot.AdvancingSince ?? snapshot.ObservedAt,
            liveSession.ActiveSubstageId.Value);
    }

    private static string ResolveStatus(AuthoritativeSessionTimerSnapshot snapshot)
    {
        if (snapshot.IsExpired)
        {
            return "Expired";
        }

        return snapshot.IsAdvancing ? "Advancing" : "Frozen";
    }

    private static int ToWholeSeconds(TimeSpan duration)
    {
        return (int)Math.Ceiling(duration.TotalSeconds);
    }
}
