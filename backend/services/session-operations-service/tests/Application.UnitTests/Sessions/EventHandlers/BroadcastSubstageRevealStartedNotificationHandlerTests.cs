using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

/// <summary>
/// The reveal push is keyed off the domain event, not off the coordinator, so a treasure hunt cleared
/// deep in <c>RegisterTargetScan</c> reaches clients too. An earlier draft broadcast from the
/// coordinator and left exactly that mode — the one D-1 exists for — with no reveal on screen.
/// </summary>
public sealed class BroadcastSubstageRevealStartedNotificationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SubstagePlayMode.TreasureHunt, "TreasureHunt", true)]
    [InlineData(SubstagePlayMode.Trivia, "Trivia", false)]
    public async Task Handle_BroadcastsTheRevealForEitherPlayMode(
        SubstagePlayMode playMode,
        string expectedPlayMode,
        bool isTerminal)
    {
        var liveSessionId = Guid.NewGuid();
        var substageId = Guid.NewGuid();
        var revealUntil = Now.AddSeconds(10);
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var handler = new BroadcastSubstageRevealStartedNotificationHandler(broadcaster.Object);

        await handler.Handle(
            new SubstageRevealStartedEvent(liveSessionId, substageId, playMode, revealUntil, isTerminal, Now),
            CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastSubstageRankingRevealStartedAsync(
                It.Is<SubstageRankingRevealStartedNotificationDto>(notification =>
                    notification.LiveSessionId == liveSessionId &&
                    notification.SubstageSnapshotId == substageId &&
                    notification.PlayMode == expectedPlayMode &&
                    notification.RevealUntil == revealUntil &&
                    notification.IsTerminal == isTerminal &&
                    notification.EmittedAt == Now),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
