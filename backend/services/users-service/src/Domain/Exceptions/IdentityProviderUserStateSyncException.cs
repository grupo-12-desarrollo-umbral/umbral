namespace umbral_backend.Domain.Exceptions;

// Raised when a user's active/enabled state cannot be propagated to the identity provider
// (Keycloak). The state write is Keycloak-first, so this surfacing means the app DB was NOT
// updated — both stores still hold the old state and the admin can safely retry (503 -> retryable).
// Mirrors IdentityProviderRoleSyncException.
public sealed class IdentityProviderUserStateSyncException : DomainException
{
    public IdentityProviderUserStateSyncException(string externalIdentityId, bool isActive, string reason)
        : base(FormatMessage(externalIdentityId, isActive, reason))
    {
    }

    public IdentityProviderUserStateSyncException(string externalIdentityId, bool isActive, string reason, Exception innerException)
        : base(FormatMessage(externalIdentityId, isActive, reason), innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.ServiceUnavailable;

    private static string FormatMessage(string externalIdentityId, bool isActive, string reason) =>
        $"Failed to sync active-state '{isActive}' to the identity provider for user '{externalIdentityId}': {reason}";
}
