using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services.SessionStates;

internal interface ILiveSessionState
{
    SessionState State { get; }

    bool CanTransitionTo(SessionState nextState);

    void Enter(LiveSession session, DateTimeOffset occurredAt);

    AuthoritativeSessionTimerSnapshot GetQuestionTimerSnapshot(LiveSession session, DateTimeOffset observedAt);

    AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt);

    bool IsQuestionTimerAdvancing(LiveSession session);
}
