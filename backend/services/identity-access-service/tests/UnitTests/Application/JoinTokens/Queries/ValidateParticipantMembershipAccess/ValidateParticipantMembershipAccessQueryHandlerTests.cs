using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public sealed class ValidateParticipantMembershipAccessQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllowedDecisionForParticipantMembership()
    {
        var actor = CreateUser(15, "kc-participant", Role.Participant);
        var team = CreateTeamWithParticipant(actor.Id);
        var handler = CreateHandler(actor, team, CreateJoinTokenRepository(), CreateTokenService(), TimeProvider.System);
        var query = new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Capability.Should().Be(nameof(ProtectedCapability.ParticipantExperience));
        result.IsAllowed.Should().BeTrue();
        result.LiveSessionId.Should().Be(query.LiveSessionId);
        result.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public async Task Handle_RejectsNonParticipantActor()
    {
        var actor = CreateUser(16, "kc-operator", Role.Operator);
        var team = CreateTeamWithParticipant(actor.Id);
        var handler = CreateHandler(actor, team, CreateJoinTokenRepository(), CreateTokenService(), TimeProvider.System);

        var act = async () => await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<umbral_backend.Domain.Exceptions.UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_RejectsWhenParticipantHasNoMembership()
    {
        var actor = CreateUser(17, "kc-participant-no-membership", Role.Participant);
        var team = Team.Register("Blue Team", "BLUE-01");
        var handler = CreateHandler(actor, team, CreateJoinTokenRepository(), CreateTokenService(), TimeProvider.System);

        var act = async () => await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_RejectsWhenParticipantTargetsAnotherTeam()
    {
        var actor = CreateUser(18, "kc-participant-other-team", Role.Participant);
        var ownTeam = CreateTeamWithParticipant(actor.Id);
        var otherTeam = Team.Register("Green Team", "GREEN-01");
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(ownTeam.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownTeam);
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(otherTeam.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherTeam);

        var handler = CreateHandler(
            actor,
            teamRepository,
            CreateJoinTokenRepository(),
            CreateTokenService(),
            TimeProvider.System);

        var act = async () => await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), otherTeam.TeamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionForExpiredToken()
    {
        var actor = CreateUser(19, "kc-expired", Role.Participant);
        var team = CreateTeamWithParticipant(actor.Id);
        var query = new ValidateParticipantMembershipAccessQuery(
            new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            team.TeamId,
            "plain-token");
        var joinTokenRepository = CreateJoinTokenRepository(CreateExpiredToken(query.LiveSessionId, query.TeamId));
        var handler = CreateHandler(
            actor,
            team,
            joinTokenRepository,
            CreateTokenService(),
            new TestTimeProvider(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(query, CancellationToken.None);

        result.Capability.Should().Be(nameof(ProtectedCapability.ParticipantExperience));
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionForConsumedToken()
    {
        var actor = CreateUser(20, "kc-consumed", Role.Participant);
        var team = CreateTeamWithParticipant(actor.Id);
        var query = new ValidateParticipantMembershipAccessQuery(
            new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            team.TeamId,
            "plain-token");
        var joinTokenRepository = CreateJoinTokenRepository(CreateConsumedToken(query.LiveSessionId, query.TeamId));
        var handler = CreateHandler(
            actor,
            team,
            joinTokenRepository,
            CreateTokenService(),
            new TestTimeProvider(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(query, CancellationToken.None);

        result.Capability.Should().Be(nameof(ProtectedCapability.ParticipantExperience));
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("Consumed");
    }

    private static ValidateParticipantMembershipAccessQueryHandler CreateHandler(
        User actor,
        Team team,
        Mock<IJoinTokenRepository> joinTokenRepository,
        Mock<IJoinTokenTokenService> tokenService,
        TimeProvider timeProvider)
    {
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        return CreateHandler(actor, teamRepository, joinTokenRepository, tokenService, timeProvider);
    }

    private static ValidateParticipantMembershipAccessQueryHandler CreateHandler(
        User actor,
        Mock<ITeamRepository> teamRepository,
        Mock<IJoinTokenRepository> joinTokenRepository,
        Mock<IJoinTokenTokenService> tokenService,
        TimeProvider timeProvider)
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repository => repository.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(actor.ExternalIdentityId);

        var executor = new ParticipantMembershipAccessValidationService(
            joinTokenRepository.Object,
            tokenService.Object,
            new JoinTokenPolicy(),
            timeProvider);

        var proxy = new ParticipantMembershipAccessAuthorizationProxy(
            userRepository.Object,
            teamRepository.Object,
            currentUser.Object,
            new AccessPolicy(),
            executor);

        return new ValidateParticipantMembershipAccessQueryHandler(proxy);
    }

    private static Mock<IJoinTokenRepository> CreateJoinTokenRepository(JoinToken? joinToken = null)
    {
        var repository = new Mock<IJoinTokenRepository>();
        repository
            .Setup(repo => repo.GetByTokenHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(joinToken);
        return repository;
    }

    private static Mock<IJoinTokenTokenService> CreateTokenService()
    {
        var tokenService = new Mock<IJoinTokenTokenService>();
        tokenService.Setup(service => service.HashToken("plain-token")).Returns("hashed-token");
        return tokenService;
    }

    private static Team CreateTeamWithParticipant(int participantId)
    {
        var team = Team.Register("Red Team", "RED-01");
        team.AssignParticipant(participantId);
        return team;
    }

    private static JoinToken CreateExpiredToken(Guid liveSessionId, Guid teamId)
    {
        return JoinToken.Issue(
            liveSessionId,
            teamId,
            "hashed-token",
            new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 11, 30, 0, TimeSpan.Zero),
            100,
            new JoinTokenPolicy());
    }

    private static JoinToken CreateConsumedToken(Guid liveSessionId, Guid teamId)
    {
        var joinToken = JoinToken.Issue(
            liveSessionId,
            teamId,
            "hashed-token",
            new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 12, 30, 0, TimeSpan.Zero),
            101,
            new JoinTokenPolicy());

        joinToken.Consume(new DateTimeOffset(2026, 6, 2, 11, 15, 0, TimeSpan.Zero), new JoinTokenPolicy());
        return joinToken;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
