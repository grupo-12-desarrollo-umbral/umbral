namespace umbral_backend.Domain.Exceptions;

// Raised when a role change cannot be propagated to the identity provider (Keycloak).
// The role write is Keycloak-first, so this surfacing means the app DB was NOT updated —
// both stores still hold the old role and the admin can safely retry (503 -> retryable).
public sealed class IdentityProviderRoleSyncException : DomainException
{
    public IdentityProviderRoleSyncException(string externalIdentityId, string role, string reason)
        : base(FormatMessage(externalIdentityId, role, reason))
    {
    }

    public IdentityProviderRoleSyncException(string externalIdentityId, string role, string reason, Exception innerException)
        : base(FormatMessage(externalIdentityId, role, reason), innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.ServiceUnavailable;

    private static string FormatMessage(string externalIdentityId, string role, string reason) =>
        $"Failed to sync role '{role}' to the identity provider for user '{externalIdentityId}': {reason}";
}
