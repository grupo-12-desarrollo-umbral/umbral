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

    /// <summary>
    /// Creates the local record for a user invited by an administrator. The invitee has not yet
    /// signed in, so no real display name exists — the email stands in as a placeholder until the
    /// invitee completes <c>Post-Login Provisioning</c>, which overwrites it via
    /// <see cref="SynchronizeProfile"/>. Only <see cref="Role.Operator"/> and
    /// <see cref="Role.Administrator"/> may be invited (see <see cref="EnsureInvitableRole"/>).
    /// </summary>
    public static User Invite(string externalIdentityId, string email, Role role)
    {
        EnsureInvitableRole(role);

        var trimmedEmail = RequireEmail(email);
        var user = new User(externalIdentityId, trimmedEmail, trimmedEmail, role);
        user.AddDomainEvent(new UserProvisionedEvent(user));

        return user;
    }

    /// <summary>
    /// Guards the invitation invariant: participants self-register and cannot be invited. Exposed so
    /// a caller can reject an ineligible role before provisioning any identity-provider account,
    /// while <see cref="Invite"/> re-applies it as the authoritative construction-time check.
    /// </summary>
    public static void EnsureInvitableRole(Role role)
    {
        if (role == Role.Participant)
        {
            throw new ParticipantNotInvitableException();
        }
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
