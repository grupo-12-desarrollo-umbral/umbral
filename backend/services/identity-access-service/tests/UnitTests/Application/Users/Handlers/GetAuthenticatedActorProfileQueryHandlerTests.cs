using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Queries.GetAuthenticatedActorProfile;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Users.Handlers;

public sealed class GetAuthenticatedActorProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentAuthenticatedActorProfile()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-010", "Jane Doe", "jane@example.com", global::umbral_backend.Domain.Enums.Role.Operator));

        var handler = new GetAuthenticatedActorProfileQueryHandler(currentActor.Object);

        var result = await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        result.UserId.Should().Be(0);
        result.ExternalIdentityId.Should().Be("kc-010");
        result.DisplayName.Should().Be("Jane Doe");
        result.Email.Should().Be("jane@example.com");
        result.Role.Should().Be("Operator");
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var handler = new GetAuthenticatedActorProfileQueryHandler(currentActor.Object);

        var act = async () => await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenProvisionedUserDoesNotExist()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(User), "kc-missing"));

        var handler = new GetAuthenticatedActorProfileQueryHandler(currentActor.Object);

        var act = async () => await handler.Handle(new GetAuthenticatedActorProfileQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
