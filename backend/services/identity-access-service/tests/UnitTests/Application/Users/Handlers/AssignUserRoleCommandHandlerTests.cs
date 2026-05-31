using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class AssignUserRoleCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignsNewRoleAndPersistsUpdate()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var target = CreateUser(2, "kc-user", Role.Operator);
        User? updatedUser = null;

        var repository = CreateRepository(actor, target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => updatedUser = user)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repository, actor.ExternalIdentityId);

        await handler.Handle(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        updatedUser.Should().NotBeNull();
        updatedUser!.Role.Should().Be(Role.Participant);
        var revokedEvent = updatedUser.DomainEvents.OfType<UserRoleRevokedEvent>().Should().ContainSingle().Subject;
        revokedEvent.RevokedRole.Should().Be(Role.Operator);
        revokedEvent.CurrentRole.Should().Be(Role.Participant);

        var assignedEvent = updatedUser.DomainEvents.OfType<UserRoleAssignedEvent>().Should().ContainSingle().Subject;
        assignedEvent.PreviousRole.Should().Be(Role.Operator);
        assignedEvent.CurrentRole.Should().Be(Role.Participant);
        repository.Verify(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameRoleAssignmentIsIdempotent()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var target = CreateUser(2, "kc-user", Role.Operator);

        var repository = CreateRepository(actor, target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repository, actor.ExternalIdentityId);

        await handler.Handle(new AssignUserRoleCommand(target.Id, "Operator"), CancellationToken.None);

        target.Role.Should().Be(Role.Operator);
        target.DomainEvents.OfType<UserRoleAssignedEvent>().Should().BeEmpty();
        target.DomainEvents.OfType<UserRoleRevokedEvent>().Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedTargetUser()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var target = CreateUser(2, "kc-user", Role.Operator);
        target.DeactivateAccess();

        var repository = CreateRepository(actor, target);
        var handler = CreateHandler(repository, actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserRoleAssignmentNotAllowedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsNonAdministratorCaller()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var target = CreateUser(2, "kc-user", Role.Participant);

        var repository = CreateRepository(actor, target);
        var handler = CreateHandler(repository, actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "Administrator"), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsUnknownRoleValue()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var target = CreateUser(2, "kc-user", Role.Participant);

        var repository = CreateRepository(actor, target);
        var handler = CreateHandler(repository, actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "SuperAdmin"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(AssignUserRoleCommand.Role)));
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AssignUserRoleCommandHandler CreateHandler(Mock<IUserRepository> repository, string currentUserId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId);

        var keycloakAdmin = new Mock<IKeycloakAdminService>();

        return new AssignUserRoleCommandHandler(
            repository.Object, currentUser.Object, new AccessPolicy(), keycloakAdmin.Object);
    }

    private static Mock<IUserRepository> CreateRepository(User actor, User target)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        return repository;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
