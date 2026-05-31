using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.Handlers;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class UserProvisioningRepositoryIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public UserProvisioningRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AuthenticateUserAndRetrieveProfile_PersistsProvisionedUser()
    {
        await using var context = BuildContext();
        await ResetDatabaseAsync(context);
        IUserRepository repository = new UserRepository(context);
        var authenticateHandler = new AuthenticateUserCommandHandler(
            repository,
            new IdentityProvisioningPolicy(),
            new AccessPolicy());

        var authenticateResult = await authenticateHandler.Handle(
            new AuthenticateUserCommand(
                "kc-user-01",
                "Alice Operator",
                "alice@example.com",
                "Operator"),
            CancellationToken.None);

        authenticateResult.Actor.ExternalIdentityId.Should().Be("kc-user-01");
        authenticateResult.Actor.Email.Should().Be("alice@example.com");
        authenticateResult.Actor.Role.Should().Be("Operator");
        authenticateResult.Access.IsAllowed.Should().BeTrue();

        var getProfileHandler = new GetAuthenticatedActorProfileQueryHandler(
            repository,
            new StubCurrentUser("kc-user-01", "alice@example.com", "Operator"));

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

        persistedUser!.StartIdentityProviderSession(
            "Keycloak",
            "session-01",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

        await repository.UpdateAsync(persistedUser, CancellationToken.None);

        persistedUser.DeactivateAccess();
        await repository.UpdateAsync(persistedUser, CancellationToken.None);

        var activeRecord = await context.Users.SingleOrDefaultAsync(
            listedUser => listedUser.Id == persistedUser.Id && listedUser.IsActive);
        activeRecord.Should().BeNull();

        var deactivatedUser = await repository.GetByIdAsync(persistedUser.Id, CancellationToken.None);
        deactivatedUser.Should().NotBeNull();
        deactivatedUser!.IsActive.Should().BeFalse();
        deactivatedUser.IdentityProviderSessions.Should().ContainSingle();
        deactivatedUser.IdentityProviderSessions.Single().ProviderSessionKey.Should().Be("session-01");
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

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.IdentityProviderSessions.ExecuteDeleteAsync();
        await context.Users.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record StubCurrentUser(string? Id, string? Email, string? Role) : ICurrentUser;
}
