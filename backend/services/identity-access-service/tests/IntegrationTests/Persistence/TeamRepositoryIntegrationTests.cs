using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Application.Teams.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class TeamRepositoryIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public TeamRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
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
        await using var setupContext = BuildContext(new NoOpMediator(), new StubCurrentUser("kc-admin"));
        await ResetDatabaseAsync(setupContext);

        IUserRepository userRepository = new UserRepository(setupContext);
        await userRepository.AddAsync(
            User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator),
            CancellationToken.None);

        var currentUser = new StubCurrentUser("kc-admin");
        var accessPolicy = new AccessPolicy();

        await using var actContext = BuildContext(new NoOpMediator(), currentUser);
        var handler = new RegisterTeamCommandHandler(
            new TeamRepository(actContext),
            new UserRepository(actContext),
            currentUser,
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

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.IdentityProviderSessions.ExecuteDeleteAsync();
        await context.Teams.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(IMediator? mediator = null, ICurrentUser? currentUser = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString);

        var effectiveCurrentUser = currentUser ?? new StubCurrentUser("integration-test");

        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(effectiveCurrentUser, TimeProvider.System),
            new DispatchDomainEventsInterceptor(mediator ?? new NoOpMediator()));

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private sealed record StubCurrentUser(string? Id, string? Email = null, string? Role = null) : ICurrentUser;

    private sealed class CapturingMediator : IMediator
    {
        public List<object> PublishedNotifications { get; } = new();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishedNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishedNotifications.Add(notification!);
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoOpMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
