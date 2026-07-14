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

    AuthoritativeSessionTimerSnapshot GetSubstageTimerSnapshot(LiveSession session, DateTimeOffset observedAt);

    AuthoritativeSessionTimerSnapshot MarkSubstageTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt);

    bool IsSubstageTimerAdvancing(LiveSession session);

    // Gates timer-driven substage advancement: only Active advances, every other state rejects
    // (Paused freezes it). Keeps the "operator cannot force advancement" rule in the state type.
    void EnsureCanAdvanceSubstage(LiveSession session);

    // Gates evidence registration: only Active admits evidence, every other state rejects
    // (Paused/Finished/Cancelled). Keeps the session-state gameplay rule in the state type.
    void EnsureCanRegisterEvidence(LiveSession session);
}
