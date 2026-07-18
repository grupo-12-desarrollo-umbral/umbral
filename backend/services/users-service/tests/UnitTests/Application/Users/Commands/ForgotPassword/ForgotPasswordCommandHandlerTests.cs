using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.ForgotPassword;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandlerTests
{
    private const string Email = "participant@example.com";
    private const string KeycloakUserId = "kc-participant-01";

    [Fact]
    public async Task Handle_WhenEmailIsKnown_LooksUpUser_ThenSendsResetPasswordEmail()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(a => a.FindUserIdByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(KeycloakUserId);

        var handler = new ForgotPasswordCommandHandler(identityProviderAdmin.Object);

        await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        identityProviderAdmin.Verify(
            a => a.FindUserIdByEmailAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(
            a => a.SendResetPasswordEmailAsync(KeycloakUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailIsUnknown_ReturnsNormally_WithoutSendingEmail()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        // No account for the address — anti-enumeration: the lookup happens, nothing is sent, no throw.
        identityProviderAdmin
            .Setup(a => a.FindUserIdByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new ForgotPasswordCommandHandler(identityProviderAdmin.Object);

        var act = async () => await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        await act.Should().NotThrowAsync();
        identityProviderAdmin.Verify(
            a => a.FindUserIdByEmailAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
        identityProviderAdmin.Verify(
            a => a.SendResetPasswordEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
