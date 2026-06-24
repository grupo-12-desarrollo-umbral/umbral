using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AuthenticateUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class AuthenticateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ProvisionNewUserAndReturnsAuthenticatedAccessFacts()
    {
        User? persistedUser = null;

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => persistedUser = user)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new AuthenticateUserCommand("kc-001", "Ada Lovelace", "ada@example.com", Role.Administrator),
            CancellationToken.None);

        result.Actor.UserId.Should().Be(0);
        result.Actor.ExternalIdentityId.Should().Be("kc-001");
        result.Actor.DisplayName.Should().Be("Ada Lovelace");
        result.Actor.Email.Should().Be("ada@example.com");
        result.Actor.Role.Should().Be("Administrator");
        result.Actor.IsActive.Should().BeTrue();
        result.Access.Capability.Should().Be("AuthenticatedPlatformAccess");
        result.Access.IsAllowed.Should().BeTrue();
        persistedUser.Should().NotBeNull();
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SynchronizesExistingUserAndPersistsUpdate()
    {
        var existingUser = User.Provision("kc-002", "Initial Name", "initial@example.com", global::umbral_backend.Domain.Enums.Role.Operator);

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        repository
            .Setup(repo => repo.UpdateAsync(existingUser, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repository);

        var result = await handler.Handle(
            new AuthenticateUserCommand("kc-002", "Grace Hopper", "grace@example.com", Role.Administrator),
            CancellationToken.None);

        result.Actor.DisplayName.Should().Be("Grace Hopper");
        result.Actor.Email.Should().Be("grace@example.com");
        result.Actor.Role.Should().Be("Operator");
        repository.Verify(repo => repo.UpdateAsync(existingUser, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedUser()
    {
        var existingUser = User.Provision("kc-003", "Deactivated User", "deactivated@example.com", global::umbral_backend.Domain.Enums.Role.Operator);
        existingUser.DeactivateAccess();

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var handler = CreateHandler(repository);

        var act = async () => await handler.Handle(
            new AuthenticateUserCommand("kc-003", "Deactivated User", "deactivated@example.com", Role.Operator),
            CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuthenticateUserCommandHandler CreateHandler(Mock<IUserRepository> repository)
    {
        return new AuthenticateUserCommandHandler(
            repository.Object,
            new IdentityProvisioningPolicy(),
            new AccessPolicy());
    }
}
