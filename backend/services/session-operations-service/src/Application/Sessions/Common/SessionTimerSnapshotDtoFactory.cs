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
        var activeQuestion = CreateActiveQuestionSnapshot(liveSession, snapshot.ObservedAt);

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

    private static ActiveQuestionSnapshotDto? CreateActiveQuestionSnapshot(
        LiveSession liveSession,
        DateTimeOffset observedAt)
    {
        if (liveSession.ActiveQuestionIndex is null)
        {
            return null;
        }

        var questionIndex = liveSession.ActiveQuestionIndex.Value;
        var question = liveSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .OrderBy(snapshot => snapshot.SequenceOrder)
            .ElementAt(questionIndex);
        var questionTimer = liveSession.GetActiveQuestionTimerSnapshot(observedAt);
        var options = question.Options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => option.OptionText)
            .ToArray();

        return new ActiveQuestionSnapshotDto(
            liveSession.LiveSessionId,
            questionIndex,
            question.SequenceOrder,
            question.Prompt,
            options,
            question.TimeLimitSeconds,
            ToWholeSeconds(questionTimer.RemainingDuration),
            questionTimer.AdvancingSince ?? observedAt);
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
