using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Common;

public sealed class SubstageAdvanceCoordinator : ISubstageAdvanceCoordinator
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionQuestionBroadcaster _sessionQuestionBroadcaster;
    private readonly IQuestionActivator _questionActivator;
    private readonly SessionStateTransitionPolicy _transitionPolicy;

    public SubstageAdvanceCoordinator(
        ILiveSessionRepository liveSessionRepository,
        ISessionQuestionBroadcaster sessionQuestionBroadcaster,
        IQuestionActivator questionActivator,
        SessionStateTransitionPolicy transitionPolicy)
    {
        _liveSessionRepository = liveSessionRepository;
        _sessionQuestionBroadcaster = sessionQuestionBroadcaster;
        _questionActivator = questionActivator;
        _transitionPolicy = transitionPolicy;
    }

    public async Task BeginRankingRevealAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Idempotency guard: a second clear in the same tick, or a repeat call, must not restart the
        // window or re-push the reveal. The domain no-ops too, but returning here also suppresses the
        // duplicate broadcast — the client would otherwise reset a countdown that never moved.
        if (session.IsAwaitingSubstageRankingReveal)
        {
            return;
        }

        session.BeginSubstageRankingReveal(now, LiveSession.SubstageRankingRevealDuration);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        // No broadcast here on purpose. A treasure hunt opens its reveal from RegisterTargetScan, deep
        // in the domain with no coordinator in the call path, so pushing from this method would leave
        // exactly the mode that needed D-1 most with no reveal on screen. The push is keyed off
        // SubstageRevealStartedEvent instead (BroadcastSubstageRevealStartedNotificationHandler), which
        // both modes raise — the same reason advancement itself moved out of the trivia facade.
    }

    public async Task CompleteRankingRevealAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Idempotency guard: a duplicate/late tick after the reveal already completed returns without
        // re-advancing.
        if (!session.IsAwaitingSubstageRankingReveal)
        {
            return;
        }

        session.CompleteSubstageRankingReveal();

        // D-6: decide finish-vs-advance atomically at this same `now`. If the mission deadline elapsed
        // while the ranking was on screen, the session ends ON that ranking — do NOT activate or
        // broadcast an intermediate substage first (which would flash the next play surface for the
        // gap before Finished lands). Read expiry from the authoritative aggregate timer, never a
        // client timestamp. MarkMissionTimerExpiredIfElapsed is a no-op mutation while time remains,
        // so the advance path below is unaffected.
        if (session.HasMissionDeadline &&
            session.MarkMissionTimerExpiredIfElapsed(now).IsExpired)
        {
            session.FinishOnMissionDeadline(now);
            await _liveSessionRepository.UpdateAsync(session, cancellationToken);
            return;
        }

        await AdvanceSubstageAsync(session, now, cancellationToken);
    }

    public async Task FinishOnMissionDeadlineAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.State != SessionState.Active)
        {
            return;
        }

        session.FinishOnMissionDeadline(now);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);
    }

    // The active substage has ended and its ranking has been shown: walk to the next substage
    // (ADR-0005). The domain moves the pointer and raises SubstageAdvancedEvent, or finishes the
    // session when no substage remains.
    private async Task AdvanceSubstageAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        session.CompleteActiveSubstageAndAdvance(now, _transitionPolicy);

        var advancedEvent = session.DomainEvents.OfType<SubstageAdvancedEvent>().Last();
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        await _sessionQuestionBroadcaster.BroadcastSubstageAdvancedAsync(
            new SubstageAdvancedNotificationDto(
                session.LiveSessionId,
                advancedEvent.FromSubstageId,
                advancedEvent.FromPlayMode.ToString(),
                advancedEvent.ToSubstageId,
                now),
            cancellationToken);

        // Advancing into a trivia substage opens its first question; a treasure hunt activates nothing
        // (its targets are already live and scannable) and simply waits to be cleared.
        if (session.State == SessionState.Active)
        {
            await _questionActivator.ActivateNextQuestionAsync(session, now, cancellationToken);
        }
    }
}
