using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.RegisterParticipant;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.RegisterParticipant;

public sealed class RegisterParticipantCommandHandlerTests
{
    private const string DisplayName = "New Participant";
    private const string Email = "participant@example.com";
    private const string Password = "sup3rsecret";
    private const string KeycloakUserId = "kc-participant-01";

    [Fact]
    public async Task Handle_CreatesKeycloakParticipant_AssignsParticipantRole_AndSendsVerifyEmail()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        var handler = new RegisterParticipantCommandHandler(identityProviderAdmin.Object);

        var result = await handler.Handle(
            new RegisterParticipantCommand(DisplayName, Email, Password), CancellationToken.None);

        result.Email.Should().Be(Email);
        result.Role.Should().Be("Participant");

        identityProviderAdmin.Verify(
            a => a.CreateParticipantAsync(DisplayName, Email, Password, It.IsAny<CancellationToken>()), Times.Once);
        // Role is server-fixed to Participant — never anything else, never read from the request.
        identityProviderAdmin.Verify(
            a => a.SyncUserRoleAsync(KeycloakUserId, Role.Participant, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(
            a => a.SyncUserRoleAsync(It.IsAny<string>(), It.Is<Role>(r => r != Role.Participant), It.IsAny<CancellationToken>()),
            Times.Never);
        // VERIFY_EMAIL only — not the invitation's UPDATE_PASSWORD action email.
        identityProviderAdmin.Verify(a => a.SendVerifyEmailAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(
            a => a.SendExecuteActionsEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        // Happy path leaves nothing to compensate.
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyRegistered_PropagatesConflict()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(a => a.CreateParticipantAsync(DisplayName, Email, Password, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailAlreadyRegisteredException(Email));

        var handler = new RegisterParticipantCommandHandler(identityProviderAdmin.Object);

        var act = async () => await handler.Handle(
            new RegisterParticipantCommand(DisplayName, Email, Password), CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
        // Nothing was created, so there is nothing to compensate.
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRoleSyncFails_CompensatesByDeletingAccount()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        identityProviderAdmin
            .Setup(a => a.SyncUserRoleAsync(KeycloakUserId, Role.Participant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("role sync failed"));

        var handler = new RegisterParticipantCommandHandler(identityProviderAdmin.Object);

        var act = async () => await handler.Handle(
            new RegisterParticipantCommand(DisplayName, Email, Password), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(a => a.SendVerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenVerifyEmailFails_CompensatesByDeletingAccount()
    {
        var identityProviderAdmin = CreateIdentityProvider();
        identityProviderAdmin
            .Setup(a => a.SendVerifyEmailAsync(KeycloakUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable."));

        var handler = new RegisterParticipantCommandHandler(identityProviderAdmin.Object);

        var act = async () => await handler.Handle(
            new RegisterParticipantCommand(DisplayName, Email, Password), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        identityProviderAdmin.Verify(a => a.DeleteUserAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IIdentityProviderAdminService> CreateIdentityProvider()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(a => a.CreateParticipantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KeycloakUserId);
        return identityProviderAdmin;
    }
}
