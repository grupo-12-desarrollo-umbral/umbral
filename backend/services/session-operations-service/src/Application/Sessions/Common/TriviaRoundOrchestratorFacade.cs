using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Common;

public sealed class TriviaRoundOrchestratorFacade : ITriviaRoundOrchestratorFacade
{
    // How long a just-closed trivia question keeps showing its result before the next question
    // activates (HU-35 reveal window). The mobile reveal + operator review land on the QuestionClosed
    // push; without this dwell the immediate next-question activation would overwrite the reveal.
    public static readonly TimeSpan QuestionRevealDuration = TimeSpan.FromSeconds(5);

    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionQuestionBroadcaster _sessionQuestionBroadcaster;
    private readonly IQuestionActivationStrategy _questionActivationStrategy;
    private readonly IQuestionActivator _questionActivator;
    private readonly ISubstageAdvanceCoordinator _substageAdvanceCoordinator;

    public TriviaRoundOrchestratorFacade(
        ILiveSessionRepository liveSessionRepository,
        ISessionQuestionBroadcaster sessionQuestionBroadcaster,
        IQuestionActivationStrategy questionActivationStrategy,
        IQuestionActivator questionActivator,
        ISubstageAdvanceCoordinator substageAdvanceCoordinator)
    {
        _liveSessionRepository = liveSessionRepository;
        _sessionQuestionBroadcaster = sessionQuestionBroadcaster;
        _questionActivationStrategy = questionActivationStrategy;
        _questionActivator = questionActivator;
        _substageAdvanceCoordinator = substageAdvanceCoordinator;
    }

    public Task ActivateNextQuestionAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return _questionActivator.ActivateNextQuestionAsync(session, now, cancellationToken);
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
        var (closedQuestion, _) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(
            session,
            closedQuestionIndex);

        // Close the question and open the reveal window instead of advancing immediately: the next
        // question (or substage advance) is deferred to `CompleteQuestionRevealAsync`, fired by the
        // timer worker once the reveal deadline passes. This gives the HU-35 mobile reveal + HU-36B
        // operator review a dwell window on the QuestionClosed push before the next question lands.
        session.CloseActiveQuestionForReveal(now, QuestionRevealDuration, nextQuestionIndex);
        var closedEvent = session.DomainEvents
            .OfType<QuestionClosedEvent>()
            .Last(domainEvent => domainEvent.QuestionIndex == closedQuestionIndex);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);
        await _sessionQuestionBroadcaster.BroadcastQuestionClosedAsync(
            new QuestionClosedNotificationDto(
                session.LiveSessionId,
                closedQuestionIndex,
                now,
                closedEvent.WasExpiredByTimer,
                closedQuestion.Options.Single(option => option.IsCorrect).SequenceOrder,
                closedQuestion.Explanation),
            cancellationToken);
    }

    // Second half of the reveal-windowed close: the deferred activation captured at close now fires.
    // Called by the timer worker once the reveal deadline elapses. Idempotent — a duplicate/late tick
    // after the reveal already completed returns without re-advancing.
    public async Task CompleteQuestionRevealAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsAwaitingQuestionReveal)
        {
            return;
        }

        var nextQuestionIndex = session.CompleteQuestionRevealAndDequeueNext();

        if (nextQuestionIndex.HasValue)
        {
            // ActivateQuestionAsync persists (clearing the reveal fields too) and broadcasts.
            await _questionActivator.ActivateQuestionAsync(session, nextQuestionIndex.Value, now, cancellationToken);
            return;
        }

        // The substage's last question just finished its 5s answer reveal, so the substage itself has
        // ended: show the ranking for 10s (D-3) rather than advancing now. The advance is deferred to
        // the coordinator, fired by the worker when that window elapses. This is the behaviour change
        // D-3 names — advance used to be immediate here. The two reveals nest: 5s answer, then 10s
        // ranking, then the next substage.
        await _substageAdvanceCoordinator.BeginRankingRevealAsync(session, now, cancellationToken);
    }
}
