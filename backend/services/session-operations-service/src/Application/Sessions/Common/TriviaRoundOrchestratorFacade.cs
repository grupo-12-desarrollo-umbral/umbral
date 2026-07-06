using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Common;

public sealed class TriviaRoundOrchestratorFacade : ITriviaRoundOrchestratorFacade
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionQuestionBroadcaster _sessionQuestionBroadcaster;
    private readonly IQuestionActivationStrategy _questionActivationStrategy;
    private readonly SessionStateTransitionPolicy _transitionPolicy;

    public TriviaRoundOrchestratorFacade(
        ILiveSessionRepository liveSessionRepository,
        ISessionQuestionBroadcaster sessionQuestionBroadcaster,
        IQuestionActivationStrategy questionActivationStrategy,
        SessionStateTransitionPolicy transitionPolicy)
    {
        _liveSessionRepository = liveSessionRepository;
        _sessionQuestionBroadcaster = sessionQuestionBroadcaster;
        _questionActivationStrategy = questionActivationStrategy;
        _transitionPolicy = transitionPolicy;
    }

    public async Task ActivateNextQuestionAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.ActiveQuestionIndex.HasValue)
        {
            return;
        }

        var nextQuestionIndex = _questionActivationStrategy.Next(session);
        if (!nextQuestionIndex.HasValue)
        {
            return;
        }

        await ActivateQuestionAsync(session, nextQuestionIndex.Value, now, cancellationToken);
    }

    public async Task CloseAndAdvanceAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Idempotency guard: a duplicate/late timer tick after the question already closed (question
        // exhausted -> substage advanced/parked/finished, all leave ActiveQuestionIndex null) returns
        // here without re-closing or double-advancing.
        if (!session.ActiveQuestionIndex.HasValue)
        {
            return;
        }

        // Resolved against the CURRENT (still active) question: is there another question left in
        // this substage? Null => the active substage's last question just closed -> advance substage.
        var nextQuestionIndex = _questionActivationStrategy.Next(session);
        var closedQuestionIndex = session.ActiveQuestionIndex.Value;

        session.CloseActiveQuestion(now);
        var closedEvent = session.DomainEvents
            .OfType<QuestionClosedEvent>()
            .Last(domainEvent => domainEvent.QuestionIndex == closedQuestionIndex);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);
        await _sessionQuestionBroadcaster.BroadcastQuestionClosedAsync(
            new QuestionClosedNotificationDto(
                session.LiveSessionId,
                closedQuestionIndex,
                now,
                closedEvent.WasExpiredByTimer),
            cancellationToken);

        if (nextQuestionIndex.HasValue)
        {
            await ActivateQuestionAsync(session, nextQuestionIndex.Value, now, cancellationToken);
            return;
        }

        await AdvanceSubstageAsync(session, now, cancellationToken);
    }

    // The active substage is exhausted: walk to the next substage (ADR-0005). The domain moves the
    // pointer and raises SubstageAdvancedEvent (or finishes the session when no substage remains —
    // the ONLY path to Finished). Advancing INTO a trivia substage activates its first question;
    // a treasure-hunt substage parks (the substage-scoped strategy yields no question, D-4).
    private async Task AdvanceSubstageAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        session.CompleteActiveSubstageAndAdvance(now, _transitionPolicy);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        var advancedEvent = session.DomainEvents.OfType<SubstageAdvancedEvent>().Last();
        await _sessionQuestionBroadcaster.BroadcastSubstageAdvancedAsync(
            new SubstageAdvancedNotificationDto(
                session.LiveSessionId,
                advancedEvent.FromSubstageId,
                advancedEvent.FromPlayMode.ToString(),
                advancedEvent.ToSubstageId,
                now),
            cancellationToken);

        if (session.State == SessionState.Active)
        {
            await ActivateNextQuestionAsync(session, now, cancellationToken);
        }
    }

    private async Task ActivateQuestionAsync(
        LiveSession session,
        int questionIndex,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        session.ActivateQuestion(questionIndex, now);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        var (question, options) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, questionIndex);

        await _sessionQuestionBroadcaster.BroadcastQuestionActivatedAsync(
            new QuestionActivatedNotificationDto(
                session.LiveSessionId,
                questionIndex,
                question.SequenceOrder,
                question.Prompt,
                options,
                question.TimeLimitSeconds,
                now),
            cancellationToken);
    }
}
