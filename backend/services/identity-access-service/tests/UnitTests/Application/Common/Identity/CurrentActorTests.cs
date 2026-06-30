using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Identity;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Common.Identity;

public sealed class CurrentActorTests
{
    [Fact]
    public async Task GetActorAsync_ResolvesActorFromCurrentUserId()
    {
        var actor = CreateUser("kc-admin", Role.Administrator);
        var repository = CreateRepository(actor);

        var sut = new CurrentActor(CreateCurrentUser(actor.ExternalIdentityId).Object, repository.Object);

        var resolved = await sut.GetActorAsync(CancellationToken.None);

        resolved.Should().BeSameAs(actor);
    }

    [Fact]
    public async Task GetActorAsync_MemoizesWithinScope_DoesNotReadRepositoryTwice()
    {
        var actor = CreateUser("kc-admin", Role.Administrator);
        var repository = CreateRepository(actor);

        var sut = new CurrentActor(CreateCurrentUser(actor.ExternalIdentityId).Object, repository.Object);

        await sut.GetActorAsync(CancellationToken.None);
        await sut.GetActorAsync(CancellationToken.None);

        repository.Verify(
            repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActorAsync_WithoutTrustedIdentity_Throws()
    {
        var sut = new CurrentActor(CreateCurrentUser(null).Object, new Mock<IUserRepository>().Object);

        var act = async () => await sut.GetActorAsync(CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetActorAsync_WhenActorMissing_ThrowsNotFound()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new CurrentActor(CreateCurrentUser("kc-missing").Object, repository.Object);

        var act = async () => await sut.GetActorAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static Mock<IUserRepository> CreateRepository(User actor)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        return repository;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        return currentUser;
    }

    private static User CreateUser(string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = 1;
        return user;
    }
}
