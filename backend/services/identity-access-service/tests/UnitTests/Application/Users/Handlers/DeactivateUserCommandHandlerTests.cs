using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class DeactivateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeactivatesUserAndPersistsUpdate()
    {
        var actor = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        actor.Id = 1;

        var target = User.Provision("kc-operator", "Operator", "operator@example.com", Role.Operator);
        target.Id = 2;
        User? updatedUser = null;

        var currentActor = new Mock<ICurrentActor>();
        currentActor.Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        repository
            .Setup(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => updatedUser = user)
            .Returns(Task.CompletedTask);

        var handler = new DeactivateUserCommandHandler(repository.Object, currentActor.Object, new AccessPolicy());

        await handler.Handle(new DeactivateUserCommand(target.Id), CancellationToken.None);

        updatedUser.Should().NotBeNull();
        updatedUser!.IsActive.Should().BeFalse();
        updatedUser.DomainEvents.Should().ContainSingle(eventItem => eventItem is UserAccessDeactivatedEvent);
        repository.Verify(repo => repo.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedActor()
    {
        var actor = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        actor.Id = 1;
        actor.DeactivateAccess();

        var currentActor = new Mock<ICurrentActor>();
        currentActor.Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        var repository = new Mock<IUserRepository>();

        var handler = new DeactivateUserCommandHandler(repository.Object, currentActor.Object, new AccessPolicy());

        var act = async () => await handler.Handle(new DeactivateUserCommand(42), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
