using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Application.Users.Handlers;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
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

    private ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record StubCurrentUser(string? Id, string? Email, string? Role) : ICurrentUser;
}
