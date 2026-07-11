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
            activeQuestion);
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
