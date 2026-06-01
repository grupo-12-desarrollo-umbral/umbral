using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AssignUserRole;

public sealed class UserRoleAssignmentAuthorizationProxyTests
{
    [Fact]
    public async Task AssignAsync_WithAdministratorActor_DelegatesToInnerService()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var repository = CreateRepository(actor);
        var currentUser = CreateCurrentUser(actor.ExternalIdentityId);
        var inner = new Mock<IUserRoleAssignmentExecutor>();

        var proxy = new UserRoleAssignmentAuthorizationProxy(
            repository.Object,
            currentUser.Object,
            new AccessPolicy(),
            inner.Object);

        var command = new AssignUserRoleCommand(7, "Participant");

        await proxy.AssignAsync(command, CancellationToken.None);

        inner.Verify(service => service.AssignAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignAsync_WithoutTrustedActorIdentity_RejectsBeforeDelegating()
    {
        var repository = new Mock<IUserRepository>();
        var currentUser = CreateCurrentUser(null);
        var inner = new Mock<IUserRoleAssignmentExecutor>();

        var proxy = new UserRoleAssignmentAuthorizationProxy(
            repository.Object,
            currentUser.Object,
            new AccessPolicy(),
            inner.Object);

        var act = async () => await proxy.AssignAsync(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        inner.Verify(service => service.AssignAsync(It.IsAny<AssignUserRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_WithNonAdministratorActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var repository = CreateRepository(actor);
        var currentUser = CreateCurrentUser(actor.ExternalIdentityId);
        var inner = new Mock<IUserRoleAssignmentExecutor>();

        var proxy = new UserRoleAssignmentAuthorizationProxy(
            repository.Object,
            currentUser.Object,
            new AccessPolicy(),
            inner.Object);

        var act = async () => await proxy.AssignAsync(new AssignUserRoleCommand(7, "Administrator"), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
        inner.Verify(service => service.AssignAsync(It.IsAny<AssignUserRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_WithDeactivatedActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        actor.DeactivateAccess();

        var repository = CreateRepository(actor);
        var currentUser = CreateCurrentUser(actor.ExternalIdentityId);
        var inner = new Mock<IUserRoleAssignmentExecutor>();

        var proxy = new UserRoleAssignmentAuthorizationProxy(
            repository.Object,
            currentUser.Object,
            new AccessPolicy(),
            inner.Object);

        var act = async () => await proxy.AssignAsync(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        inner.Verify(service => service.AssignAsync(It.IsAny<AssignUserRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_WhenActorIsMissing_RejectsBeforeDelegating()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var currentUser = CreateCurrentUser("kc-missing");
        var inner = new Mock<IUserRoleAssignmentExecutor>();

        var proxy = new UserRoleAssignmentAuthorizationProxy(
            repository.Object,
            currentUser.Object,
            new AccessPolicy(),
            inner.Object);

        var act = async () => await proxy.AssignAsync(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        inner.Verify(service => service.AssignAsync(It.IsAny<AssignUserRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserRepository> CreateRepository(User actor)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        return repository;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        return currentUser;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
