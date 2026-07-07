using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetParticipantSessionTimerSnapshot;

// Participant timer query returns the active-substage (trivia-question) window (HU-22) under the
// unchanged participant-membership authorization guard: allowed reads see the active-question
// remaining, no-active-question reads see 0 with no ActiveQuestion, denied/missing still throw.
public sealed class GetParticipantSessionTimerSnapshotQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivatedAt = StartsAt.AddMinutes(2);

    [Fact]
    public async Task Handle_WhenTriviaQuestionIsActive_ReturnsActiveSubstageRemaining()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(5));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, "join-token"),
            CancellationToken.None);

        result.TeamId.Should().Be(teamId);
        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(30);
        result.RemainingSeconds.Should().Be(25);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.AdvancingSince.Should().Be(ActivatedAt);

        result.ActiveQuestion.Should().NotBeNull();
        result.ActiveQuestion!.QuestionIndex.Should().Be(0);
        result.ActiveQuestion.TimeLimitSeconds.Should().Be(30);
        result.ActiveQuestion.RemainingSeconds.Should().Be(25);
    }

    [Fact]
    public async Task Handle_WhenNoQuestionIsActive_ReturnsNoCountdownAndNoActiveQuestion()
    {
        var session = CreateActiveTreasureHunt(activeAt: StartsAt.AddMinutes(1));
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: StartsAt.AddMinutes(4));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.TotalSeconds.Should().Be(0);
        result.RemainingSeconds.Should().Be(0);
        result.IsAdvancing.Should().BeFalse();
        result.ActiveQuestion.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPaused_FreezesActiveQuestionRemainder()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        session.MoveTo(SessionState.Paused, ActivatedAt.AddSeconds(10), new SessionStateTransitionPolicy());
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(50));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Paused));
        result.RemainingSeconds.Should().Be(20);
        result.TimerStatus.Should().Be("Frozen");
        result.IsAdvancing.Should().BeFalse();
        result.ActiveQuestion!.RemainingSeconds.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WhenMembershipAccessIsDenied_ThrowsForbidden()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository,
            CreateAccessClient(session.LiveSessionId, teamId, isAllowed: false),
            ActivatedAt.AddSeconds(5));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLiveSessionDoesNotExist_ThrowsNotFound()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var handler = CreateHandler(
            repository,
            CreateAccessClient(liveSessionId, teamId, isAllowed: true),
            StartsAt.AddMinutes(2));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(liveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        LiveSession session,
        Guid teamId,
        bool isAllowed,
        DateTimeOffset observedAt)
    {
        return CreateHandler(
            CreateRepository(session),
            CreateAccessClient(session.LiveSessionId, teamId, isAllowed),
            observedAt);
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IParticipantMembershipAccessClient> accessClient,
        DateTimeOffset observedAt)
    {
        return new GetParticipantSessionTimerSnapshotQueryHandler(
            repository.Object,
            accessClient.Object,
            new FixedTimeProvider(observedAt));
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return repository;
    }

    private static Mock<IParticipantMembershipAccessClient> CreateAccessClient(
        Guid liveSessionId,
        Guid teamId,
        bool isAllowed)
    {
        var accessClient = new Mock<IParticipantMembershipAccessClient>();
        accessClient
            .Setup(client => client.ValidateAsync(liveSessionId, teamId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantMembershipAccessDecisionDto(
                "ParticipantExperience",
                isAllowed,
                isAllowed ? "eligible" : "users-unavailable",
                isAllowed ? "allowed" : "denied",
                liveSessionId,
                teamId));

        return accessClient;
    }

    private static LiveSession CreateActiveTriviaSession(DateTimeOffset activatedAt)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(scheduledAt: StartsAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activatedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activatedAt.AddSeconds(-1), policy);
        session.ActivateQuestion(0, activatedAt);

        return session;
    }

    private static LiveSession CreateActiveTreasureHunt(DateTimeOffset activeAt)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(scheduledAt: StartsAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activeAt, policy);

        return session;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
