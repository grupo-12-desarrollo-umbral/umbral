using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Handlers;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class GetAuthenticatedActorProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentAuthenticatedActorProfile()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-010");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-010", It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-010", "Jane Doe", "jane@example.com", Domain.Enums.Role.Operator));

        var handler = new GetAuthenticatedActorProfileQueryHandler(repository.Object, currentUser.Object);

        var result = await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        result.ExternalIdentityId.Should().Be("kc-010");
        result.DisplayName.Should().Be("Jane Doe");
        result.Email.Should().Be("jane@example.com");
        result.Role.Should().Be("Operator");
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns((string?)null);

        var repository = new Mock<IUserRepository>();
        var handler = new GetAuthenticatedActorProfileQueryHandler(repository.Object, currentUser.Object);

        var act = async () => await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenProvisionedUserDoesNotExist()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("kc-missing");

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync("kc-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new GetAuthenticatedActorProfileQueryHandler(repository.Object, currentUser.Object);

        var act = async () => await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
