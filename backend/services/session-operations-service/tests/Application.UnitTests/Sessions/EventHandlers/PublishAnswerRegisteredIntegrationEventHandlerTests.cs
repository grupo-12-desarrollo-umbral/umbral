using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// The RabbitMQ bridge for the accepted-answer fact. It only ever runs off AnswerRegisteredEvent,
// which the domain raises exclusively on the accept path (so publish is success-only by construction).
// Correctness/score DO ride this contract for downstream scoring. Under the transactional outbox the
// publish is a local insert on the business SaveChanges, so a failure is a DB fault and must propagate
// (rolling the transaction back), not be swallowed.
public sealed class PublishAnswerRegisteredIntegrationEventHandlerTests
{
    private static AnswerRegisteredEvent Event() => new(
        liveSessionId: Guid.NewGuid(),
        teamId: Guid.NewGuid(),
        referenceTeamId: Guid.NewGuid(),
        teamDisplayName: "Gilded Owls",
        evidenceSubmissionId: Guid.NewGuid(),
        activeSubstageId: Guid.NewGuid(),
        questionSequenceOrder: 1,
        selectedOptionSequenceOrder: 2,
        isCorrect: true,
        scoreValue: 100,
        submittedAt: new DateTimeOffset(2026, 6, 3, 10, 1, 5, TimeSpan.Zero));

    [Fact]
    public async Task Handle_PublishesAnswerRegisteredIntegrationEventCarryingCorrectnessAndScore()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var handler = NewHandler(publishEndpoint.Object);
        var domainEvent = Event();

        await handler.Handle(domainEvent, CancellationToken.None);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(
                new AnswerRegisteredIntegrationEvent(
                    domainEvent.LiveSessionId,
                    domainEvent.TeamId,
                    domainEvent.ReferenceTeamId,
                    domainEvent.TeamDisplayName,
                    domainEvent.EvidenceSubmissionId,
                    domainEvent.ActiveSubstageId,
                    1,
                    2,
                    true,
                    100,
                    domainEvent.SubmittedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenOutboxInsertFails_PropagatesToRollBackTheTransaction()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<AnswerRegisteredIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox insert failed"));
        var handler = NewHandler(publishEndpoint.Object);

        var act = async () => await handler.Handle(Event(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static PublishAnswerRegisteredIntegrationEventHandler NewHandler(IPublishEndpoint publishEndpoint)
        => new(publishEndpoint, NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance);
}
