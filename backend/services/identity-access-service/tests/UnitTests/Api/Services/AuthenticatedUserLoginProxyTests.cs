using FluentValidation;
using Moq;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;
using umbral_backend.Application.Users.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Api.Services;

public sealed class AuthenticatedUserLoginProxyTests
{
    [Fact]
    public async Task AuthenticateAsync_WithTrustedIdentity_DelegatesToRealHandler()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-user-01");
        currentUser.SetupGet(user => user.Email).Returns("alice@example.com");
        currentUser.SetupGet(user => user.Role).Returns("Operator");

        var expectedResult = new AuthenticateUserResultDto(
            new AuthenticatedActorProfileDto(27, "kc-user-01", "Alice", "alice@example.com", "Operator", true),
            new ProtectedAccessDecisionDto("AuthenticatedPlatformAccess", true, "allowed"));

        var handler = new Mock<IAuthenticatedUserLoginHandler>();
        handler
            .Setup(service => service.HandleAsync(
                new AuthenticatedUserLoginRequest("kc-user-01", "Alice", "alice@example.com", Role.Operator),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var proxy = new AuthenticatedUserLoginProxy(currentUser.Object, handler.Object);

        var result = await proxy.AuthenticateAsync("Alice", CancellationToken.None);

        result.Should().BeSameAs(expectedResult);
        handler.Verify(service => service.HandleAsync(
            new AuthenticatedUserLoginRequest("kc-user-01", "Alice", "alice@example.com", Role.Operator),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutTrustedHeaders_RejectsRequestBeforeDelegating()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns((string?)null);
        currentUser.SetupGet(user => user.Email).Returns("alice@example.com");
        currentUser.SetupGet(user => user.Role).Returns("Operator");

        var handler = new Mock<IAuthenticatedUserLoginHandler>();
        var proxy = new AuthenticatedUserLoginProxy(currentUser.Object, handler.Object);

        var act = async () => await proxy.AuthenticateAsync("Alice", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Trusted gateway identity headers are required.");
        handler.Verify(service => service.HandleAsync(It.IsAny<AuthenticatedUserLoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnsupportedRole_RejectsRequestBeforeDelegating()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-user-01");
        currentUser.SetupGet(user => user.Email).Returns("alice@example.com");
        currentUser.SetupGet(user => user.Role).Returns("Operador");

        var handler = new Mock<IAuthenticatedUserLoginHandler>();
        var proxy = new AuthenticatedUserLoginProxy(currentUser.Object, handler.Object);

        var act = async () => await proxy.AuthenticateAsync("Alice", CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Unsupported role 'Operador'.");
        handler.Verify(service => service.HandleAsync(It.IsAny<AuthenticatedUserLoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
