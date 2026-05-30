using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Domain.Services;

public sealed class IdentityProvisioningPolicyTests
{
    private readonly IdentityProvisioningPolicy _policy = new();

    [Fact]
    public void SynchronizeOrCreate_WhenUserDoesNotExist_ProvisionNewUser()
    {
        var user = _policy.SynchronizeOrCreate(
            existingUser: null,
            externalIdentityId: "kc-01",
            displayName: "Ada",
            email: "ada@example.com",
            role: Role.Operator);

        user.ExternalIdentityId.Should().Be("kc-01");
        user.DisplayName.Should().Be("Ada");
        user.Role.Should().Be(Role.Operator);
    }

    [Fact]
    public void SynchronizeOrCreate_WhenUserExists_SynchronizesProfileAndRole()
    {
        var existingUser = User.Provision("kc-02", "Initial", "initial@example.com", Role.Operator);
        existingUser.ClearDomainEvents();

        var user = _policy.SynchronizeOrCreate(
            existingUser,
            "kc-02",
            "Grace",
            "grace@example.com",
            Role.Administrator);

        user.Should().BeSameAs(existingUser);
        user.DisplayName.Should().Be("Grace");
        user.Email.Should().Be("grace@example.com");
        user.Role.Should().Be(Role.Administrator);
    }

    [Fact]
    public void SynchronizeOrCreate_WhenIdentityDoesNotMatch_ThrowsException()
    {
        var existingUser = User.Provision("kc-03", "Initial", "initial@example.com", Role.Operator);

        FluentActions.Invoking(() => _policy.SynchronizeOrCreate(
                existingUser,
                "other-id",
                "Grace",
                "grace@example.com",
                Role.Operator))
            .Should().Throw<ExternalIdentityMismatchException>();
    }
}
