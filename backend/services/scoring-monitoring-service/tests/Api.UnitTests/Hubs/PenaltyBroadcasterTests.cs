using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Dtos.Scores;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Hubs;

public sealed class PenaltyBroadcasterTests
{
    [Fact]
    public async Task PenaltyApplied_SendsToCorrectSessionGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var notification = new PenaltyAppliedNotificationDto(
            liveSessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100,
            "Unsportsmanlike conduct",
            DateTimeOffset.UtcNow);

        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        var hubContext = new Mock<IHubContext<ScoringHub>>();

        clientProxy
            .Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubClients
            .Setup(clients => clients.Group($"live-session:{liveSessionId:D}"))
            .Returns(clientProxy.Object);

        hubContext
            .Setup(ctx => ctx.Clients)
            .Returns(hubClients.Object);

        var broadcaster = new PenaltyBroadcaster(hubContext.Object);

        await broadcaster.PenaltyApplied(liveSessionId, notification, CancellationToken.None);

        clientProxy.Verify(
            p => p.SendCoreAsync(
                "PenaltyApplied",
                It.Is<object[]>(args => args.Length == 1 && args[0] is PenaltyAppliedNotificationDto),
                CancellationToken.None),
            Times.Once);
    }
}
