using MassTransit;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Application.Scores.Consumers;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Consumers;

public sealed class TargetResolvedConsumerTests
{
    [Fact]
    public async Task Consume_WhenTargetResolved_SendsRecordScoreEntryCommand()
    {
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<TargetResolvedIntegrationEvent>>();
        var message = new TargetResolvedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Gilded Owls",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            200,
            3,
            new DateTimeOffset(2026, 7, 14, 18, 10, 0, TimeSpan.Zero));
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new TargetResolvedConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(
                It.Is<RecordScoreEntryCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.TeamId == message.ReferenceTeamId &&
                    command.TeamDisplayName == message.TeamDisplayName &&
                    command.ReasonCode == "treasure-target-resolved" &&
                    command.ScoreValue == message.ScoreValue &&
                    command.DifficultyFactor == message.DifficultyFactor &&
                    command.RecordedAt == message.ResolvedAt &&
                    command.SourceEntityType == ScoreSourceType.TargetResolution &&
                    command.SourceEntityId == message.EvidenceSubmissionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenTargetResolved_RecalculatesRankingSynchronously()
    {
        // The ranking is recomputed in the same consume that records the score, so a teammate's scan
        // moves the header immediately instead of lagging until the substage reveal force-refetches.
        var sender = new Mock<ISender>();
        var context = new Mock<ConsumeContext<TargetResolvedIntegrationEvent>>();
        var message = new TargetResolvedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Gilded Owls",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            200,
            3,
            new DateTimeOffset(2026, 7, 14, 18, 10, 0, TimeSpan.Zero));
        context.SetupGet(current => current.Message).Returns(message);
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        var consumer = new TargetResolvedConsumer(sender.Object);

        await consumer.Consume(context.Object);

        sender.Verify(
            current => current.Send(
                It.Is<RecalculateRankingCommand>(command =>
                    command.LiveSessionId == message.LiveSessionId &&
                    command.GeneratedAt == message.ResolvedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
