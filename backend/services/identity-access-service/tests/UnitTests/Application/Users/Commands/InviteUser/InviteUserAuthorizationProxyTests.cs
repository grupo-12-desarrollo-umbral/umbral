using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.InviteUser;
using umbral_backend.Application.Users.Common.Authorization;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.InviteUser;

public sealed class InviteUserAuthorizationProxyTests
{
    private const string Email = "invitee@example.com";

    // The inner handler reaching IIdentityProviderAdminService.CreateUserAsync is the observable proof
    // that the guard let the request through; Times.Never proves the guard short-circuited before it.
    [Fact]
    public async Task Handle_WithAdministratorActor_DelegatesToInnerHandler()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var identityProviderAdmin = CreateIdentityProvider();
        var proxy = CreateProxy(CreateCurrentActor(actor), CreateRepository(), identityProviderAdmin);

        await proxy.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        identityProviderAdmin.Verify(a => a.CreateUserAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutTrustedActorIdentity_RejectsBeforeDelegating()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var identityProviderAdmin = CreateIdentityProvider();
        var proxy = CreateProxy(currentActor, CreateRepository(), identityProviderAdmin);

        var act = async () => await proxy.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyInnerNeverRan(identityProviderAdmin);
    }

    [Fact]
    public async Task Handle_WithNonAdministratorActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var identityProviderAdmin = CreateIdentityProvider();
        var proxy = CreateProxy(CreateCurrentActor(actor), CreateRepository(), identityProviderAdmin);

        var act = async () => await proxy.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
        VerifyInnerNeverRan(identityProviderAdmin);
    }

    [Fact]
    public async Task Handle_WithDeactivatedAdministratorActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        actor.DeactivateAccess();
        var identityProviderAdmin = CreateIdentityProvider();
        var proxy = CreateProxy(CreateCurrentActor(actor), CreateRepository(), identityProviderAdmin);

        var act = async () => await proxy.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        VerifyInnerNeverRan(identityProviderAdmin);
    }

    [Fact]
    public async Task Handle_WhenActorIsMissing_RejectsBeforeDelegating()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(User), "kc-missing"));
        var identityProviderAdmin = CreateIdentityProvider();
        var proxy = CreateProxy(currentActor, CreateRepository(), identityProviderAdmin);

        var act = async () => await proxy.Handle(new InviteUserCommand(Email, "Operator"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyInnerNeverRan(identityProviderAdmin);
    }

    private static InviteUserAuthorizationProxy CreateProxy(
        Mock<ICurrentActor> currentActor,
        Mock<IUserRepository> userRepository,
        Mock<IIdentityProviderAdminService> identityProviderAdmin)
    {
        var inner = new InviteUserCommandHandler(userRepository.Object, identityProviderAdmin.Object);
        return new InviteUserAuthorizationProxy(currentActor.Object, new AccessPolicy(), inner);
    }

    private static Mock<IUserRepository> CreateRepository()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return repository;
    }

    private static Mock<IIdentityProviderAdminService> CreateIdentityProvider()
    {
        var identityProviderAdmin = new Mock<IIdentityProviderAdminService>();
        identityProviderAdmin
            .Setup(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("kc-invited-01");
        return identityProviderAdmin;
    }

    private static void VerifyInnerNeverRan(Mock<IIdentityProviderAdminService> identityProviderAdmin)
    {
        identityProviderAdmin.Verify(
            a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ICurrentActor> CreateCurrentActor(User actor)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        return currentActor;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
