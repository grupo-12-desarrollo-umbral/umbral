using MassTransit;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.SessionEvents.Common;
using umbral_backend.Application.SessionEvents.Consumers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.SessionEvents.Consumers;

public sealed class SessionEventHistoryConsumerTests
{
    [Fact]
    public async Task Consume_StateChange_AppendsMappedHistoryEvent()
    {
        var responsibleUserExternalId = Guid.NewGuid();
        var message = new SessionStateChangedIntegrationEvent(
            Guid.NewGuid(),
            SessionState.Scheduled,
            SessionState.Preparing,
            DateTimeOffset.UtcNow,
            responsibleUserExternalId,
            "Preparing");
        var repository = new Mock<ISessionEventHistoryRepository>();
        var context = ContextFor(message);

        await new SessionEventHistoryConsumer(repository.Object).Consume(context.Object);

        repository.Verify(current => current.AppendAsync(
            It.Is<SessionEvent>(sessionEvent =>
                sessionEvent.LiveSessionId == message.LiveSessionId
                && sessionEvent.EventType == SessionEvent.StateChangedEventType
                && sessionEvent.ResponsibleUserExternalId == responsibleUserExternalId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Consume_QuestionClosed_AppendsMappedHistoryEvent()
    {
        var message = new QuestionClosedIntegrationEvent(Guid.NewGuid(), 3, DateTimeOffset.UtcNow);
        var repository = new Mock<ISessionEventHistoryRepository>();
        var context = ContextFor(message);

        await new SessionEventHistoryConsumer(repository.Object).Consume(context.Object);

        repository.Verify(current => current.AppendAsync(
            It.Is<SessionEvent>(sessionEvent =>
                sessionEvent.LiveSessionId == message.LiveSessionId
                && sessionEvent.EventType == SessionEvent.QuestionClosedEventType
                && sessionEvent.PayloadSummary.Contains("3")),
            CancellationToken.None));
    }

    [Fact]
    public async Task Consume_ResultsFinalized_AppendsMappedHistoryEvent()
    {
        var message = new SessionResultsFinalizedIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var repository = new Mock<ISessionEventHistoryRepository>();
        var context = ContextFor(message);

        await new SessionEventHistoryConsumer(repository.Object).Consume(context.Object);

        repository.Verify(current => current.AppendAsync(
            It.Is<SessionEvent>(sessionEvent =>
                sessionEvent.LiveSessionId == message.LiveSessionId
                && sessionEvent.EventType == SessionEvent.ResultsFinalizedEventType),
            CancellationToken.None));
    }

    private static Mock<ConsumeContext<TMessage>> ContextFor<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}
