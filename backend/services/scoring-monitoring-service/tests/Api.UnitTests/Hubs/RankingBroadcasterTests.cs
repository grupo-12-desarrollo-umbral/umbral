using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Hubs;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Hubs;

public sealed class RankingBroadcasterTests
{
    [Fact]
    public async Task RankingChanged_SendsToCorrectSessionGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var snapshot = new RankingSnapshotDto(
            liveSessionId,
            DateTimeOffset.UtcNow,
            1,
            Array.Empty<RankingRowDto>());

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

        var broadcaster = new RankingBroadcaster(hubContext.Object);

        await broadcaster.RankingChanged(liveSessionId, snapshot, CancellationToken.None);

        clientProxy.Verify(
            p => p.SendCoreAsync(
                "RankingChanged",
                It.Is<object[]>(args => args.Length == 1 && args[0] is RankingSnapshotDto),
                CancellationToken.None),
            Times.Once);
    }
}
