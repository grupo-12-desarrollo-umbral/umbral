using Moq;
using umbral_backend.Application.Users.Queries.GetUsers;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;

namespace umbral_backend.Application.UnitTests.Api.Services;

public sealed class UserManagementProxyTests
{
    [Fact]
    public async Task ListUsersAsync_WithAdministratorHeaders_DelegatesToRealHandler()
    {
        var currentUser = CreateCurrentUser("kc-admin-01", "Administrator", "admin@example.com");
        var expectedResult = new PagedResult<UserAccessCatalogItemDto>
        {
            Items =
            [
                new UserAccessCatalogItemDto(7, "kc-target-01", "Target User", "target@example.com", "Participant", true)
            ],
            TotalCount = 1,
            Page = 2,
            PageSize = 15
        };

        var handler = new Mock<IUserManagementHandler>();
        handler
            .Setup(service => service.ListUsersAsync(2, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var proxy = new UserManagementProxy(currentUser.Object, handler.Object);

        var result = await proxy.ListUsersAsync(2, 15, CancellationToken.None);

        result.Should().BeSameAs(expectedResult);
        handler.Verify(service => service.ListUsersAsync(2, 15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListUsersAsync_WithOperatorHeaders_DelegatesToRealHandler()
    {
        var currentUser = CreateCurrentUser("kc-operator-01", "Operator", "operator@example.com");
        var handler = new Mock<IUserManagementHandler>();
        handler
            .Setup(service => service.ListUsersAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UserAccessCatalogItemDto>());
        var proxy = new UserManagementProxy(currentUser.Object, handler.Object);

        await proxy.ListUsersAsync(1, 20, CancellationToken.None);

        handler.Verify(service => service.ListUsersAsync(1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserRoleAsync_WithOperatorHeaders_DelegatesToRealHandler()
    {
        var currentUser = CreateCurrentUser("kc-operator-01", "Operator", "operator@example.com");
        var handler = new Mock<IUserManagementHandler>();
        var proxy = new UserManagementProxy(currentUser.Object, handler.Object);

        await proxy.AssignUserRoleAsync(42, "Participant", CancellationToken.None);

        handler.Verify(service => service.AssignUserRoleAsync(42, "Participant", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAccessAsync_WithoutTrustedHeaders_RejectsBeforeDelegating()
    {
        var currentUser = CreateCurrentUser(null, "Administrator", "admin@example.com");
        var handler = new Mock<IUserManagementHandler>();
        var proxy = new UserManagementProxy(currentUser.Object, handler.Object);

        var act = async () => await proxy.DeactivateUserAccessAsync(42, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Trusted gateway identity headers are required.");
        handler.Verify(service => service.DeactivateUserAccessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role, string? email)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        currentUser.SetupGet(user => user.Email).Returns(email);
        return currentUser;
    }
}
