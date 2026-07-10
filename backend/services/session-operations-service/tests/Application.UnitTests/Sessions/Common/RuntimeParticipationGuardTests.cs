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
    public async Task EnsureAllowedAsync_WhenUsersAllows_DoesNotBlockOrEvict()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, teamId, isAllowed: true, repository, notifier);

        await guard.EnsureAllowedAsync(session.LiveSessionId, teamId, "token", CancellationToken.None);

        session.Participants.Single().IsBlocked.Should().BeFalse();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyBlockedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAllowedAsync_WhenUsersDenies_BlocksPersistsEvictsAndThrows()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var participantId = session.Participants.Single().SessionParticipantId;
        var repository = new Mock<ILiveSessionRepository>();
        var notifier = new Mock<IParticipantBlockNotifier>();
        var guard = CreateGuard(session, teamId, isAllowed: false, repository, notifier);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, teamId, "token", CancellationToken.None);

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
        var guard = CreateGuard(session, teamId, isAllowed: false, repository, notifier);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, teamId, "token", CancellationToken.None);

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
        var guard = CreateGuard(session, teamId, isAllowed: false, repository, notifier, currentUserId: "not-a-guid");

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, teamId, "token", CancellationToken.None);

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
        var accessClient = new Mock<IParticipantMembershipAccessClient>();
        accessClient
            .Setup(client => client.ValidateAsync(session.LiveSessionId, teamId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantMembershipAccessDecisionDto(
                "ParticipantExperience",
                false,
                "user-access-deactivated",
                "denied",
                session.LiveSessionId,
                teamId));
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(ParticipantIdentity.ToString());
        var guard = new RuntimeParticipationGuard(
            accessClient.Object,
            repository.Object,
            currentUser.Object,
            notifier.Object,
            TimeProvider.System);

        var act = async () => await guard.EnsureAllowedAsync(session.LiveSessionId, teamId, "token", CancellationToken.None);

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
        Guid teamId,
        bool isAllowed,
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

        var accessClient = new Mock<IParticipantMembershipAccessClient>();
        accessClient
            .Setup(client => client.ValidateAsync(session.LiveSessionId, teamId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantMembershipAccessDecisionDto(
                "ParticipantExperience",
                isAllowed,
                isAllowed ? "eligible" : "user-access-deactivated",
                isAllowed ? "allowed" : "denied",
                session.LiveSessionId,
                teamId));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId ?? ParticipantIdentity.ToString());

        return new RuntimeParticipationGuard(
            accessClient.Object,
            repository.Object,
            currentUser.Object,
            notifier.Object,
            TimeProvider.System);
    }
}
