using MediatR;
using umbral_backend.Application.Common.Identity;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserProvisioningRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public UserProvisioningRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task AuthenticateUserAndRetrieveProfile_PersistsProvisionedUser()
    {
        await using var context = BuildContext();
        await ResetDatabaseAsync(context);
        IUserRepository repository = new UserRepository(context);
        var authenticateHandler = new AuthenticateUserCommandHandler(
            new TestCurrentUser("kc-user-01", "alice@example.com", "Operator"),
            repository,
            new IdentityProvisioningPolicy(),
            new AccessPolicy());

        var authenticateResult = await authenticateHandler.Handle(
            new AuthenticateUserCommand("Alice Operator"),
            CancellationToken.None);

        authenticateResult.Actor.ExternalIdentityId.Should().Be("kc-user-01");
        authenticateResult.Actor.Email.Should().Be("alice@example.com");
        authenticateResult.Actor.Role.Should().Be("Operator");
        authenticateResult.Access.IsAllowed.Should().BeTrue();

        var getProfileHandler = new GetAuthenticatedActorProfileQueryHandler(
            new CurrentActor(
                new TestCurrentUser("kc-user-01", "alice@example.com", "Operator"),
                repository));

        var profile = await getProfileHandler.Handle(
            new GetAuthenticatedActorProfileQuery(),
            CancellationToken.None);

        profile.ExternalIdentityId.Should().Be("kc-user-01");
        profile.DisplayName.Should().Be("Alice Operator");
        profile.Email.Should().Be("alice@example.com");
        profile.Role.Should().Be("Operator");
        profile.IsActive.Should().BeTrue();

        var persistedUser = await context.Users.SingleAsync();
        persistedUser.ExternalIdentityId.Should().Be("kc-user-01");
        persistedUser.DisplayName.Should().Be("Alice Operator");
        persistedUser.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task DeactivateUser_PersistsInactiveStateAndPreservesHistory()
    {
        await using var context = BuildContext();
        await ResetDatabaseAsync(context);
        IUserRepository repository = new UserRepository(context);

        var user = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        await repository.AddAsync(user, CancellationToken.None);

        var persistedUser = await repository.GetByExternalIdentityIdAsync("kc-admin", CancellationToken.None);
        persistedUser.Should().NotBeNull();

        persistedUser!.DeactivateAccess();
        await repository.UpdateAsync(persistedUser, CancellationToken.None);

        var activeRecord = await context.Users.SingleOrDefaultAsync(
            listedUser => listedUser.Id == persistedUser.Id && listedUser.IsActive);
        activeRecord.Should().BeNull();

        var deactivatedUser = await repository.GetByIdAsync(persistedUser.Id, CancellationToken.None);
        deactivatedUser.Should().NotBeNull();
        deactivatedUser!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ListUsers_ReturnsStablePagedCatalog()
    {
        await using var context = BuildContext();
        await ResetDatabaseAsync(context);
        IUserRepository repository = new UserRepository(context);

        await repository.AddAsync(
            User.Provision("kc-charlie", "Charlie", "charlie@example.com", Role.Operator),
            CancellationToken.None);
        await repository.AddAsync(
            User.Provision("kc-alice", "Alice", "alice@example.com", Role.Administrator),
            CancellationToken.None);
        await repository.AddAsync(
            User.Provision("kc-bob", "Bob", "bob@example.com", Role.Operator),
            CancellationToken.None);

        var page = await repository.ListAsync(2, 2, CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.Items.Single().DisplayName.Should().Be("Charlie");
    }

    [Fact]
    public async Task AssignRole_PersistsRoleChangeAndPublishesAssignedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        IUserRepository setupRepository = new UserRepository(setupContext);

        var user = User.Provision("kc-role-01", "Role User", "role.user@example.com", Role.Operator);
        await setupRepository.AddAsync(user, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        IUserRepository repository = new UserRepository(actContext);

        var persistedUser = await repository.GetByIdAsync(user.Id, CancellationToken.None);
        persistedUser.Should().NotBeNull();

        persistedUser!.AssignRole(Role.Administrator);
        await repository.UpdateAsync(persistedUser, CancellationToken.None);

        await using var assertContext = BuildContext();
        var reloadedUser = await assertContext.Users.SingleAsync(storedUser => storedUser.Id == user.Id);

        reloadedUser.Role.Should().Be(Role.Administrator);
        mediator.PublishedNotifications
            .OfType<UserRoleAssignedEvent>()
            .Should()
            .ContainSingle(assignedEvent =>
                assignedEvent.User.Id == user.Id
                && assignedEvent.PreviousRole == Role.Operator
                && assignedEvent.CurrentRole == Role.Administrator);
    }

    [Fact]
    public async Task AssignRole_OnDeactivatedUser_ThrowsAndKeepsPersistedRoleUnchanged()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        IUserRepository setupRepository = new UserRepository(setupContext);

        var user = User.Provision("kc-role-02", "Inactive User", "inactive.user@example.com", Role.Participant);
        user.DeactivateAccess();
        await setupRepository.AddAsync(user, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        IUserRepository repository = new UserRepository(actContext);

        var persistedUser = await repository.GetByIdAsync(user.Id, CancellationToken.None);
        persistedUser.Should().NotBeNull();

        var act = () => persistedUser!.AssignRole(Role.Operator);

        act.Should().Throw<DeactivatedUserRoleAssignmentNotAllowedException>();

        await using var assertContext = BuildContext();
        var reloadedUser = await assertContext.Users.SingleAsync(storedUser => storedUser.Id == user.Id);

        reloadedUser.Role.Should().Be(Role.Participant);
        mediator.PublishedNotifications.Should().NotContain(notification => notification is UserRoleAssignedEvent);
    }

    [Fact]
    public async Task AssignRole_WithSameRole_IsIdempotentAndDoesNotPublishAssignedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        IUserRepository setupRepository = new UserRepository(setupContext);

        var user = User.Provision("kc-role-03", "Idempotent User", "idempotent.user@example.com", Role.Operator);
        await setupRepository.AddAsync(user, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator);
        IUserRepository repository = new UserRepository(actContext);

        var persistedUser = await repository.GetByIdAsync(user.Id, CancellationToken.None);
        persistedUser.Should().NotBeNull();

        persistedUser!.AssignRole(Role.Operator);
        await repository.UpdateAsync(persistedUser, CancellationToken.None);

        await using var assertContext = BuildContext();
        var reloadedUser = await assertContext.Users.SingleAsync(storedUser => storedUser.Id == user.Id);

        reloadedUser.Role.Should().Be(Role.Operator);
        mediator.PublishedNotifications.Should().NotContain(notification => notification is UserRoleAssignedEvent);
        mediator.PublishedNotifications.Should().NotContain(notification => notification is UserRoleRevokedEvent);
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(IMediator? mediator = null)
        => _contextFactory.Create(mediator);
}
