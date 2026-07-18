using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AssignUserRole;

public sealed class AssignUserRoleCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignsNewRoleAndPersistsUpdate()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);
        User? updatedUser = null;

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => updatedUser = user)
            .Returns(Task.CompletedTask);

        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        var handler = new AssignUserRoleCommandHandler(repository.Object, identityProviderAdmin.Object);

        await handler.Handle(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        updatedUser.Should().NotBeNull();
        updatedUser!.Role.Should().Be(Role.Participant);
        updatedUser.DomainEvents.OfType<UserRoleRevokedEvent>().Should().ContainSingle();
        updatedUser.DomainEvents.OfType<UserRoleAssignedEvent>().Should().ContainSingle();
        identityProviderAdmin.Verify(
            admin => admin.SyncUserRoleAsync(target.ExternalIdentityId, Role.Participant, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenKeycloakSyncFails_DoesNotPersistUpdate()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(admin => admin.SyncUserRoleAsync(target.ExternalIdentityId, Role.Participant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IdentityProviderRoleSyncException(target.ExternalIdentityId, "Participant", "Keycloak unavailable."));

        var handler = new AssignUserRoleCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        // Keycloak-first: a sync failure aborts before the DB is committed, so the two stores stay in agreement.
        await act.Should().ThrowAsync<IdentityProviderRoleSyncException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameRoleAssignmentIsIdempotent()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        var handler = new AssignUserRoleCommandHandler(repository.Object, identityProviderAdmin.Object);

        await handler.Handle(new AssignUserRoleCommand(target.Id, "Operator"), CancellationToken.None);

        target.Role.Should().Be(Role.Operator);
        target.DomainEvents.OfType<UserRoleAssignedEvent>().Should().BeEmpty();
        target.DomainEvents.OfType<UserRoleRevokedEvent>().Should().BeEmpty();
        identityProviderAdmin.Verify(
            admin => admin.SyncUserRoleAsync(target.ExternalIdentityId, Role.Operator, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedTargetUser()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);
        target.DeactivateAccess();

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        var handler = new AssignUserRoleCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserRoleAssignmentNotAllowedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        identityProviderAdmin.Verify(
            admin => admin.SyncUserRoleAsync(It.IsAny<string>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsUnknownRoleValue()
    {
        var target = CreateUser(2, "kc-user", Role.Participant);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var handler = new AssignUserRoleCommandHandler(repository.Object, Mock.Of<IIdentityProviderAdminService>());

        var act = async () => await handler.Handle(new AssignUserRoleCommand(target.Id, "SuperAdmin"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(AssignUserRoleCommand.Role)));
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
