using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Common;

// Maps the domain projection (TriviaAnsweredMonitorSnapshot) to the operator monitor DTO. It only
// copies answered/not-answered + AnsweredAt + active-question identity; there is no option/correctness/
// score field to map, so the no-leak invariant carries straight through from the domain type's shape.
public static class TriviaAnsweredMonitorDtoFactory
{
    public static TriviaAnsweredMonitorDto Create(
        LiveSession liveSession,
        TriviaAnsweredMonitorSnapshot snapshot)
    {
        var teams = snapshot.TeamStatuses
            .Select(status => new TriviaTeamAnsweredStatusDto(
                status.TeamId,
                status.TeamCode,
                status.DisplayName,
                status.Answered,
                status.AnsweredAt))
            .ToList();

        return new TriviaAnsweredMonitorDto(
            liveSession.LiveSessionId,
            snapshot.SubstageSnapshotId,
            snapshot.QuestionSequenceOrder,
            teams);
    }
}
