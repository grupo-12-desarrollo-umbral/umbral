using Microsoft.Extensions.Logging.Abstractions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// The RabbitMQ bridge for the accepted-answer fact. It only ever runs off AnswerRegisteredEvent,
// which the domain raises exclusively on the accept path (so publish is success-only by construction).
// Correctness/score DO ride this contract for downstream scoring; broker failures are swallowed.
public sealed class PublishAnswerRegisteredIntegrationEventHandlerTests
{
    private static AnswerRegisteredEvent Event() => new(
        liveSessionId: Guid.NewGuid(),
        teamId: Guid.NewGuid(),
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
        var publisher = new FakeIntegrationEventPublisher();
        var handler = new PublishAnswerRegisteredIntegrationEventHandler(publisher, NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance);
        var domainEvent = Event();

        await handler.Handle(domainEvent, CancellationToken.None);

        var published = publisher.Published.OfType<AnswerRegisteredIntegrationEvent>().Single();
        published.LiveSessionId.Should().Be(domainEvent.LiveSessionId);
        published.TeamId.Should().Be(domainEvent.TeamId);
        published.TriviaAnswerSubmissionId.Should().Be(domainEvent.EvidenceSubmissionId);
        published.TriviaSubstageSnapshotId.Should().Be(domainEvent.ActiveSubstageId);
        published.QuestionSequenceOrder.Should().Be(1);
        published.SelectedOptionSequenceOrder.Should().Be(2);
        published.IsCorrect.Should().BeTrue();
        published.ScoreValue.Should().Be(100);
        published.SubmittedAt.Should().Be(domainEvent.SubmittedAt);
    }

    [Fact]
    public async Task Handle_WhenBrokerFails_SwallowsSoRuntimeNeverFaults()
    {
        var publisher = new FakeIntegrationEventPublisher(throwOnPublish: true);
        var handler = new PublishAnswerRegisteredIntegrationEventHandler(publisher, NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance);

        var act = async () => await handler.Handle(Event(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
