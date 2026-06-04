using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Users.Handlers;
using umbral_backend.Application.Users.Queries.GetUsers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class GetUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPagedUserCatalogForAuthorizedActor()
    {
        var actor = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        actor.Id = 2;
        var listedUser = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        listedUser.Id = 7;

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-admin");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        repository
            .Setup(repo => repo.ListAsync(2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<User>
            {
                Items = new[] { listedUser },
                TotalCount = 11,
                Page = 2,
                PageSize = 10
            });

        var handler = new GetUsersQueryHandler(repository.Object, currentUser.Object, new AccessPolicy());

        var result = await handler.Handle(new GetUsersQuery(2, 10), CancellationToken.None);

        result.TotalCount.Should().Be(11);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.Items.Should().ContainSingle();
        result.Items.Single().DisplayName.Should().Be("Admin");
        result.Items.Single().Role.Should().Be("Administrator");
    }

    [Fact]
    public async Task Handle_RejectsDeactivatedActor()
    {
        var actor = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        actor.Id = 2;
        actor.DeactivateAccess();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-admin");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var handler = new GetUsersQueryHandler(repository.Object, currentUser.Object, new AccessPolicy());

        var act = async () => await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<DeactivatedUserAccessDeniedException>();
        repository.Verify(repo => repo.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AllowsOperatorActor()
    {
        var actor = User.Provision("kc-operator", "Operator", "operator@example.com", Role.Operator);
        actor.Id = 2;
        var listedUser = User.Provision("kc-admin", "Admin", "admin@example.com", Role.Administrator);
        listedUser.Id = 7;

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-operator");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        repository
            .Setup(repo => repo.ListAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<User>
            {
                Items = new[] { listedUser },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        var handler = new GetUsersQueryHandler(repository.Object, currentUser.Object, new AccessPolicy());

        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.Single().Role.Should().Be("Administrator");
        repository.Verify(repo => repo.ListAsync(1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
