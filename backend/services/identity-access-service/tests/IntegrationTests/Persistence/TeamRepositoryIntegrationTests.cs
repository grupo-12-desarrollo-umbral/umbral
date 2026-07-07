using MediatR;
using umbral_backend.Application.Common.Identity;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class TeamRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public TeamRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task RegisterTeam_PersistsFieldsAndPublishesRegisteredEvent()
    {
        await using var context = BuildContext(new CapturingMediator());
        await ResetDatabaseAsync(context);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        ITeamRepository repository = new TeamRepository(actContext);

        var team = Team.Register(" Red Foxes ", " RED-01 ");
        await repository.AddAsync(team, CancellationToken.None);

        var publishedEvent = mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TeamRegisteredEvent)
            .Which.Should().BeOfType<TeamRegisteredEvent>().Subject;

        publishedEvent.TeamId.Should().Be(team.TeamId);
        publishedEvent.DisplayName.Should().Be("Red Foxes");
        publishedEvent.TeamCode.Should().Be("RED-01");

        await using var assertContext = BuildContext();
        var reloadedTeam = await assertContext.Teams.SingleAsync(storedTeam => storedTeam.TeamId == team.TeamId);

        reloadedTeam.DisplayName.Should().Be("Red Foxes");
        reloadedTeam.TeamCode.Should().Be("RED-01");
        reloadedTeam.IsActive.Should().BeTrue();
        reloadedTeam.Created.Should().NotBe(default);
        reloadedTeam.LastModified.Should().NotBe(default);
    }

    [Fact]
    public async Task UpdateTeamDetails_PersistsChangesAndPublishesUpdatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        ITeamRepository setupRepository = new TeamRepository(setupContext);

        var team = Team.Register("Red Foxes", "RED-01");
        await setupRepository.AddAsync(team, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        ITeamRepository repository = new TeamRepository(actContext);

        var persistedTeam = await repository.GetByIdAsync(team.TeamId, CancellationToken.None);
        persistedTeam.Should().NotBeNull();

        persistedTeam!.UpdateDetails(" Blue Owls ", " BLUE-02 ");
        await repository.UpdateAsync(persistedTeam, CancellationToken.None);

        var publishedEvent = mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TeamDetailsUpdatedEvent)
            .Which.Should().BeOfType<TeamDetailsUpdatedEvent>().Subject;

        publishedEvent.TeamId.Should().Be(team.TeamId);
        publishedEvent.DisplayName.Should().Be("Blue Owls");
        publishedEvent.TeamCode.Should().Be("BLUE-02");

        await using var assertContext = BuildContext();
        var reloadedTeam = await assertContext.Teams.SingleAsync(storedTeam => storedTeam.TeamId == team.TeamId);

        reloadedTeam.DisplayName.Should().Be("Blue Owls");
        reloadedTeam.TeamCode.Should().Be("BLUE-02");
        reloadedTeam.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateTeam_PersistsInactiveStateAndPublishesDeactivatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        ITeamRepository setupRepository = new TeamRepository(setupContext);

        var team = Team.Register("Red Foxes", "RED-01");
        await setupRepository.AddAsync(team, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        ITeamRepository repository = new TeamRepository(actContext);

        var persistedTeam = await repository.GetByIdAsync(team.TeamId, CancellationToken.None);
        persistedTeam.Should().NotBeNull();

        persistedTeam!.Deactivate();
        await repository.UpdateAsync(persistedTeam, CancellationToken.None);

        var publishedEvent = mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TeamDeactivatedEvent)
            .Which.Should().BeOfType<TeamDeactivatedEvent>().Subject;

        publishedEvent.TeamId.Should().Be(team.TeamId);

        await using var assertContext = BuildContext();
        var reloadedTeam = await assertContext.Teams.SingleAsync(storedTeam => storedTeam.TeamId == team.TeamId);

        reloadedTeam.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterTeam_WithDuplicateCode_ThrowsInHandlerAndDatabaseBackstopRejectsDuplicate()
    {
        await using var setupContext = BuildContext(new NoOpMediator(), new TestCurrentUser("kc-admin"));
        await ResetDatabaseAsync(setupContext);

        IUserRepository userRepository = new UserRepository(setupContext);
        await userRepository.AddAsync(
            User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator),
            CancellationToken.None);

        var currentUser = new TestCurrentUser("kc-admin");
        var accessPolicy = new AccessPolicy();

        await using var actContext = BuildContext(new NoOpMediator(), currentUser);
        var handler = new RegisterTeamCommandHandler(
            new TeamRepository(actContext),
            new CurrentActor(currentUser, new UserRepository(actContext)),
            accessPolicy);

        var firstTeamId = await handler.Handle(
            new RegisterTeamCommand("Red Foxes", "DUP-01"),
            CancellationToken.None);

        firstTeamId.Should().NotBe(Guid.Empty);

        await FluentActions.Invoking(() => handler.Handle(
                new RegisterTeamCommand("Blue Owls", "DUP-01"),
                CancellationToken.None))
            .Should().ThrowAsync<TeamCodeAlreadyExistsException>();

        await using var backstopContext = BuildContext();
        ITeamRepository backstopRepository = new TeamRepository(backstopContext);

        await FluentActions.Invoking(() => backstopRepository.AddAsync(
                Team.Register("Green Turtles", "DUP-01"),
                CancellationToken.None))
            .Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ListTeams_WithPagination_RespectsPageBoundaries()
    {
        await using var context = BuildContext();
        await ResetDatabaseAsync(context);
        ITeamRepository repository = new TeamRepository(context);

        await repository.AddAsync(Team.Register("Charlie Crew", "TEAM-03"), CancellationToken.None);
        await repository.AddAsync(Team.Register("Alpha Squad", "TEAM-01"), CancellationToken.None);
        await repository.AddAsync(Team.Register("Bravo Unit", "TEAM-02"), CancellationToken.None);

        var firstPage = await repository.ListAsync(page: 1, pageSize: 2, CancellationToken.None);
        var secondPage = await repository.ListAsync(page: 2, pageSize: 2, CancellationToken.None);

        firstPage.TotalCount.Should().Be(3);
        firstPage.Page.Should().Be(1);
        firstPage.PageSize.Should().Be(2);
        firstPage.Items.Select(team => team.DisplayName).Should().Equal("Alpha Squad", "Bravo Unit");

        secondPage.TotalCount.Should().Be(3);
        secondPage.Page.Should().Be(2);
        secondPage.PageSize.Should().Be(2);
        secondPage.Items.Select(team => team.DisplayName).Should().Equal("Charlie Crew");
    }

    [Fact]
    public async Task AssignParticipantToActiveTeam_PersistsMembershipAndPublishesAssignedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var administrator = User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator);
        var participant = User.Provision("kc-participant-01", "Pat Participant", "participant@example.com", Role.Participant);
        var team = Team.Register("Red Foxes", "RED-01");

        setupContext.Users.AddRange(administrator, participant);
        setupContext.Teams.Add(team);
        await setupContext.SaveChangesAsync();

        var mediator = new CapturingMediator();
        var currentUser = new TestCurrentUser("kc-admin");

        await using var actContext = BuildContext(mediator, currentUser);
        var handler = new AssignParticipantToTeamCommandHandler(
            new TeamRepository(actContext),
            new UserRepository(actContext),
            new CurrentActor(currentUser, new UserRepository(actContext)),
            new AccessPolicy());

        var membershipId = await handler.Handle(
            new AssignParticipantToTeamCommand(team.TeamId, participant.Id),
            CancellationToken.None);

        membershipId.Should().NotBe(Guid.Empty);

        mediator.PublishedNotifications
            .OfType<ParticipantAssignedToTeamEvent>()
            .Should()
            .ContainSingle(@event => @event.TeamId == team.TeamId && @event.UserId == participant.Id);

        await using var assertContext = BuildContext();
        var reloadedTeam = await new TeamRepository(assertContext)
            .GetByIdWithMembershipsAsync(team.TeamId, CancellationToken.None);

        reloadedTeam.Should().NotBeNull();
        reloadedTeam!.Memberships.Should().ContainSingle();

        var membership = reloadedTeam.Memberships.Single();
        membership.TeamMembershipId.Should().Be(membershipId);
        membership.TeamId.Should().Be(team.TeamId);
        membership.UserId.Should().Be(participant.Id);
        membership.AssignedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task AssignParticipantTwice_ThrowsAndLeavesSingleMembershipRow()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var administrator = User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator);
        var participant = User.Provision("kc-participant-02", "Pat Participant", "participant2@example.com", Role.Participant);
        var team = Team.Register("Blue Owls", "BLUE-02");

        setupContext.Users.AddRange(administrator, participant);
        setupContext.Teams.Add(team);
        await setupContext.SaveChangesAsync();

        var currentUser = new TestCurrentUser("kc-admin");

        await using var firstContext = BuildContext(new NoOpMediator(), currentUser);
        var firstHandler = new AssignParticipantToTeamCommandHandler(
            new TeamRepository(firstContext),
            new UserRepository(firstContext),
            new CurrentActor(currentUser, new UserRepository(firstContext)),
            new AccessPolicy());

        await firstHandler.Handle(
            new AssignParticipantToTeamCommand(team.TeamId, participant.Id),
            CancellationToken.None);

        await using var secondContext = BuildContext(new NoOpMediator(), currentUser);
        var secondHandler = new AssignParticipantToTeamCommandHandler(
            new TeamRepository(secondContext),
            new UserRepository(secondContext),
            new CurrentActor(currentUser, new UserRepository(secondContext)),
            new AccessPolicy());

        await FluentActions.Invoking(() => secondHandler.Handle(
                new AssignParticipantToTeamCommand(team.TeamId, participant.Id),
                CancellationToken.None))
            .Should().ThrowAsync<ParticipantAlreadyAssignedToTeamException>();

        await using var assertContext = BuildContext();
        var membershipRows = await assertContext.TeamMemberships
            .Where(membership => membership.TeamId == team.TeamId && membership.UserId == participant.Id)
            .CountAsync();

        membershipRows.Should().Be(1);
    }

    [Fact]
    public async Task AssignParticipantToInactiveTeam_ThrowsAndCreatesNoMembershipRow()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var administrator = User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator);
        var participant = User.Provision("kc-participant-03", "Pat Participant", "participant3@example.com", Role.Participant);
        var team = Team.Register("Green Turtles", "GREEN-03");
        team.Deactivate();

        setupContext.Users.AddRange(administrator, participant);
        setupContext.Teams.Add(team);
        await setupContext.SaveChangesAsync();

        var currentUser = new TestCurrentUser("kc-admin");

        await using var actContext = BuildContext(new NoOpMediator(), currentUser);
        var handler = new AssignParticipantToTeamCommandHandler(
            new TeamRepository(actContext),
            new UserRepository(actContext),
            new CurrentActor(currentUser, new UserRepository(actContext)),
            new AccessPolicy());

        await FluentActions.Invoking(() => handler.Handle(
                new AssignParticipantToTeamCommand(team.TeamId, participant.Id),
                CancellationToken.None))
            .Should().ThrowAsync<TeamNotActiveException>();

        await using var assertContext = BuildContext();
        var membershipRows = await assertContext.TeamMemberships
            .Where(membership => membership.TeamId == team.TeamId)
            .CountAsync();

        membershipRows.Should().Be(0);
    }

    [Fact]
    public async Task GetTeamParticipants_ReturnsPersistedMembershipProjections()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);

        var administrator = User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator);
        var firstParticipant = User.Provision("kc-participant-04", "First Participant", "participant4@example.com", Role.Participant);
        var secondParticipant = User.Provision("kc-participant-05", "Second Participant", "participant5@example.com", Role.Participant);
        var team = Team.Register("Silver Sharks", "SILVER-04");

        setupContext.Users.AddRange(administrator, firstParticipant, secondParticipant);
        setupContext.Teams.Add(team);
        await setupContext.SaveChangesAsync();

        team.AssignParticipant(firstParticipant.Id);
        team.AssignParticipant(secondParticipant.Id);
        await setupContext.SaveChangesAsync();

        var currentUser = new TestCurrentUser("kc-admin");

        await using var actContext = BuildContext(new NoOpMediator(), currentUser);
        var handler = new GetTeamParticipantsQueryHandler(
            new TeamRepository(actContext),
            new UserRepository(actContext),
            new CurrentActor(currentUser, new UserRepository(actContext)),
            new AccessPolicy());

        var result = await handler.Handle(
            new GetTeamParticipantsQuery(team.TeamId),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(item => item.TeamId == team.TeamId && item.UserId == firstParticipant.Id);
        result.Should().Contain(item => item.TeamId == team.TeamId && item.UserId == secondParticipant.Id);
        result.All(item => item.TeamMembershipId != Guid.Empty).Should().BeTrue();
        result.All(item => item.AssignedAt != default).Should().BeTrue();
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.TeamMemberships.ExecuteDeleteAsync();
        await context.SessionTeamAssociations.ExecuteDeleteAsync();
        await context.LiveSessionReferences.ExecuteDeleteAsync();
        await context.Teams.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(IMediator? mediator = null, ICurrentUser? currentUser = null)
        => _contextFactory.Create(mediator, currentUser);
}
