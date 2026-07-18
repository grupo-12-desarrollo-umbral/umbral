using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Users.Commands.ForgotPassword;

// Anonymous forgot-password (ADR-0016 §1). No AuthorizationProxy wraps this handler: the caller has no
// session, and its only capability is *email a reset link to an address that already has an account*.
// CRITICAL anti-enumeration: when no account matches the email, the handler returns normally — it never
// throws and never signals existence, so the response is identical whether or not the email is
// registered. Keycloak owns the reset itself; here we only look up the account and trigger the mail.
public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public ForgotPasswordCommandHandler(IIdentityProviderAdminService identityProviderAdmin)
    {
        _identityProviderAdmin = identityProviderAdmin;
    }

    public async Task Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var externalIdentityId = await _identityProviderAdmin.FindUserIdByEmailAsync(command.Email, cancellationToken);

        // Unknown address: return silently. No throw, no distinct response — the endpoint must look the
        // same to a caller probing which emails exist.
        if (externalIdentityId is null)
        {
            return;
        }

        await _identityProviderAdmin.SendResetPasswordEmailAsync(externalIdentityId, cancellationToken);
    }
}
