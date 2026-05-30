using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public sealed class AccessPolicyTests
{
    private readonly AccessPolicy _policy = new();

    [Fact]
    public void Evaluate_ReturnsAllowedForAuthenticatedPlatformAccess()
    {
        var user = User.Provision("kc-01", "Ada", "ada@example.com", Role.Operator);

        var decision = _policy.Evaluate(user, ProtectedCapability.AuthenticatedPlatformAccess);

        decision.IsAllowed.Should().BeTrue();
        decision.Reason.Should().Be("Access granted for role.");
    }

    [Theory]
    [InlineData(Role.Administrator, ProtectedCapability.AdministratorPanel, true)]
    [InlineData(Role.Operator, ProtectedCapability.OperatorPanel, true)]
    [InlineData(Role.Administrator, ProtectedCapability.OperatorPanel, true)]
    [InlineData(Role.Participant, ProtectedCapability.ParticipantExperience, true)]
    [InlineData(Role.Administrator, ProtectedCapability.UserAccessCatalog, true)]
    [InlineData(Role.Operator, ProtectedCapability.UserAccessCatalog, true)]
    [InlineData(Role.Operator, ProtectedCapability.AdministratorPanel, false)]
    [InlineData(Role.Operator, ProtectedCapability.ParticipantExperience, false)]
    [InlineData(Role.Participant, ProtectedCapability.UserAccessCatalog, false)]
    public void Evaluate_ResolvesRoleCapabilityMatrix(Role role, ProtectedCapability capability, bool expected)
    {
        var user = User.Provision("kc-matrix", "Matrix", "matrix@example.com", role);

        var decision = _policy.Evaluate(user, capability);

        decision.IsAllowed.Should().Be(expected);
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
}
