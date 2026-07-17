using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Common;

public sealed class QuestionActivator : IQuestionActivator
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionQuestionBroadcaster _sessionQuestionBroadcaster;
    private readonly IQuestionActivationStrategy _questionActivationStrategy;

    public QuestionActivator(
        ILiveSessionRepository liveSessionRepository,
        ISessionQuestionBroadcaster sessionQuestionBroadcaster,
        IQuestionActivationStrategy questionActivationStrategy)
    {
        _liveSessionRepository = liveSessionRepository;
        _sessionQuestionBroadcaster = sessionQuestionBroadcaster;
        _questionActivationStrategy = questionActivationStrategy;
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

        // Null for a treasure-hunt substage (the strategy is substage-scoped and a hunt holds no
        // questions), which is how advancing into one activates nothing without a play-mode check here.
        var nextQuestionIndex = _questionActivationStrategy.Next(session);
        if (!nextQuestionIndex.HasValue)
        {
            return;
        }

        await ActivateQuestionAsync(session, nextQuestionIndex.Value, now, cancellationToken);
    }

    public async Task ActivateQuestionAsync(
        LiveSession session,
        int questionIndex,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

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
                now,
                session.ActiveSubstageId!.Value),
            cancellationToken);
    }
}
