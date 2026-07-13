using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Transactional outbox router: maps each gameplay domain event to the integration publisher that
/// enqueues its contract into the bus outbox. Kept separate from the MediatR notification fan-out so
/// these publishes run pre-commit (atomic capture) while SignalR broadcasts run post-commit. An event
/// with no integration contract is a no-op.
/// </summary>
public sealed class OutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
{
    private readonly PublishAnswerRegisteredIntegrationEventHandler _answerRegistered;
    private readonly PublishEvidenceSubmissionRegisteredIntegrationEventHandler _evidenceSubmissionRegistered;
    private readonly PublishQuestionClosedIntegrationEventHandler _questionClosed;
    private readonly PublishSessionResultsFinalizedIntegrationEventHandler _sessionResultsFinalized;

    public OutboxDomainEventDispatcher(
        PublishAnswerRegisteredIntegrationEventHandler answerRegistered,
        PublishEvidenceSubmissionRegisteredIntegrationEventHandler evidenceSubmissionRegistered,
        PublishQuestionClosedIntegrationEventHandler questionClosed,
        PublishSessionResultsFinalizedIntegrationEventHandler sessionResultsFinalized)
    {
        _answerRegistered = answerRegistered;
        _evidenceSubmissionRegistered = evidenceSubmissionRegistered;
        _questionClosed = questionClosed;
        _sessionResultsFinalized = sessionResultsFinalized;
    }

    public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        AnswerRegisteredEvent answerRegistered => _answerRegistered.Handle(answerRegistered, cancellationToken),
        EvidenceSubmissionRegisteredEvent evidenceSubmissionRegistered =>
            _evidenceSubmissionRegistered.Handle(evidenceSubmissionRegistered, cancellationToken),
        QuestionClosedEvent questionClosed => _questionClosed.Handle(questionClosed, cancellationToken),
        SessionStateChangedEvent sessionStateChanged => _sessionResultsFinalized.Handle(sessionStateChanged, cancellationToken),
        _ => Task.CompletedTask,
    };
}
