using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// The operator-only SignalR bridge for the answered indicator. It maps the accepted-answer fact to a
// DTO that OMITS correctness/points/selected option, and routes through the operator-only broadcaster
// seam so participants never receive it.
public sealed class TeamAnsweredNotificationHandlerTests
{
    [Fact]
    public async Task Handle_BroadcastsAnsweredIndicatorWithoutCorrectnessScoreOrOption()
    {
        var broadcaster = new Mock<ITeamAnsweredBroadcaster>();
        TeamAnsweredNotificationDto? captured = null;
        broadcaster
            .Setup(b => b.BroadcastTeamAnsweredAsync(It.IsAny<TeamAnsweredNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<TeamAnsweredNotificationDto, CancellationToken>((dto, _) => captured = dto)
            .Returns(Task.CompletedTask);
        var handler = new TeamAnsweredNotificationHandler(broadcaster.Object);

        var domainEvent = new AnswerRegisteredEvent(
            liveSessionId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            evidenceSubmissionId: Guid.NewGuid(),
            activeSubstageId: Guid.NewGuid(),
            questionSequenceOrder: 3,
            selectedOptionSequenceOrder: 2,
            isCorrect: true,
            scoreValue: 100,
            submittedAt: new DateTimeOffset(2026, 6, 3, 10, 1, 5, TimeSpan.Zero));

        await handler.Handle(domainEvent, CancellationToken.None);

        broadcaster.Verify(b => b.BroadcastTeamAnsweredAsync(It.IsAny<TeamAnsweredNotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.LiveSessionId.Should().Be(domainEvent.LiveSessionId);
        captured.TeamId.Should().Be(domainEvent.TeamId);
        captured.TriviaSubstageSnapshotId.Should().Be(domainEvent.ActiveSubstageId);
        captured.QuestionSequenceOrder.Should().Be(3);
        captured.AnsweredAt.Should().Be(domainEvent.SubmittedAt);
    }

    [Fact]
    public void Dto_HasNoCorrectnessScoreOrSelectedOptionSurface()
    {
        var properties = typeof(TeamAnsweredNotificationDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        properties.Should().NotContain(new[] { "IsCorrect", "ScoreValue", "SelectedOptionSequenceOrder" });
    }
}
