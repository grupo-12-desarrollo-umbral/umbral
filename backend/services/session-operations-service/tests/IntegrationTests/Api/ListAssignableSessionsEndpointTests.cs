using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ListAssignableSessionsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ListAssignableSessionsEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            27,
            "kc-operator-27",
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ListAssignableSessions_WithAdministratorCaller_ReturnsNonTerminalSessionsOrderedBySchedule()
    {
        var mostRecent = await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            assignedOperatorUserId: 27);
        var older = await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero),
            state: SessionState.Active);
        await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 9, 0, 0, TimeSpan.Zero),
            state: SessionState.Finished);
        await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 8, 0, 0, TimeSpan.Zero),
            state: SessionState.Cancelled);

        AddTrustedHeaders(_client, "99", "Administrator", "admin@example.com");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<SessionOperatorSummaryResponse>>();
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(2);
        payload.Select(item => item.LiveSessionId).Should().ContainInOrder(mostRecent.LiveSessionId, older.LiveSessionId);
        payload[0].AssignedOperatorUserId.Should().Be(27);
        payload[0].SessionState.Should().Be("Scheduled");
        payload[1].SessionState.Should().Be("Active");
    }

    [Fact]
    public async Task ListAssignableSessions_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListAssignableSessions_WithAssignedOperatorCaller_ReturnsOwnedSessionsIncludingConcluded()
    {
        var owned = await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
            assignedOperatorUserId: 27,
            title: "Owned Session");
        await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 11, 0, 0, TimeSpan.Zero),
            assignedOperatorUserId: 99,
            title: "Other Operator Session");
        var finished = await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero),
            assignedOperatorUserId: 27,
            state: SessionState.Finished,
            title: "Finished Session");
        var cancelled = await SeedSessionAsync(
            scheduledAt: new DateTimeOffset(2026, 6, 4, 9, 0, 0, TimeSpan.Zero),
            assignedOperatorUserId: 27,
            state: SessionState.Cancelled,
            title: "Cancelled Session");

        AddTrustedHeaders(_client, "kc-operator-27", "Operator", "operator@example.com");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<SessionOperatorSummaryResponse>>();
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(3);
        payload.Select(item => item.LiveSessionId)
            .Should()
            .ContainInOrder(owned.LiveSessionId, finished.LiveSessionId, cancelled.LiveSessionId);
        payload.Should().OnlyContain(item => item.AssignedOperatorUserId == 27);
        payload.Select(item => item.SessionState)
            .Should()
            .BeEquivalentTo(new[] { "Scheduled", "Finished", "Cancelled" });
    }

    [Fact]
    public async Task ListAssignableSessions_WithParticipantRole_ReturnsForbidden()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<LiveSession> SeedSessionAsync(
        DateTimeOffset scheduledAt,
        SessionState state = SessionState.Scheduled,
        int? assignedOperatorUserId = null,
        string title = "Operator Assignment Session")
    {
        var liveSession = CreateSession(scheduledAt, title);

        if (assignedOperatorUserId is not null)
        {
            liveSession.AssignOperator(assignedOperatorUserId.Value, scheduledAt.AddMinutes(-5));
        }

        if (state != SessionState.Scheduled)
        {
            MoveToState(liveSession, state, scheduledAt.AddMinutes(1));
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return liveSession;
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt, string title)
    {
        var sourceMissionId = Guid.NewGuid();
        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            title,
            45,
            scheduledAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Seeded Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
            ],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    100,
                    4.711,
                    -74.0721,
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
    }

    private static void MoveToState(LiveSession session, SessionState targetState, DateTimeOffset occurredAt)
    {
        var policy = new SessionStateTransitionPolicy();

        if (targetState is SessionState.Preparing or SessionState.Active or SessionState.Paused or SessionState.Finished)
        {
            session.AssociateTeam(Guid.NewGuid(), "Explorers", "TEAM-01", 4);
        }

        switch (targetState)
        {
            case SessionState.Preparing:
                session.MoveTo(SessionState.Preparing, occurredAt, policy);
                break;
            case SessionState.Active:
                session.MoveTo(SessionState.Preparing, occurredAt, policy);
                session.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), policy);
                break;
            case SessionState.Paused:
                session.MoveTo(SessionState.Preparing, occurredAt, policy);
                session.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), policy);
                session.MoveTo(SessionState.Paused, occurredAt.AddMinutes(2), policy);
                break;
            case SessionState.Finished:
                // Finished is reached only via SessionCompletion (never a manual MoveTo); the single
                // treasure-hunt substage above completing with no next substage drives it there.
                session.MoveTo(SessionState.Preparing, occurredAt, policy);
                session.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), policy);
                session.CompleteActiveSubstageAndAdvance(occurredAt.AddMinutes(2), policy);
                break;
            case SessionState.Cancelled:
                session.MoveTo(SessionState.Cancelled, occurredAt, policy);
                break;
        }
    }

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record SessionOperatorSummaryResponse(
        Guid LiveSessionId,
        string SessionCode,
        string Title,
        string SessionState,
        int? AssignedOperatorUserId,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? LastTransitionedAt);
}
