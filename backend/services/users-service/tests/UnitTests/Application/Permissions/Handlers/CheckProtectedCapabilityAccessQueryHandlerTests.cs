using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Permissions.Handlers;

public sealed class CheckProtectedCapabilityAccessQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllowedDecisionForAuthorizedRole()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-admin", "Admin User", "admin@example.com", Role.Administrator));

        var handler = new CheckProtectedCapabilityAccessQueryHandler(
            currentActor.Object,
            new AccessPolicy());

        var result = await handler.Handle(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.AdministratorPanel),
            CancellationToken.None);

        result.Capability.Should().Be("AdministratorPanel");
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RejectsUnauthorizedRoleForCapability()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-operator", "Operator User", "operator@example.com", Role.Operator));

        var handler = new CheckProtectedCapabilityAccessQueryHandler(
            currentActor.Object,
            new AccessPolicy());

        var act = async () => await handler.Handle(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.AdministratorPanel),
            CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedUser()
    {
        var user = User.Provision("kc-deactivated", "Inactive User", "inactive@example.com", Role.Operator);
        user.DeactivateAccess();

        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new CheckProtectedCapabilityAccessQueryHandler(
            currentActor.Object,
            new AccessPolicy());

        var act = async () => await handler.Handle(
            new CheckProtectedCapabilityAccessQuery(ProtectedCapability.OperatorPanel),
            CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
    }
}
