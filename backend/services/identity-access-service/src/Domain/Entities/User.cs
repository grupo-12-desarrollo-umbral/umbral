using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class User : BaseAuditableEntity
{
    private User()
    {
        ExternalIdentityId = string.Empty;
        DisplayName = string.Empty;
        Email = string.Empty;
    }

    private User(string externalIdentityId, string displayName, string email, Role role)
    {
        ExternalIdentityId = RequireExternalIdentityId(externalIdentityId);
        DisplayName = RequireDisplayName(displayName);
        Email = RequireEmail(email);
        Role = role;
        IsActive = true;
    }

    public string ExternalIdentityId { get; private set; }

    public string DisplayName { get; private set; }

    public string Email { get; private set; }

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public static User Provision(string externalIdentityId, string displayName, string email, Role role)
    {
        var user = new User(externalIdentityId, displayName, email, role);
        user.AddDomainEvent(new UserProvisionedEvent(user));

        return user;
    }

    public void SynchronizeProfile(string displayName, string email)
    {
        DisplayName = RequireDisplayName(displayName);
        Email = RequireEmail(email);
    }

    public void AssignRole(Role role)
    {
        if (!IsActive)
        {
            throw new DeactivatedUserRoleAssignmentNotAllowedException(Id);
        }

        if (Role == role)
        {
            return;
        }

        var previousRole = Role;
        Role = role;
        AddDomainEvent(new UserRoleRevokedEvent(this, previousRole, role));
        AddDomainEvent(new UserRoleAssignedEvent(this, previousRole, role));
    }

    public void DeactivateAccess()
    {
        if (!IsActive)
        {
            throw new UserAccessAlreadyDeactivatedException(Id);
        }

        IsActive = false;
        AddDomainEvent(new UserAccessDeactivatedEvent(this));
    }

    public void ReactivateAccess()
    {
        if (IsActive)
        {
            throw new UserAccessAlreadyActiveException(Id);
        }

        IsActive = true;
        AddDomainEvent(new UserAccessReactivatedEvent(this));
    }

    public void RecordAccessDecision(AccessDecision decision)
    {
        AddDomainEvent(new AccessDecisionRecordedEvent(this, decision));
    }

    private static string RequireExternalIdentityId(string externalIdentityId)
    {
        if (string.IsNullOrWhiteSpace(externalIdentityId))
        {
            throw new ExternalIdentityIdRequiredException();
        }

        return externalIdentityId.Trim();
    }

    private static string RequireDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new UserDisplayNameRequiredException();
        }

        return displayName.Trim();
    }

    private static string RequireEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserEmailRequiredException();
        }

        return email.Trim();
    }
}
