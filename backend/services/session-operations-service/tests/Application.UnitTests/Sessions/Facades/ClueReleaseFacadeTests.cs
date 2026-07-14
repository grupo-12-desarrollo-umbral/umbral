using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.ReleaseClue;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class ClueReleaseFacadeTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private const int OperatorUserId = 42;

    [Fact]
    public async Task ReleaseCluesAsync_WhenTeamIsSpecified_ReleasesOnlyThatTeamAndPersists()
    {
        var session = CreateActiveSession(out var targetId, out var alphaId, out var bravoId);
        var repository = CreateRepository(session);
        var resolver = CreateResolver(session);
        var facade = CreateFacade(resolver.Object, repository.Object);
        var activeSubstageId = session.ActiveSubstageId;

        var result = await facade.ReleaseCluesAsync(
            new(session.LiveSessionId, targetId, null, alphaId), CancellationToken.None);

        result.TargetId.Should().Be(targetId);
        result.ClueId.Should().BeNull();
        result.ReleasedTeamIds.Should().Equal(alphaId);
        session.GetClueReleaseRecords().Should().ContainSingle(record => record.TeamId == alphaId && record.ReleasedByUserId == OperatorUserId && record.ReleasedAt == Now);
        session.GetClueReleaseRecords().Should().NotContain(record => record.TeamId == bravoId);
        session.ActiveSubstageId.Should().Be(activeSubstageId);
        session.ProjectParticipantTeamBoard(alphaId, Now).ActiveSubstageContext!.ResolvedTargets.Should().Be(0);
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
        resolver.Verify(x => x.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseCluesAsync_WhenTeamIdIsNull_ReleasesAllTeamsAndPersistsOnce()
    {
        var session = CreateActiveSession(out var targetId, out var alphaId, out var bravoId);
        var repository = CreateRepository(session);
        var facade = CreateFacade(CreateResolver(session).Object, repository.Object);

        var result = await facade.ReleaseCluesAsync(
            new(session.LiveSessionId, targetId, null, null), CancellationToken.None);

        result.ReleasedTeamIds.Should().BeEquivalentTo([alphaId, bravoId]);
        session.GetClueReleaseRecords().Select(record => record.TeamId).Should().BeEquivalentTo([alphaId, bravoId]);
        session.DomainEvents.OfType<ClueReleasedEvent>().Should().HaveCount(2);
        repository.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseCluesAsync_WhenClueIdIsProvided_ConstructsTriviaClueSubject()
    {
        var session = CreateActiveTriviaSession(out var clueId, out var teamId);
        var repository = CreateRepository(session);
        var facade = CreateFacade(CreateResolver(session).Object, repository.Object);

        var result = await facade.ReleaseCluesAsync(
            new(session.LiveSessionId, null, clueId, teamId),
            CancellationToken.None);

        result.TargetId.Should().BeNull();
        result.ClueId.Should().Be(clueId);
        session.GetClueReleaseRecords().Should().ContainSingle(record =>
            record.TargetId == null && record.ClueId == clueId && record.TeamId == teamId);
        repository.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseCluesAsync_WhenReleaseIsDuplicate_PropagatesDomainRejectionWithoutPersisting()
    {
        var session = CreateActiveSession(out var targetId, out var alphaId, out _);
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targetId), alphaId, OperatorUserId, Now.AddMinutes(-1));
        var repository = CreateRepository(session);
        var facade = CreateFacade(CreateResolver(session).Object, repository.Object);

        var act = async () => await facade.ReleaseCluesAsync(
            new(session.LiveSessionId, targetId, null, alphaId), CancellationToken.None);

        await act.Should().ThrowAsync<ClueAlreadyReleasedToTeamException>();
        repository.Verify(x => x.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseCluesAsync_WhenOperatorDoesNotOwnSession_ProxyRejectsBeforeMutation()
    {
        var session = CreateActiveSession(out var targetId, out var alphaId, out _);
        var repository = CreateRepository(session);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-operator-99");
        currentUser.SetupGet(user => user.Role).Returns("Operator");
        var actorClient = new Mock<IAuthenticatedActorProfileAccessClient>();
        actorClient.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AuthenticatedActorProfileLookupDto(99, "kc-operator-99", "Operator", true));
        ISessionAdministrationAccessResolver proxy = new SessionAdministrationAuthorizationProxy(currentUser.Object, repository.Object, actorClient.Object);
        var facade = CreateFacade(proxy, repository.Object);

        var act = async () => await facade.ReleaseCluesAsync(
            new(session.LiveSessionId, targetId, null, alphaId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        session.GetClueReleaseRecords().Should().BeEmpty();
        repository.Verify(x => x.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ClueReleaseFacade CreateFacade(ISessionAdministrationAccessResolver resolver, ILiveSessionRepository repository)
        => new(resolver, repository, new FixedTimeProvider(Now));

    private static Mock<ISessionAdministrationAccessResolver> CreateResolver(LiveSession session)
    {
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver.Setup(x => x.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        return resolver;
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(x => x.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        repository.Setup(x => x.UpdateAsync(session, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repository;
    }

    private static LiveSession CreateActiveSession(out Guid targetId, out Guid alphaId, out Guid bravoId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var target = TargetSnapshot.Create(substage.SubstageSnapshotId, "Main Exhibit", "QR-HIDDEN", 1, true, 100, 4.711, -74.0721, "Look near the entrance.", "HiddenUntilOperatorRelease");
        var snapshot = MissionRuntimeSnapshot.Create(Guid.NewGuid(), "Museum Hunt", MaximumTime.Create(45), [StageSnapshot.Create("Stage One", 1, [substage])], [target], []);
        var session = LiveSession.Create(SessionSource.Create(snapshot.SourceMissionId), "clue-2", "Museum Hunt", 45, Now.AddHours(-1), snapshot);
        session.AssignOperator(OperatorUserId, Now.AddMinutes(-10));
        var alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        targetId = target.TargetSnapshotId;
        alphaId = alpha.TeamId;
        bravoId = bravo.TeamId;
        return session;
    }

    private static LiveSession CreateActiveTriviaSession(out Guid clueId, out Guid teamId)
    {
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var question = TriviaQuestionSnapshot.Create(
            substage.SubstageSnapshotId,
            "Which planet is closest to the Sun?",
            1,
            100,
            30,
            "Mercury is closest.",
            [
                TriviaOptionSnapshot.Create("Mercury", 1, true),
                TriviaOptionSnapshot.Create("Venus", 2, false)
            ]);
        var clue = ClueSnapshot.Create(
            substage.SubstageSnapshotId,
            "Operator-only clue.",
            ClueSnapshot.HiddenUntilOperatorReleasePolicy,
            1);
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Trivia Mission",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [],
            [question],
            [clue]);
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "trivia-clue",
            "Trivia Mission",
            20,
            Now.AddHours(-1),
            snapshot);
        session.AssignOperator(OperatorUserId, Now.AddMinutes(-10));
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        clueId = clue.ClueSnapshotId;
        teamId = team.TeamId;
        return session;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
