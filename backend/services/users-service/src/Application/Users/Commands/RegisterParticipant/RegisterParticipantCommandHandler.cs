using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Users.Commands.RegisterParticipant;

// Anonymous participant self-registration (ADR-0016 §1). No AuthorizationProxy wraps this handler:
// the endpoint is intentionally unauthenticated, and its only capability is *create a Participant*.
// The role is fixed here and never read from the request, so this path can never mint an
// Operator/Administrator. No local User record is written — that happens on first sign-in via
// POST /api/users/authenticated; here we only provision the Keycloak identity.
public sealed class RegisterParticipantCommandHandler
    : IRequestHandler<RegisterParticipantCommand, RegisterParticipantResultDto>
{
    // Server-fixed role: the sole role this path may ever assign. Elevation to Operator/Administrator
    // is a separate, Administrator-guarded act (ADR-0016 §2/§3), never reachable from here.
    private const Role SelfRegistrationRole = Role.Participant;

    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public RegisterParticipantCommandHandler(IIdentityProviderAdminService identityProviderAdmin)
    {
        _identityProviderAdmin = identityProviderAdmin;
    }

    public async Task<RegisterParticipantResultDto> Handle(
        RegisterParticipantCommand command, CancellationToken cancellationToken)
    {
        // Keycloak-first with a compensating delete, mirroring the invitation flow: the account is
        // created (enabled, with the chosen password, emailVerified: false), then gets its role and the
        // verification email. If role assignment or the email fails, the account is deleted so no
        // orphaned account is left behind. Uniqueness is Keycloak's — a duplicate email 409s inside
        // CreateParticipantAsync as EmailAlreadyRegisteredException.
        var externalIdentityId = await _identityProviderAdmin.CreateParticipantAsync(
            command.DisplayName, command.Email, command.Password, cancellationToken);

        try
        {
            await _identityProviderAdmin.SyncUserRoleAsync(externalIdentityId, SelfRegistrationRole, cancellationToken);
            await _identityProviderAdmin.SendVerifyEmailAsync(externalIdentityId, cancellationToken);
        }
        catch
        {
            await _identityProviderAdmin.DeleteUserAsync(externalIdentityId, CancellationToken.None);
            throw;
        }

        return new RegisterParticipantResultDto(command.Email, SelfRegistrationRole.ToString());
    }
}
