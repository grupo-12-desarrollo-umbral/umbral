using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.DTOs;

public static class SessionTimerSnapshotDtoFactory
{
    public static SessionTimerSnapshotDto Create(
        LiveSession liveSession,
        Guid? teamId,
        AuthoritativeSessionTimerSnapshot snapshot)
    {
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
            snapshot.ExpiredAt);
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
