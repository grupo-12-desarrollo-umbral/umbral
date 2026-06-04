using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.ListAssignableSessions;

public sealed class ListAssignableSessionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_AsAdministrator_ReturnsUnfilteredRepositoryProjection()
    {
        var expected = new[]
        {
            new SessionOperatorSummaryDto(
                Guid.NewGuid(),
                "SES-123456",
                "Museum Hunt",
                "Scheduled",
                27,
                new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 4, 9, 55, 0, TimeSpan.Zero))
        };

        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListAssignableSummariesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var currentUser = CreateCurrentUser("kc-admin-01", "Administrator");
        var actorClient = CreateActorClient(99, "kc-admin-01", "Administrator");

        var handler = new ListAssignableSessionsQueryHandler(repository.Object, currentUser.Object, actorClient.Object);

        var result = await handler.Handle(new ListAssignableSessionsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        repository.Verify(repo => repo.ListAssignableSummariesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AsOperator_ReturnsOperatorScopedProjection()
    {
        var expected = new[]
        {
            new SessionOperatorSummaryDto(
                Guid.NewGuid(),
                "SES-654321",
                "Night Shift",
                "Active",
                27,
                new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 4, 12, 5, 0, TimeSpan.Zero))
        };

        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListAssignableSummariesAsync(27, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var currentUser = CreateCurrentUser("kc-operator-27", "Operator");
        var actorClient = CreateActorClient(27, "kc-operator-27", "Operator");

        var handler = new ListAssignableSessionsQueryHandler(repository.Object, currentUser.Object, actorClient.Object);

        var result = await handler.Handle(new ListAssignableSessionsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        repository.Verify(repo => repo.ListAssignableSummariesAsync(27, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }

    private static Mock<IAuthenticatedActorProfileAccessClient> CreateActorClient(int userId, string externalIdentityId, string role)
    {
        var actorClient = new Mock<IAuthenticatedActorProfileAccessClient>();
        actorClient
            .Setup(client => client.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedActorProfileLookupDto(userId, externalIdentityId, role, true));
        return actorClient;
    }
}
