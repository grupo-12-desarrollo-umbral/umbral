using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.Common;

public sealed class RankingSessionMembershipGuardTests
{
    [Fact]
    public async Task EnsureAllowedAsync_WhenClientAllows_DoesNotThrow()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var client = new Mock<IParticipantSessionMembershipClient>();
        client
            .Setup(c => c.ValidateAsync(liveSessionId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantSessionMembershipDecisionDto(true, liveSessionId, teamId, "allowed"));

        var guard = new RankingSessionMembershipGuard(client.Object);

        var act = async () => await guard.EnsureAllowedAsync(liveSessionId, teamId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenClientDenies_ThrowsForbidden()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var client = new Mock<IParticipantSessionMembershipClient>();
        client
            .Setup(c => c.ValidateAsync(liveSessionId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantSessionMembershipDecisionDto(false, liveSessionId, teamId, "participant-not-in-session"));

        var guard = new RankingSessionMembershipGuard(client.Object);

        var act = async () => await guard.EnsureAllowedAsync(liveSessionId, teamId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
