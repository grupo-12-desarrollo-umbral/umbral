using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public sealed class AccessPolicyTests
{
    private readonly AccessPolicy _policy = new();
    private static readonly IReadOnlyDictionary<ProtectedCapability, Role[]> CapabilityMatrix =
        new Dictionary<ProtectedCapability, Role[]>
        {
            [ProtectedCapability.AuthenticatedPlatformAccess] = Enum.GetValues<Role>(),
            [ProtectedCapability.AdministratorPanel] = new[] { Role.Administrator },
            [ProtectedCapability.OperatorPanel] = new[] { Role.Administrator, Role.Operator },
            [ProtectedCapability.ParticipantExperience] = new[] { Role.Participant },
            [ProtectedCapability.UserAccessCatalog] = new[] { Role.Administrator }
        };

    [Fact]
    public void Evaluate_ReturnsAllowedForAuthenticatedPlatformAccess()
    {
        var user = User.Provision("kc-01", "Ada", "ada@example.com", Role.Operator);

        var decision = _policy.Evaluate(user, ProtectedCapability.AuthenticatedPlatformAccess);

        decision.IsAllowed.Should().BeTrue();
        decision.Reason.Should().Be("Access granted for role.");
    }

    [Fact]
    public void Evaluate_ResolvesRoleCapabilityMatrix()
    {
        CapabilityMatrix.Keys.Should().BeEquivalentTo(Enum.GetValues<ProtectedCapability>());

        foreach (var capability in Enum.GetValues<ProtectedCapability>())
        {
            foreach (var role in Enum.GetValues<Role>())
            {
                var user = User.Provision($"kc-{capability}-{role}", "Matrix", "matrix@example.com", role);

                var decision = _policy.Evaluate(user, capability);

                decision.IsAllowed.Should().Be(
                    CapabilityMatrix[capability].Contains(role),
                    $"role '{role}' should match the defined matrix for capability '{capability}'");
            }
        }
    }

    [Fact]
    public void EnsureCanAccess_WhenUserIsDeactivated_ThrowsException()
    {
        var user = User.Provision("kc-02", "Inactive", "inactive@example.com", Role.Operator);
        user.DeactivateAccess();

        FluentActions.Invoking(() => _policy.EnsureCanAccess(user, ProtectedCapability.OperatorPanel))
            .Should().Throw<DeactivatedUserAccessDeniedException>();
    }

    [Fact]
    public void EnsureCanAccess_WhenRoleIsDenied_ThrowsException()
    {
        var user = User.Provision("kc-03", "Participant", "participant@example.com", Role.Participant);

        FluentActions.Invoking(() => _policy.EnsureCanAccess(user, ProtectedCapability.UserAccessCatalog))
            .Should().Throw<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public void Evaluate_WhenCapabilityIsUnknown_DeniesAccess()
    {
        var user = User.Provision("kc-04", "Fallback", "fallback@example.com", Role.Administrator);

        var decision = _policy.Evaluate(user, (ProtectedCapability)999);

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Be("Role is not authorized for capability.");
    }
}
