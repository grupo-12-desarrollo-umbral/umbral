using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class JoinTokenRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public JoinTokenRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task AddAndReadBackJoinToken_PersistsConfiguredFields()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var joinToken = CreateJoinToken();

        await using var actContext = BuildContext();
        IJoinTokenRepository repository = new JoinTokenRepository(actContext);
        await repository.AddAsync(joinToken, CancellationToken.None);

        await using var assertContext = BuildContext();
        var persistedJoinToken = await new JoinTokenRepository(assertContext)
            .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

        persistedJoinToken.Should().NotBeNull();
        persistedJoinToken!.LiveSessionId.Should().Be(joinToken.LiveSessionId);
        persistedJoinToken.TeamId.Should().Be(joinToken.TeamId);
        persistedJoinToken.TokenHash.Should().Be(joinToken.TokenHash);
        persistedJoinToken.IssuedByUserId.Should().Be(joinToken.IssuedByUserId);
        persistedJoinToken.IssuedAt.Should().Be(joinToken.IssuedAt);
        persistedJoinToken.ExpiresAt.Should().Be(joinToken.ExpiresAt);
        persistedJoinToken.ConsumedAt.Should().BeNull();
        persistedJoinToken.Status.Should().Be(JoinTokenStatus.Active);
    }

    [Fact]
    public async Task ConsumeJoinToken_PersistsConsumedStateAndDispatchesDomainEvents()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var issuedMediator = new CapturingMediator();
        var joinToken = CreateJoinToken();

        await using var issueContext = BuildContext(issuedMediator);
        IJoinTokenRepository issueRepository = new JoinTokenRepository(issueContext);
        await issueRepository.AddAsync(joinToken, CancellationToken.None);

        issuedMediator.PublishedNotifications
            .OfType<JoinTokenIssuedEvent>()
            .Should()
            .ContainSingle(@event => @event.JoinTokenId == joinToken.JoinTokenId);

        var consumedAt = joinToken.IssuedAt.AddMinutes(5);
        var consumedMediator = new CapturingMediator();

        await using var consumeContext = BuildContext(consumedMediator);
        var persistedJoinToken = await new JoinTokenRepository(consumeContext)
            .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

        persistedJoinToken.Should().NotBeNull();
        persistedJoinToken!.Consume(consumedAt, new JoinTokenPolicy());
        await consumeContext.SaveChangesAsync(CancellationToken.None);

        consumedMediator.PublishedNotifications
            .OfType<JoinTokenConsumedEvent>()
            .Should()
            .ContainSingle(@event =>
                @event.JoinTokenId == joinToken.JoinTokenId &&
                @event.LiveSessionId == joinToken.LiveSessionId &&
                @event.TeamId == joinToken.TeamId &&
                @event.ConsumedAt == consumedAt);

        await using var assertContext = BuildContext();
        var reloadedJoinToken = await new JoinTokenRepository(assertContext)
            .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

        reloadedJoinToken.Should().NotBeNull();
        reloadedJoinToken!.Status.Should().Be(JoinTokenStatus.Consumed);
        reloadedJoinToken.ConsumedAt.Should().Be(consumedAt);
    }

    [Fact]
    public async Task ReplayAttemptOnConsumedToken_IsRejectedAndStateRemainsConsumed()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var joinToken = CreateJoinToken();

        await using var issueContext = BuildContext();
        IJoinTokenRepository issueRepository = new JoinTokenRepository(issueContext);
        await issueRepository.AddAsync(joinToken, CancellationToken.None);

        var consumedAt = joinToken.IssuedAt.AddMinutes(3);

        await using (var consumeContext = BuildContext())
        {
            var persistedJoinToken = await new JoinTokenRepository(consumeContext)
                .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

            persistedJoinToken!.Consume(consumedAt, new JoinTokenPolicy());
            await consumeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var replayContext = BuildContext();
        var consumedJoinToken = await new JoinTokenRepository(replayContext)
            .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

        consumedJoinToken.Should().NotBeNull();

        Action replay = () => consumedJoinToken!.Consume(consumedAt.AddMinutes(1), new JoinTokenPolicy());

        replay.Should().Throw<JoinTokenReplayRejectedException>();

        await using var assertContext = BuildContext();
        var reloadedJoinToken = await new JoinTokenRepository(assertContext)
            .GetByIdAsync(joinToken.JoinTokenId, CancellationToken.None);

        reloadedJoinToken.Should().NotBeNull();
        reloadedJoinToken!.Status.Should().Be(JoinTokenStatus.Consumed);
        reloadedJoinToken.ConsumedAt.Should().Be(consumedAt);
    }

    [Fact]
    public async Task MembershipAndJoinTokenLookup_ValidateParticipantTeamContextEndToEnd()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var participant = User.Provision("kc-participant-join", "Pat Join", "pat.join@example.com", Role.Participant);
        var team = RegisteredTeam.Register("Session Team", "SESSION-01");
        var joinToken = CreateJoinToken(liveSessionId: Guid.NewGuid(), teamId: team.TeamId, issuedByUserId: 99);

        setupContext.Users.Add(participant);
        setupContext.RegisteredTeams.Add(team);
        await setupContext.SaveChangesAsync(CancellationToken.None);

        team.AuthorizeParticipant(participant.Id);
        setupContext.JoinTokens.Add(joinToken);
        await setupContext.SaveChangesAsync(CancellationToken.None);

        await using var assertContext = BuildContext();
        var persistedTeam = await new TeamRepository(assertContext)
            .GetByIdWithMembershipsAsync(team.TeamId, CancellationToken.None);
        var persistedJoinToken = await new JoinTokenRepository(assertContext)
            .GetByLiveSessionIdAndTeamIdAsync(joinToken.LiveSessionId, team.TeamId, CancellationToken.None);

        persistedTeam.Should().NotBeNull();
        persistedTeam!.Memberships.Should().ContainSingle(membership =>
            membership.TeamId == team.TeamId && membership.UserId == participant.Id);

        persistedJoinToken.Should().NotBeNull();
        persistedJoinToken!.TeamId.Should().Be(team.TeamId);
        persistedJoinToken.LiveSessionId.Should().Be(joinToken.LiveSessionId);
        persistedJoinToken.Status.Should().Be(JoinTokenStatus.Active);
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.JoinTokens.ExecuteDeleteAsync();
        await context.RegisteredTeamMemberships.ExecuteDeleteAsync();
        await context.RegisteredTeams.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(IMediator? mediator = null, ICurrentUser? currentUser = null)
        => _contextFactory.Create(mediator, currentUser);

    private static JoinToken CreateJoinToken(
        Guid? liveSessionId = null,
        Guid? teamId = null,
        int issuedByUserId = 42,
        DateTimeOffset? issuedAt = null)
    {
        var effectiveIssuedAt = issuedAt ?? DateTimeOffset.UtcNow;

        return JoinToken.Issue(
            liveSessionId ?? Guid.NewGuid(),
            teamId ?? Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}",
            effectiveIssuedAt,
            effectiveIssuedAt.AddMinutes(30),
            issuedByUserId,
            new JoinTokenPolicy());
    }
}
