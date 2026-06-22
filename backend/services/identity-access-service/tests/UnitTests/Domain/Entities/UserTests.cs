using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public sealed class UserTests
{
    [Fact]
    public void Provision_WithValidValues_CreatesActiveUserAndRaisesProvisionedEvent()
    {
        var user = User.Provision(" kc-01 ", " Ada ", " ada@example.com ", Role.Operator);

        user.ExternalIdentityId.Should().Be("kc-01");
        user.DisplayName.Should().Be("Ada");
        user.Email.Should().Be("ada@example.com");
        user.Role.Should().Be(Role.Operator);
        user.IsActive.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserProvisionedEvent>();
    }

    [Fact]
    public void SynchronizeProfileAndAssignRole_UpdateStateAndRecordRoleEvent()
    {
        var user = User.Provision("kc-02", "Initial", "initial@example.com", Role.Operator);
        user.ClearDomainEvents();

        user.SynchronizeProfile("Grace Hopper", "grace@example.com");
        user.AssignRole(Role.Administrator);

        user.DisplayName.Should().Be("Grace Hopper");
        user.Email.Should().Be("grace@example.com");
        user.Role.Should().Be(Role.Administrator);
        var domainEvents = user.DomainEvents.ToArray();

        domainEvents.Should().HaveCount(2);
        domainEvents[0].Should().BeOfType<UserRoleRevokedEvent>();
        domainEvents[1].Should().BeOfType<UserRoleAssignedEvent>();
    }

    [Fact]
    public void AssignRole_WhenRoleIsUnchanged_DoesNotRaiseEvent()
    {
        var user = User.Provision("kc-03", "Same Role", "same@example.com", Role.Operator);
        user.ClearDomainEvents();

        user.AssignRole(Role.Operator);

        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignRole_WhenUserIsDeactivated_ThrowsException()
    {
        var user = User.Provision("kc-08", "Inactive", "inactive@example.com", Role.Operator);
        user.DeactivateAccess();
        user.ClearDomainEvents();

        FluentActions.Invoking(() => user.AssignRole(Role.Administrator))
            .Should().Throw<DeactivatedUserRoleAssignmentNotAllowedException>();

        user.Role.Should().Be(Role.Operator);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignRole_WhenUserIsDeactivatedAndRoleIsUnchanged_ThrowsException()
    {
        var user = User.Provision("kc-09", "Inactive", "inactive@example.com", Role.Operator);
        user.DeactivateAccess();
        user.ClearDomainEvents();

        // A deactivated user is never a valid target for role assignment — not even a same-role
        // no-op — so the active-state guard runs before the unchanged-role short-circuit.
        FluentActions.Invoking(() => user.AssignRole(Role.Operator))
            .Should().Throw<DeactivatedUserRoleAssignmentNotAllowedException>();

        user.Role.Should().Be(Role.Operator);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DeactivateAccess_WhenCalledTwice_ThrowsException()
    {
        var user = User.Provision("kc-04", "Deactivate Me", "deactivate@example.com", Role.Operator);

        user.DeactivateAccess();

        user.IsActive.Should().BeFalse();
        user.ExternalIdentityId.Should().Be("kc-04");
        user.DisplayName.Should().Be("Deactivate Me");
        user.Email.Should().Be("deactivate@example.com");
        user.Role.Should().Be(Role.Operator);
        user.DomainEvents.Should().ContainSingle(eventItem => eventItem is UserAccessDeactivatedEvent);

        FluentActions.Invoking(user.DeactivateAccess)
            .Should().Throw<UserAccessAlreadyDeactivatedException>();
    }

    [Fact]
    public void RecordAccessDecisionAndStartIdentityProviderSession_AddDomainEventsAndChildren()
    {
        var user = User.Provision("kc-05", "Alice", "alice@example.com", Role.Administrator);
        user.Id = 1;
        user.ClearDomainEvents();
        var decision = global::umbral_backend.Domain.ValueObjects.AccessDecision.Allow(ProtectedCapability.AdministratorPanel, "allowed");

        user.RecordAccessDecision(decision);
        var session = user.StartIdentityProviderSession(
            "Keycloak",
            "sid-100",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

        user.IdentityProviderSessions.Should().ContainSingle().Which.Should().BeSameAs(session);
        user.DomainEvents.Should().Contain(eventItem => eventItem is AccessDecisionRecordedEvent);
        session.DomainEvents.Should().Contain(eventItem => eventItem is IdentityProviderSessionStartedEvent);
    }

    [Fact]
    public void ProvisionAndSynchronize_WithInvalidValues_ThrowExpectedExceptions()
    {
        FluentActions.Invoking(() => User.Provision(" ", "Ada", "ada@example.com", Role.Operator))
            .Should().Throw<ExternalIdentityIdRequiredException>();
        FluentActions.Invoking(() => User.Provision("kc-06", " ", "ada@example.com", Role.Operator))
            .Should().Throw<UserDisplayNameRequiredException>();
        FluentActions.Invoking(() => User.Provision("kc-06", "Ada", " ", Role.Operator))
            .Should().Throw<UserEmailRequiredException>();

        var user = User.Provision("kc-07", "Initial", "initial@example.com", Role.Operator);
        FluentActions.Invoking(() => user.SynchronizeProfile(" ", "updated@example.com"))
            .Should().Throw<UserDisplayNameRequiredException>();
        FluentActions.Invoking(() => user.SynchronizeProfile("Updated", " "))
            .Should().Throw<UserEmailRequiredException>();
    }
}
