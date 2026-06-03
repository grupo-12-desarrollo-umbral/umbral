using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionReferenceRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionReferenceRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task ListTeamLobbyEntriesAsync_ReturnsActiveTeamsWithCallerMembershipFlags()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var participant = User.Provision("kc-participant-01", "Participant User", "participant@example.com", Role.Participant);
        var otherParticipant = User.Provision("kc-participant-02", "Other Participant", "participant2@example.com", Role.Participant);
        var ownTeam = Team.Register("Blue Owls", "BLUE-01");
        var joinableTeam = Team.Register("Red Foxes", "RED-01");
        var inactiveTeam = Team.Register("Grey Wolves", "GREY-01");
        inactiveTeam.Deactivate();

        setupContext.Users.AddRange(participant, otherParticipant);
        setupContext.Teams.AddRange(ownTeam, joinableTeam, inactiveTeam);
        await setupContext.SaveChangesAsync();

        ownTeam.AssignParticipant(participant.Id);
        joinableTeam.AssignParticipant(otherParticipant.Id);

        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        liveSessionReference.AssociateTeam(ownTeam.TeamId);
        liveSessionReference.AssociateTeam(joinableTeam.TeamId);
        liveSessionReference.AssociateTeam(inactiveTeam.TeamId);

        setupContext.LiveSessionReferences.Add(liveSessionReference);
        await setupContext.SaveChangesAsync();

        var currentUser = new TestCurrentUser(participant.ExternalIdentityId);
        await using var actContext = BuildContext(new NoOpMediator(), currentUser);
        ILiveSessionReferenceRepository repository = new LiveSessionReferenceRepository(actContext, currentUser);

        var result = await repository.ListTeamLobbyEntriesAsync(
            liveSessionReference.LiveSessionId,
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(entry => entry.TeamId == ownTeam.TeamId && entry.IsCallerMember);
        result.Should().Contain(entry => entry.TeamId == joinableTeam.TeamId && !entry.IsCallerMember);
        result.Select(entry => entry.DisplayName).Should().Equal("Blue Owls", "Red Foxes");
    }

    [Fact]
    public async Task GetBySessionCodeAsync_ReturnsPersistedReference()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        setupContext.LiveSessionReferences.Add(liveSessionReference);
        await setupContext.SaveChangesAsync();

        await using var actContext = BuildContext();
        ILiveSessionReferenceRepository repository = new LiveSessionReferenceRepository(
            actContext,
            TestCurrentUser.Default);

        var result = await repository.GetBySessionCodeAsync("RSF231", CancellationToken.None);

        result.Should().NotBeNull();
        result!.LiveSessionId.Should().Be(liveSessionReference.LiveSessionId);
        result.SessionCode.Should().Be("RSF231");
    }

    [Fact]
    public async Task GetParticipantMembershipAsync_ReturnsMembershipScopedToSession()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var participant = User.Provision("kc-participant-03", "Participant User", "participant3@example.com", Role.Participant);
        var sessionTeam = Team.Register("Blue Owls", "BLUE-01");
        var outsideTeam = Team.Register("Red Foxes", "RED-01");

        setupContext.Users.Add(participant);
        setupContext.Teams.AddRange(sessionTeam, outsideTeam);
        await setupContext.SaveChangesAsync();

        var expectedMembership = sessionTeam.AssignParticipant(participant.Id);
        outsideTeam.AssignParticipant(participant.Id);

        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        liveSessionReference.AssociateTeam(sessionTeam.TeamId);
        setupContext.LiveSessionReferences.Add(liveSessionReference);
        await setupContext.SaveChangesAsync();

        await using var actContext = BuildContext();
        ILiveSessionReferenceRepository repository = new LiveSessionReferenceRepository(
            actContext,
            TestCurrentUser.Default);

        var result = await repository.GetParticipantMembershipAsync(
            liveSessionReference.LiveSessionId,
            participant.Id,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.TeamId.Should().Be(sessionTeam.TeamId);
        result.TeamMembershipId.Should().Be(expectedMembership.TeamMembershipId);
    }

    [Fact]
    public async Task IsTeamAssociatedAsync_ReturnsTrueOnlyForSessionTeams()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var associatedTeam = Team.Register("Blue Owls", "BLUE-01");
        var foreignTeam = Team.Register("Red Foxes", "RED-01");
        var liveSessionReference = LiveSessionReference.Create(Guid.NewGuid(), "RSF231");
        liveSessionReference.AssociateTeam(associatedTeam.TeamId);

        setupContext.Teams.AddRange(associatedTeam, foreignTeam);
        setupContext.LiveSessionReferences.Add(liveSessionReference);
        await setupContext.SaveChangesAsync();

        await using var actContext = BuildContext();
        ILiveSessionReferenceRepository repository = new LiveSessionReferenceRepository(
            actContext,
            TestCurrentUser.Default);

        (await repository.IsTeamAssociatedAsync(
            liveSessionReference.LiveSessionId,
            associatedTeam.TeamId,
            CancellationToken.None)).Should().BeTrue();

        (await repository.IsTeamAssociatedAsync(
            liveSessionReference.LiveSessionId,
            foreignTeam.TeamId,
            CancellationToken.None)).Should().BeFalse();
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.IdentityProviderSessions.ExecuteDeleteAsync();
        await context.JoinTokens.ExecuteDeleteAsync();
        await context.TeamMemberships.ExecuteDeleteAsync();
        await context.SessionTeamAssociations.ExecuteDeleteAsync();
        await context.LiveSessionReferences.ExecuteDeleteAsync();
        await context.Teams.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(IMediator? mediator = null, ICurrentUser? currentUser = null)
        => _contextFactory.Create(mediator, currentUser);
}
