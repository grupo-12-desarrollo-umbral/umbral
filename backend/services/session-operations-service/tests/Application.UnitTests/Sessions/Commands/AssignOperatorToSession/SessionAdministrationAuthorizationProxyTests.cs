using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssignOperatorToSession;

public sealed class SessionAdministrationAuthorizationProxyTests
{
    [Fact]
    public async Task GetAuthorizedSessionAsync_WhenCallerIsAdministrator_ReturnsSession()
    {
        var session = CreateScheduledSession();
        var proxy = CreateProxy(session, "99", "Administrator");

        var result = await proxy.GetAuthorizedSessionAsync(session.LiveSessionId, CancellationToken.None);

        result.Should().BeSameAs(session);
    }

    [Fact]
    public async Task GetAuthorizedSessionAsync_WhenCallerIsAssignedOperator_ReturnsSession()
    {
        var session = CreateScheduledSession();
        session.AssignOperator(27, DateTimeOffset.UtcNow);
        var proxy = CreateProxy(session, "kc-operator-27", "Operator", resolvedUserId: 27);

        var result = await proxy.GetAuthorizedSessionAsync(session.LiveSessionId, CancellationToken.None);

        result.Should().BeSameAs(session);
    }

    [Fact]
    public async Task GetAuthorizedSessionWithActorAsync_WhenCallerIsAssignedOperator_SurfacesNumericUserId()
    {
        var session = CreateScheduledSession();
        session.AssignOperator(27, DateTimeOffset.UtcNow);
        var proxy = CreateProxy(session, "kc-operator-27", "Operator", resolvedUserId: 27);

        var result = await proxy.GetAuthorizedSessionWithActorAsync(
            session.LiveSessionId,
            CancellationToken.None);

        result.Session.Should().BeSameAs(session);
        result.ResponsibleUserId.Should().Be(27);
    }

    [Fact]
    public async Task GetAuthorizedSessionWithActorAsync_WhenCallerIsAdministrator_ReturnsNullActor()
    {
        var session = CreateScheduledSession();
        var proxy = CreateProxy(session, "99", "Administrator");

        var result = await proxy.GetAuthorizedSessionWithActorAsync(
            session.LiveSessionId,
            CancellationToken.None);

        result.Session.Should().BeSameAs(session);
        result.ResponsibleUserId.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthorizedSessionAsync_WhenCallerIsDifferentOperator_ThrowsForbiddenException()
    {
        var session = CreateScheduledSession();
        session.AssignOperator(27, DateTimeOffset.UtcNow);
        var proxy = CreateProxy(session, "kc-operator-31", "Operator", resolvedUserId: 31);

        var act = async () => await proxy.GetAuthorizedSessionAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task GetAuthorizedSessionAsync_WhenCallerHasNoIdentity_ThrowsUnauthorizedException()
    {
        var session = CreateScheduledSession();
        var proxy = CreateProxy(session, null, null);

        var act = async () => await proxy.GetAuthorizedSessionAsync(session.LiveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetAuthorizedTimerSessionAsync_WhenCallerIsAssignedOperator_ReturnsSession()
    {
        var session = CreateScheduledSession();
        session.AssignOperator(27, DateTimeOffset.UtcNow);
        var proxy = CreateProxy(session, "kc-operator-27", "Operator", resolvedUserId: 27);

        var result = await proxy.GetAuthorizedTimerSessionAsync(session.LiveSessionId, CancellationToken.None);

        result.Should().BeSameAs(session);
    }

    private static SessionAdministrationAuthorizationProxy CreateProxy(
        LiveSession session,
        string? id,
        string? role,
        int resolvedUserId = 99)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);

        var actorClient = new Mock<IAuthenticatedActorProfileAccessClient>();
        actorClient
            .Setup(client => client.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedActorProfileLookupDto(
                resolvedUserId,
                id ?? "missing",
                role ?? "Unknown",
                true));

        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.GetTimerSessionByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return new SessionAdministrationAuthorizationProxy(currentUser.Object, repository.Object, actorClient.Object);
    }

    private static LiveSession CreateScheduledSession()
    {
        return LiveSessionTestFactory.CreateScheduledTreasureHunt(
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero));
    }
}
