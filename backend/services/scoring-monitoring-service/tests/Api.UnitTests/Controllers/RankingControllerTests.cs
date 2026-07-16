using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Controllers;
using umbral_backend.Application.Rankings.Queries.GetOperatorRankingSnapshot;
using umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Controllers;

public sealed class RankingControllerTests
{
    [Fact]
    public async Task GetRankingAsync_WhenRankingExists_ReturnsOkWithSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var snapshot = new RankingSnapshotDto(
            liveSessionId,
            DateTimeOffset.UtcNow,
            1,
            new[]
            {
                new RankingRowDto(Guid.NewGuid(), "Alpha", 1, 100, TimeSpan.FromMinutes(5)),
                new RankingRowDto(Guid.NewGuid(), "Beta", 2, 80, TimeSpan.FromMinutes(7))
            });

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetRankingSnapshotQuery>(q => q.LiveSessionId == liveSessionId && q.TeamId == teamId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var controller = new RankingController(sender.Object);

        var result = await controller.GetRankingAsync(liveSessionId, teamId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RankingSnapshotDto>().Subject;
        response.LiveSessionId.Should().Be(liveSessionId);
        response.Rows.Should().HaveCount(2);
        response.Rows[0].Position.Should().Be(1);
        response.Rows[1].Position.Should().Be(2);
    }

    [Fact]
    public async Task GetRankingAsync_WhenRankingDoesNotExist_ReturnsOkWithEmptySnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var emptySnapshot = RankingSnapshotDto.Empty(liveSessionId);

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetRankingSnapshotQuery>(q => q.LiveSessionId == liveSessionId && q.TeamId == teamId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptySnapshot);

        var controller = new RankingController(sender.Object);

        var result = await controller.GetRankingAsync(liveSessionId, teamId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RankingSnapshotDto>().Subject;
        response.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOperatorRankingAsync_SendsTeamlessQueryAndReturnsOkWithSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var snapshot = new RankingSnapshotDto(
            liveSessionId,
            DateTimeOffset.UtcNow,
            1,
            new[]
            {
                new RankingRowDto(Guid.NewGuid(), "Alpha", 1, 100, TimeSpan.FromMinutes(5)),
                new RankingRowDto(Guid.NewGuid(), "Beta", 2, 80, TimeSpan.FromMinutes(7))
            });

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetOperatorRankingSnapshotQuery>(q => q.LiveSessionId == liveSessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var controller = new RankingController(sender.Object);

        var result = await controller.GetOperatorRankingAsync(liveSessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RankingSnapshotDto>().Subject;
        response.LiveSessionId.Should().Be(liveSessionId);
        response.Rows.Should().HaveCount(2);
    }
}
