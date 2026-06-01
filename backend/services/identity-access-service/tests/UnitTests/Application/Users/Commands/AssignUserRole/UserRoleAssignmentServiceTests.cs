using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AssignUserRole;

public sealed class UserRoleAssignmentServiceTests
{
    [Fact]
    public async Task AssignAsync_AssignsNewRoleAndPersistsUpdate()
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

        var keycloakAdmin = new Mock<IKeycloakAdminService>();
        var service = new UserRoleAssignmentService(repository.Object, keycloakAdmin.Object);

        await service.AssignAsync(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        updatedUser.Should().NotBeNull();
        updatedUser!.Role.Should().Be(Role.Participant);
        updatedUser.DomainEvents.OfType<UserRoleRevokedEvent>().Should().ContainSingle();
        updatedUser.DomainEvents.OfType<UserRoleAssignedEvent>().Should().ContainSingle();
        keycloakAdmin.Verify(
            admin => admin.SyncUserRoleAsync(target.ExternalIdentityId, Role.Participant, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignAsync_SameRoleAssignmentIsIdempotent()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var keycloakAdmin = new Mock<IKeycloakAdminService>();
        var service = new UserRoleAssignmentService(repository.Object, keycloakAdmin.Object);

        await service.AssignAsync(new AssignUserRoleCommand(target.Id, "Operator"), CancellationToken.None);

        target.Role.Should().Be(Role.Operator);
        target.DomainEvents.OfType<UserRoleAssignedEvent>().Should().BeEmpty();
        target.DomainEvents.OfType<UserRoleRevokedEvent>().Should().BeEmpty();
        keycloakAdmin.Verify(
            admin => admin.SyncUserRoleAsync(target.ExternalIdentityId, Role.Operator, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignAsync_RejectsDeactivatedTargetUser()
    {
        var target = CreateUser(2, "kc-user", Role.Operator);
        target.DeactivateAccess();

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var keycloakAdmin = new Mock<IKeycloakAdminService>();
        var service = new UserRoleAssignmentService(repository.Object, keycloakAdmin.Object);

        var act = async () => await service.AssignAsync(new AssignUserRoleCommand(target.Id, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserRoleAssignmentNotAllowedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        keycloakAdmin.Verify(
            admin => admin.SyncUserRoleAsync(It.IsAny<string>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignAsync_RejectsUnknownRoleValue()
    {
        var target = CreateUser(2, "kc-user", Role.Participant);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        var service = new UserRoleAssignmentService(repository.Object, Mock.Of<IKeycloakAdminService>());

        var act = async () => await service.AssignAsync(new AssignUserRoleCommand(target.Id, "SuperAdmin"), CancellationToken.None);

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
