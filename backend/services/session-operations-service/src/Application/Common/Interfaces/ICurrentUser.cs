namespace umbral_backend.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? Id { get; }

    string? Email { get; }

    string? Role { get; }

    // Human-readable name for display in a session. Resolved from the identity token, with an
    // email local-part fallback for users whose Keycloak profile carries no name.
    string DisplayName { get; }
}
