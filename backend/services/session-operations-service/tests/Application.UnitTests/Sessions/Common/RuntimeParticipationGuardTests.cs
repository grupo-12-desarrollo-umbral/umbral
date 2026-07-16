using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common;

// The synchronous fail-closed Participation Block (#91, Tier 1): an allowed re-check is a no-op;
// a denied one records the block on the SessionParticipant, evicts the live connection, rejects,
// and is idempotent across repeated denied polls.
public sealed class RuntimeParticipationGuardTests
{
    private static readonly Guid ParticipantIdentity = Guid.NewGuid();

    [Fact]
    public async Task EnsureAllowedAsync_WhenParticipantIsEligible_DoesNotBlockOrEvict()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, isEligible: true, repository, notifier);

        await guard.EnsureAllowedAsync(session.LiveSessionId, CancellationToken.None);

        session.Participants.Single().IsBlocked.Should().BeFalse();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenParticipantIsIneligible_BlocksPersistsEvictsAndThrows()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var participantId = session.Participants.Single().SessionParticipantId;
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, isEligible: false, repository, notifier);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        session.Participants.Single().ParticipantStatus.Should().Be(ParticipantStatus.Blocked);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyBlockedAsync(participantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenDeniedButAlreadyBlocked_IsIdempotent()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        session.Participants.Single().Block(DateTimeOffset.UtcNow);
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, isEligible: false, repository, notifier);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenDeniedAndCurrentUserUnresolvable_ThrowsWithoutTouchingSession()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, isEligible: false, repository, notifier, currentUserId: "not-a-guid");

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenDeniedAndSessionCannotBeLoaded_ThrowsWithoutPersistingOrNotifying()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var notifier = new Mock<IParticipantBlockNotifier>();
        var eligibleTeamsClient = new Mock<IParticipantEligibleTeamsClient>();
        eligibleTeamsClient
            .Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantEligibleTeamsDto(false, "user-access-deactivated", []));
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(ParticipantIdentity.ToString());
        var guard = new RuntimeParticipationGuard(
            eligibleTeamsClient.Object,
            repository.Object,
            currentUser.Object,
            notifier.Object,
            TimeProvider.System);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (LiveSession Session, Guid TeamId) CreateSessionWithParticipant()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        session.AdmitParticipant(
            ParticipantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());
        return (session, team.TeamId);
    }

    private static RuntimeParticipationGuard CreateGuard(
        LiveSession session,
        bool isEligible,
        Mock<ILiveSessionRepository> repository,
        Mock<IParticipantBlockNotifier> notifier,
        string? currentUserId = null)
    {
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifier
            .Setup(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eligibleTeamsClient = new Mock<IParticipantEligibleTeamsClient>();
        eligibleTeamsClient
            .Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantEligibleTeamsDto(
                isEligible,
                isEligible ? "eligible" : "user-access-deactivated",
                []));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId ?? ParticipantIdentity.ToString());

        return new RuntimeParticipationGuard(
            eligibleTeamsClient.Object,
            repository.Object,
            currentUser.Object,
            notifier.Object,
            TimeProvider.System);
    }
}
