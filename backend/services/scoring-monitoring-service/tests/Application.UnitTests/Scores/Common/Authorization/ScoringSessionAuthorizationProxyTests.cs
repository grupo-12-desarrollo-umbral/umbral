using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common.Authorization;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Common.Authorization;

public sealed class ScoringSessionAuthorizationProxyTests
{
    private static Mock<ICurrentUser> CurrentUser(string? id = null, string? role = null)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.Id).Returns(id);
        user.SetupGet(u => u.Role).Returns(role);
        return user;
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenActorIsBlank_ThrowsUnauthorizedAccessException()
    {
        var user = CurrentUser(id: null, role: null);
        var repo = new Mock<ISessionAssignmentReadRepository>();
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenActorIsAdministrator_Passes()
    {
        var user = CurrentUser(id: "42", role: "Administrator");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenActorIsAssignedOperator_Passes()
    {
        var liveSessionId = Guid.NewGuid();
        var assignedOperatorUserId = Guid.NewGuid();
        var user = CurrentUser(id: assignedOperatorUserId.ToString(), role: "Operator");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        repo.Setup(r => r.GetAssignedOperatorUserIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignedOperatorUserId);
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(liveSessionId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenOperatorIsNotAssignedToSession_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var assignedOperatorUserId = Guid.NewGuid();
        var otherOperatorId = Guid.NewGuid();
        var user = CurrentUser(id: otherOperatorId.ToString(), role: "Operator");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        repo.Setup(r => r.GetAssignedOperatorUserIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignedOperatorUserId);
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(liveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenOperatorHasNoProjectionData_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var user = CurrentUser(id: Guid.NewGuid().ToString(), role: "Operator");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        repo.Setup(r => r.GetAssignedOperatorUserIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(liveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenActorIsNotOperatorOrAdministrator_ThrowsForbiddenAccessException()
    {
        var user = CurrentUser(id: "42", role: "Participant");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureAccessAsync_WhenActorIdCannotBeParsedAsGuid_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var assignedOperatorUserId = Guid.NewGuid();
        var user = CurrentUser(id: "not-a-guid", role: "Operator");
        var repo = new Mock<ISessionAssignmentReadRepository>();
        repo.Setup(r => r.GetAssignedOperatorUserIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignedOperatorUserId);
        var proxy = new ScoringSessionAuthorizationProxy(user.Object, repo.Object);

        var act = () => proxy.EnsureAccessAsync(liveSessionId, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
