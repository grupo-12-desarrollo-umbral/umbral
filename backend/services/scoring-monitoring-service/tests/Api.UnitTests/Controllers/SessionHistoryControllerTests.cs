using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Controllers;
using umbral_backend.Application.Dtos.SessionEvents;
using umbral_backend.Application.SessionEvents.Queries.GetSessionHistory;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Controllers;

public sealed class SessionHistoryControllerTests
{
    [Fact]
    public async Task GetHistoryAsync_SendsOptionalTeamFilterAndReturnsOk()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var history = SessionHistoryDto.Empty(liveSessionId);
        var sender = new Mock<ISender>();
        sender
            .Setup(current => current.Send(
                It.Is<GetSessionHistoryQuery>(query =>
                    query.LiveSessionId == liveSessionId && query.TeamId == teamId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var result = await new SessionHistoryController(sender.Object)
            .GetHistoryAsync(liveSessionId, teamId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(history);
    }
}
