using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using MediatR;
using Moq;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

public class AuthorizationBehaviourTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();

    [Fact]
    public async Task Handle_WhenNoAuthorizeAttribute_CallsNext()
    {
        var sut = new AuthorizationBehaviour<UnauthorizedRequest, int>(_currentUser.Object);
        var result = await sut.Handle(new UnauthorizedRequest(), () => Task.FromResult(7), CancellationToken.None);

        result.Should().Be(7);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotAuthenticated_ThrowsUnauthorized()
    {
        _currentUser.SetupGet(u => u.Id).Returns((string?)null);
        var sut = new AuthorizationBehaviour<RoleAuthorizedRequest, int>(_currentUser.Object);

        var act = () => sut.Handle(new RoleAuthorizedRequest(), () => Task.FromResult(0), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenUserHasRequiredRole_CallsNext()
    {
        _currentUser.SetupGet(u => u.Id).Returns("user-1");
        _currentUser.SetupGet(u => u.Roles).Returns(new List<string> { "Administrator" });
        var sut = new AuthorizationBehaviour<RoleAuthorizedRequest, int>(_currentUser.Object);
        var result = await sut.Handle(new RoleAuthorizedRequest(), () => Task.FromResult(21), CancellationToken.None);

        result.Should().Be(21);
    }

    [Fact]
    public async Task Handle_WhenUserLacksRequiredRole_ThrowsForbidden()
    {
        _currentUser.SetupGet(u => u.Id).Returns("user-1");
        _currentUser.SetupGet(u => u.Roles).Returns(new List<string> { "Participant" });
        var sut = new AuthorizationBehaviour<RoleAuthorizedRequest, int>(_currentUser.Object);

        var act = () => sut.Handle(new RoleAuthorizedRequest(), () => Task.FromResult(0), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_WhenAuthorizeHasEmptyRoles_SkipsRoleCheck()
    {
        _currentUser.SetupGet(u => u.Id).Returns("user-1");
        var sut = new AuthorizationBehaviour<EmptyRolesRequest, int>(_currentUser.Object);
        var result = await sut.Handle(new EmptyRolesRequest(), () => Task.FromResult(44), CancellationToken.None);

        result.Should().Be(44);
    }

    [Authorize(Roles = "Administrator")]
    private sealed record RoleAuthorizedRequest : IRequest<int>;

    [Authorize]
    private sealed record EmptyRolesRequest : IRequest<int>;

    private sealed record UnauthorizedRequest : IRequest<int>;
}
