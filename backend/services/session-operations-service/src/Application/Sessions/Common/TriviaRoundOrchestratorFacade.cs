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

        if (!session.ActiveQuestionIndex.HasValue)
        {
            return;
        }

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

        session.MoveTo(SessionState.Finished, now, _transitionPolicy);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);
    }

    private async Task ActivateQuestionAsync(
        LiveSession session,
        int questionIndex,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        session.ActivateQuestion(questionIndex, now);
        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        var question = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .OrderBy(snapshot => snapshot.SequenceOrder)
            .ElementAt(questionIndex);
        var options = question.Options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => option.OptionText)
            .ToArray();

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
