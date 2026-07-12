using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.InviteUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.InviteUser;

public sealed class InviteUserCommandHandlerTests
{
    private const string Email = "invitee@example.com";
    private const string KeycloakUserId = "kc-invited-01";

    [Fact]
    public async Task Handle_CreatesKeycloakUser_AssignsRole_SendsEmail_AndPersistsLocalRecord()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        var repository = CreateRepositoryWithNoExistingEmail();
        User? addedUser = null;
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => addedUser = user)
            .Returns(Task.CompletedTask);

        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var result = await handler.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        addedUser.Should().NotBeNull();
        addedUser!.ExternalIdentityId.Should().Be(KeycloakUserId);
        addedUser.Email.Should().Be(Email);
        addedUser.Role.Should().Be(Role.Operator);
        addedUser.IsActive.Should().BeTrue();
        addedUser.DomainEvents.OfType<UserProvisionedEvent>().Should().ContainSingle();

        result.Email.Should().Be(Email);
        result.Role.Should().Be("Operator");

        identityProviderAdmin.Verify(a => a.CreateUserAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(a => a.SyncUserRoleAsync(KeycloakUserId, Role.Operator, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(a => a.SendExecuteActionsEmailAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
        // Created enabled, so no separate enable step, and nothing to compensate on the happy path.
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithParticipantRole_RejectsBeforeTouchingKeycloak()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        var repository = new Mock<IUserRepository>();
        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new InviteUserCommand(Email, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<ParticipantNotInvitableException>();
        identityProviderAdmin.Verify(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnknownRole_ThrowsValidationException()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        var repository = new Mock<IUserRepository>();
        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new InviteUserCommand(Email, "SuperAdmin"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey(nameof(InviteUserCommand.Role)));
        identityProviderAdmin.Verify(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyRegisteredLocally_ThrowsConflict_WithoutTouchingKeycloak()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-existing", "Existing", Email, Role.Operator));

        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<InvitedEmailAlreadyRegisteredException>();
        identityProviderAdmin.Verify(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailSendFails_CompensatesByDeletingAccount_AndDoesNotPersistLocalRecord()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        identityProviderAdmin
            .Setup(a => a.SendExecuteActionsEmailAsync(KeycloakUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable."));

        var repository = CreateRepositoryWithNoExistingEmail();
        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // No orphaned account: the created account is compensated away, and no local record is written.
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRoleSyncFails_CompensatesByDeletingAccount()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        identityProviderAdmin
            .Setup(a => a.SyncUserRoleAsync(KeycloakUserId, Role.Operator, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("role sync failed"));

        var repository = CreateRepositoryWithNoExistingEmail();
        var handler = new InviteUserCommandHandler(repository.Object, identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(a => a.SendExecuteActionsEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserRepository> CreateRepositoryWithNoExistingEmail()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return repository;
    }

    private static Mock<IIdentityProviderAdminService> CreateIdentityProvider()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KeycloakUserId);
        return identityProviderAdmin;
    }
}
