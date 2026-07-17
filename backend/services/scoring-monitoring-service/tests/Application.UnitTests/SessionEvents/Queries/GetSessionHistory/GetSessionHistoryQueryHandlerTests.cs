using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.SessionEvents.Queries.GetSessionHistory;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Domain.Entities;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.SessionEvents.Queries.GetSessionHistory;

public sealed class GetSessionHistoryQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenAccessAllowed_ReturnsChronologicalHistory()
    {
        var liveSessionId = Guid.NewGuid();
        var externalActorId = Guid.NewGuid();
        var earlier = SessionEvent.ForStateChange(
            liveSessionId,
            Domain.Enums.SessionState.Scheduled,
            Domain.Enums.SessionState.Preparing,
            new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            externalActorId,
            "Preparing");
        var later = SessionEvent.ForResultsFinalized(
            liveSessionId,
            new DateTimeOffset(2026, 7, 16, 13, 0, 0, TimeSpan.Zero));
        var repository = new Mock<ISessionEventHistoryRepository>();
        repository
            .Setup(current => current.GetBySessionAsync(liveSessionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { earlier, later });
        var accessResolver = new Mock<IScoringSessionAccessResolver>();

        var result = await new GetSessionHistoryQueryHandler(repository.Object, accessResolver.Object)
            .Handle(new GetSessionHistoryQuery(liveSessionId, null), CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionId);
        result.Events.Select(row => row.OccurredAt).Should().BeInAscendingOrder();
        result.Events[0].ResponsibleUserExternalId.Should().Be(externalActorId);
        accessResolver.Verify(current => current.EnsureAccessAsync(liveSessionId, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionHasNoHistory_ReturnsEmptyHistory()
    {
        var liveSessionId = Guid.NewGuid();
        var repository = new Mock<ISessionEventHistoryRepository>();
        repository
            .Setup(current => current.GetBySessionAsync(liveSessionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SessionEvent>());

        var result = await new GetSessionHistoryQueryHandler(
                repository.Object,
                Mock.Of<IScoringSessionAccessResolver>())
            .Handle(new GetSessionHistoryQuery(liveSessionId, null), CancellationToken.None);

        result.LiveSessionId.Should().Be(liveSessionId);
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAccessDenied_DoesNotReadHistory()
    {
        var liveSessionId = Guid.NewGuid();
        var repository = new Mock<ISessionEventHistoryRepository>();
        var accessResolver = new Mock<IScoringSessionAccessResolver>();
        accessResolver
            .Setup(current => current.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var act = () => new GetSessionHistoryQueryHandler(repository.Object, accessResolver.Object)
            .Handle(new GetSessionHistoryQuery(liveSessionId, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(
            current => current.GetBySessionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
