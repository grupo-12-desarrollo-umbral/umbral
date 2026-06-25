using Moq;
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

        var handler = CreateHandler(repository, "kc-001", "ada@example.com", "Administrator");

        var result = await handler.Handle(
            new AuthenticateUserCommand("Ada Lovelace"),
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

        var handler = CreateHandler(repository, "kc-002", "grace@example.com", "Administrator");

        var result = await handler.Handle(
            new AuthenticateUserCommand("Grace Hopper"),
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

        var handler = CreateHandler(repository, "kc-003", "deactivated@example.com", "Operator");

        var act = async () => await handler.Handle(
            new AuthenticateUserCommand("Deactivated User"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutTrustedHeaders_RejectsRequest()
    {
        var repository = new Mock<IUserRepository>();
        var handler = CreateHandler(repository, id: null, email: "ada@example.com", role: "Administrator");

        var act = async () => await handler.Handle(
            new AuthenticateUserCommand("Ada Lovelace"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Trusted gateway identity headers are required.");
        repository.Verify(repo => repo.GetByExternalIdentityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuthenticateUserCommandHandler CreateHandler(
        Mock<IUserRepository> repository,
        string? id,
        string? email,
        string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Email).Returns(email);
        currentUser.SetupGet(user => user.Role).Returns(role);

        return new AuthenticateUserCommandHandler(
            currentUser.Object,
            repository.Object,
            new IdentityProvisioningPolicy(),
            new AccessPolicy());
    }
}
