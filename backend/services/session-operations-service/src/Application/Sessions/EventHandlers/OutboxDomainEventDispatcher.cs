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
    private readonly PublishEvidenceSubmissionAcceptedIntegrationEventHandler _evidenceSubmissionAccepted;
    private readonly PublishEvidenceSubmissionRejectedIntegrationEventHandler _evidenceSubmissionRejected;
    private readonly PublishQuestionClosedIntegrationEventHandler _questionClosed;
    private readonly PublishSessionResultsFinalizedIntegrationEventHandler _sessionResultsFinalized;
    private readonly PublishSessionStateChangedIntegrationEventHandler _sessionStateChanged;
    private readonly PublishTargetResolvedIntegrationEventHandler _targetResolved;

    public OutboxDomainEventDispatcher(
        PublishAnswerRegisteredIntegrationEventHandler answerRegistered,
        PublishEvidenceSubmissionRegisteredIntegrationEventHandler evidenceSubmissionRegistered,
        PublishEvidenceSubmissionAcceptedIntegrationEventHandler evidenceSubmissionAccepted,
        PublishEvidenceSubmissionRejectedIntegrationEventHandler evidenceSubmissionRejected,
        PublishQuestionClosedIntegrationEventHandler questionClosed,
        PublishSessionResultsFinalizedIntegrationEventHandler sessionResultsFinalized,
        PublishSessionStateChangedIntegrationEventHandler sessionStateChanged,
        PublishTargetResolvedIntegrationEventHandler targetResolved)
    {
        _answerRegistered = answerRegistered;
        _evidenceSubmissionRegistered = evidenceSubmissionRegistered;
        _evidenceSubmissionAccepted = evidenceSubmissionAccepted;
        _evidenceSubmissionRejected = evidenceSubmissionRejected;
        _questionClosed = questionClosed;
        _sessionResultsFinalized = sessionResultsFinalized;
        _sessionStateChanged = sessionStateChanged;
        _targetResolved = targetResolved;
    }

    public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        AnswerRegisteredEvent answerRegistered => _answerRegistered.Handle(answerRegistered, cancellationToken),
        EvidenceSubmissionRegisteredEvent evidenceSubmissionRegistered =>
            _evidenceSubmissionRegistered.Handle(evidenceSubmissionRegistered, cancellationToken),
        EvidenceSubmissionAcceptedEvent evidenceSubmissionAccepted =>
            _evidenceSubmissionAccepted.Handle(evidenceSubmissionAccepted, cancellationToken),
        EvidenceSubmissionRejectedEvent evidenceSubmissionRejected =>
            _evidenceSubmissionRejected.Handle(evidenceSubmissionRejected, cancellationToken),
        QuestionClosedEvent questionClosed => _questionClosed.Handle(questionClosed, cancellationToken),
        TargetResolvedEvent targetResolved => _targetResolved.Handle(targetResolved, cancellationToken),
        SessionStateChangedEvent sessionStateChanged => DispatchSessionStateChangedAsync(
            sessionStateChanged,
            cancellationToken),
        _ => Task.CompletedTask,
    };

    private async Task DispatchSessionStateChangedAsync(
        SessionStateChangedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _sessionResultsFinalized.Handle(domainEvent, cancellationToken);
        await _sessionStateChanged.Handle(domainEvent, cancellationToken);
    }
}
