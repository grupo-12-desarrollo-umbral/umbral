using System.Reflection;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.UnitTests.Application.Common.Behaviours;

public sealed class AuthorizationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenRequestHasNoAuthorizeAttribute_AllowsExecution()
    {
        var behaviour = new AuthorizationBehaviour<OpenRequest, string>(StubCurrentUser.Authenticated("kc-01", "Operator"));
        var nextInvoked = false;

        var result = await behaviour.Handle(
            new OpenRequest(),
            () =>
            {
                nextInvoked = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        result.Should().Be("ok");
        nextInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsMissing_ThrowsUnauthorizedAccessException()
    {
        var behaviour = new AuthorizationBehaviour<RestrictedRequest, string>(StubCurrentUser.Anonymous());

        var act = async () => await behaviour.Handle(
            new RestrictedRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenRoleIsNotAllowed_ThrowsForbiddenAccessException()
    {
        var behaviour = new AuthorizationBehaviour<RestrictedRequest, string>(StubCurrentUser.Authenticated("kc-01", "Participant"));

        var act = async () => await behaviour.Handle(
            new RestrictedRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenRoleMatches_AllowsExecution()
    {
        var behaviour = new AuthorizationBehaviour<RestrictedRequest, string>(StubCurrentUser.Authenticated("kc-01", "Operator"));

        var result = await behaviour.Handle(
            new RestrictedRequest(),
            () => Task.FromResult("allowed"),
            CancellationToken.None);

        result.Should().Be("allowed");
    }

    [Fact]
    public void AuthorizeAttribute_StoresConfiguredRoles()
    {
        var attribute = typeof(RestrictedRequest).GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Be("Administrator,Operator");
    }

    [Authorize(Roles = "Administrator,Operator")]
    private sealed record RestrictedRequest;

    private sealed record OpenRequest;

    private sealed record StubCurrentUser(string? Id, string? Role) : ICurrentUser
    {
        public string? Email => null;

        public static StubCurrentUser Authenticated(string id, string role) => new(id, role);

        public static StubCurrentUser Anonymous() => new(null, null);
    }
}
