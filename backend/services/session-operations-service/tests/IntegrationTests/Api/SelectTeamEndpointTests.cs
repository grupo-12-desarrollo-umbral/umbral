using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// Self-join (#110) against a real Postgres. These belong at the integration layer rather than the
// domain suite: the invariant at stake is the unique index on live_session_team_members, which the
// in-memory domain tests cannot violate. A membership row is per-stint history — ReleaseParticipant
// marks the row Removed and AssignParticipant adds a fresh one — so switching away and back leaves
// several rows for one (team, participant) pair, and only an Active-filtered index tolerates that.
[Collection(PostgreSqlCollection.Name)]
public sealed class SelectTeamEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public SelectTeamEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.EligibleTeamsClient.IsEligible = true;
        _factory.EligibleTeamsClient.Teams = [];
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SelectTeam_RejoiningTeamThePartipantPreviouslyLeft_Succeeds()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedScheduledSessionWithTwoTeamsAsync();
        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        (await JoinAsync(seeded.SessionCode, seeded.TeamId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await JoinAsync(seeded.SessionCode, seeded.OtherTeamId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The regression: the first team already holds a Removed row for this participant, so assigning
        // them again inserts a second row for the same pair. An unfiltered unique index rejected it (23505)
        // and the endpoint surfaced 500.
        var rejoin = await JoinAsync(seeded.SessionCode, seeded.TeamId);

        rejoin.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await rejoin.Content.ReadFromJsonAsync<SelectTeamResultDto>();
        payload.Should().NotBeNull();
        payload!.TeamId.Should().Be(seeded.TeamId);

        var memberships = await LoadMembershipsAsync(seeded.LiveSessionId);

        // Every stint is retained, and the participant is active on exactly the team they last picked.
        memberships.Should().HaveCount(3);
        memberships.Count(member => member.IsActive).Should().Be(1);
        memberships.Single(member => member.IsActive).TeamId.Should().Be(seeded.TeamId);
        memberships.Where(member => !member.IsActive).Should().OnlyContain(member => member.LeftAt != null);
    }

    [Fact]
    public async Task SelectTeam_RepeatedSwitchingBetweenTwoTeams_KeepsExactlyOneActiveMembership()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedScheduledSessionWithTwoTeamsAsync();
        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        foreach (var teamId in new[] { seeded.TeamId, seeded.OtherTeamId, seeded.TeamId, seeded.OtherTeamId })
        {
            (await JoinAsync(seeded.SessionCode, teamId)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var memberships = await LoadMembershipsAsync(seeded.LiveSessionId);

        memberships.Should().HaveCount(4);
        memberships.Count(member => member.IsActive).Should().Be(1);
        memberships.Single(member => member.IsActive).TeamId.Should().Be(seeded.OtherTeamId);
    }

    [Fact]
    public async Task SelectTeam_RepickingTheCurrentTeam_IsANoOpAndAddsNoMembershipRow()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedScheduledSessionWithTwoTeamsAsync();
        AddTrustedHeaders(_client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        (await JoinAsync(seeded.SessionCode, seeded.TeamId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await JoinAsync(seeded.SessionCode, seeded.TeamId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var memberships = await LoadMembershipsAsync(seeded.LiveSessionId);

        memberships.Should().ContainSingle();
        memberships.Single().IsActive.Should().BeTrue();
    }

    private Task<HttpResponseMessage> JoinAsync(string sessionCode, Guid runtimeTeamId)
    {
        return _client.PostAsync(
            $"/api/sessions/by-code/{sessionCode}/teams/{runtimeTeamId}/join",
            content: null);
    }

    private async Task<IReadOnlyList<TeamMember>> LoadMembershipsAsync(Guid liveSessionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await dbContext.LiveSessions
            .Include(session => session.Teams)
            .ThenInclude(team => team.Members)
            .SingleAsync(session => session.LiveSessionId == liveSessionId);

        return session.Teams.SelectMany(team => team.Members).ToList();
    }

    private async Task<SeededSession> SeedScheduledSessionWithTwoTeamsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var sessionCode = $"SEL-{Guid.NewGuid():N}"[..12];
        var sourceMissionId = Guid.NewGuid();
        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            sessionCode,
            "Select Team Session",
            45,
            scheduledAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var team = liveSession.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        var otherTeam = liveSession.AssociateTeam(Guid.NewGuid(), "Blue", "BLUE-01", 4);

        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        return new SeededSession(liveSession.LiveSessionId, sessionCode, team.TeamId, otherTeam.TeamId);
    }

    // Play mode is irrelevant to team selection; a treasure hunt keeps the fixture minimal because a
    // trivia substage snapshot would additionally require questions.
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

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record SeededSession(
        Guid LiveSessionId,
        string SessionCode,
        Guid TeamId,
        Guid OtherTeamId);
}
