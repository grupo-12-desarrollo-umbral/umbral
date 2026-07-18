using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Users.Common.Authorization;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AssignUserRole;

public sealed class UserRoleAssignmentAuthorizationProxyTests
{
    // The inner handler reaching IUserRepository.GetByIdAsync is the observable proof that the
    // guard let the request through; Times.Never proves the guard short-circuited before it.
    [Fact]
    public async Task Handle_WithAdministratorActor_DelegatesToInnerHandler()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var userRepository = CreateUserRepositoryWithTarget(CreateUser(7, "kc-target", Role.Operator));
        var proxy = CreateProxy(CreateCurrentActor(actor), userRepository);

        await proxy.Handle(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        userRepository.Verify(repository => repository.GetByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutTrustedActorIdentity_RejectsBeforeDelegating()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var userRepository = new Mock<IUserRepository>();
        var proxy = CreateProxy(currentActor, userRepository);

        var act = async () => await proxy.Handle(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        VerifyInnerNeverRan(userRepository);
    }

    [Fact]
    public async Task Handle_WithNonAdministratorActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var userRepository = new Mock<IUserRepository>();
        var proxy = CreateProxy(CreateCurrentActor(actor), userRepository);

        var act = async () => await proxy.Handle(new AssignUserRoleCommand(7, "Administrator"), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
        VerifyInnerNeverRan(userRepository);
    }

    [Fact]
    public async Task Handle_WithDeactivatedActor_RejectsBeforeDelegating()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        actor.DeactivateAccess();
        var userRepository = new Mock<IUserRepository>();
        var proxy = CreateProxy(CreateCurrentActor(actor), userRepository);

        var act = async () => await proxy.Handle(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        VerifyInnerNeverRan(userRepository);
    }

    [Fact]
    public async Task Handle_WhenActorIsMissing_RejectsBeforeDelegating()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(User), "kc-missing"));
        var userRepository = new Mock<IUserRepository>();
        var proxy = CreateProxy(currentActor, userRepository);

        var act = async () => await proxy.Handle(new AssignUserRoleCommand(7, "Participant"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyInnerNeverRan(userRepository);
    }

    private static UserRoleAssignmentAuthorizationProxy CreateProxy(
        Mock<ICurrentActor> currentActor,
        Mock<IUserRepository> userRepository)
    {
        var inner = new AssignUserRoleCommandHandler(userRepository.Object, Mock.Of<IIdentityProviderAdminService>());
        return new UserRoleAssignmentAuthorizationProxy(currentActor.Object, new AccessPolicy(), inner);
    }

    private static Mock<IUserRepository> CreateUserRepositoryWithTarget(User target)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        return repository;
    }

    private static void VerifyInnerNeverRan(Mock<IUserRepository> userRepository)
    {
        userRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
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
