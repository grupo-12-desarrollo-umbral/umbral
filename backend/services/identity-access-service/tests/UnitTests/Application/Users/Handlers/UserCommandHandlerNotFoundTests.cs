using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Application.Users.Commands.ReactivateUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

// Covers the `GetByIdAsync(...) ?? throw new NotFoundException(...)` branch that the happy-path
// handler tests never take: the target user does not exist. The repository returns null (its
// default), so the null-coalescing throw fires before any identity-provider sync or persistence.
public sealed class UserCommandHandlerNotFoundTests
{
    private static Mock<ICurrentActor> AdministratorActor()
    {
        var actor = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        actor.Id = 1;
        var currentActor = new Mock<ICurrentActor>();
        currentActor.Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        return currentActor;
    }

    [Fact]
    public async Task DeactivateUser_UserNotFound_ThrowsNotFound()
    {
        var repository = new Mock<IUserRepository>();
        var identityProvider = new Mock<IIdentityProviderAdminService>();
        var handler = new DeactivateUserCommandHandler(
            repository.Object, AdministratorActor().Object, new AccessPolicy(), identityProvider.Object);

        var act = async () => await handler.Handle(new DeactivateUserCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReactivateUser_UserNotFound_ThrowsNotFound()
    {
        var repository = new Mock<IUserRepository>();
        var identityProvider = new Mock<IIdentityProviderAdminService>();
        var handler = new ReactivateUserCommandHandler(
            repository.Object, AdministratorActor().Object, new AccessPolicy(), identityProvider.Object);

        var act = async () => await handler.Handle(new ReactivateUserCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignUserRole_UserNotFound_ThrowsNotFound()
    {
        var repository = new Mock<IUserRepository>();
        var identityProvider = new Mock<IIdentityProviderAdminService>();
        var handler = new AssignUserRoleCommandHandler(repository.Object, identityProvider.Object);

        var act = async () => await handler.Handle(
            new AssignUserRoleCommand(999, Role.Operator.ToString()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
