using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AddOperativeClueEndpointTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";
    private const string ClueText = "Look beneath the blue banner.";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public AddOperativeClueEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.AccessClient.IsAllowed = true;
        SetCurrentOperator(OperatorUserId, OperatorExternalIdentityId);
        _client = _factory.CreateClient();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AddOperativeClue_ToOneTeam_ReturnsOkAndOnlyAssignedTeamCanSeeItWithoutAdvancing()
    {
        var seeded = await SeedSessionAsync(SessionState.Active);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await PostClueAsync(seeded.LiveSessionId, ClueText, [seeded.PrimaryTeamId]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AddOperativeClueResultDto>();
        payload.Should().NotBeNull();
        payload!.ClueText.Should().Be(ClueText);
        payload.OperativeClueIds.Should().ContainSingle();
        payload.AssignedTeamIds.Should().ContainSingle().Which.Should().Be(seeded.PrimaryTeamId);

        // Each board is read by the participant who is an active member of that team: membership is now
        // enforced per session-team, so one participant cannot read another team's board.
        AddTrustedHeaders(seeded.PrimaryParticipantId.ToString(), "Participant", "participant@example.com");
        var assignedBoard = await GetBoardAsync(seeded.LiveSessionId, seeded.PrimaryReferenceTeamId);
        AddTrustedHeaders(seeded.SecondaryParticipantId.ToString(), "Participant", "participant@example.com");
        var unassignedBoard = await GetBoardAsync(seeded.LiveSessionId, seeded.SecondaryReferenceTeamId);

        var assignedClue = assignedBoard.VisibleClues
            .Should().ContainSingle(clue => clue.ClueText == ClueText)
            .Which;
        assignedClue.OperativeClueId.Should().Be(payload.OperativeClueIds.Single());
        assignedClue.TargetSnapshotId.Should().BeNull();
        assignedClue.TargetName.Should().BeNull();
        unassignedBoard.VisibleClues.Should().NotContain(clue => clue.ClueText == ClueText);
        assignedBoard.ActiveSubstage!.SubstageSnapshotId.Should().Be(seeded.ActiveSubstageSnapshotId);
        assignedBoard.ActiveSubstage.ResolvedTargets.Should().Be(0);
    }

    [Fact]
    public async Task AddOperativeClue_ToSeveralTeamsWhilePaused_ReturnsOneRecordPerTeam()
    {
        var seeded = await SeedSessionAsync(SessionState.Paused);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await PostClueAsync(
            seeded.LiveSessionId,
            ClueText,
            [seeded.PrimaryTeamId, seeded.SecondaryTeamId]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AddOperativeClueResultDto>();
        payload.Should().NotBeNull();
        payload!.OperativeClueIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        payload.AssignedTeamIds.Should().BeEquivalentTo([seeded.PrimaryTeamId, seeded.SecondaryTeamId]);
    }

    [Fact]
    public async Task AddOperativeClue_ByNonOwningOperator_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedSessionAsync(SessionState.Active);
        SetCurrentOperator(77, "kc-operator-77");
        AddTrustedHeaders("kc-operator-77", "Operator", "other@example.com");

        var response = await PostClueAsync(seeded.LiveSessionId, ClueText, [seeded.PrimaryTeamId]);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemDetailsAsync(response, StatusCodes.Status403Forbidden, "Forbidden.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddOperativeClue_WithEmptyTextOrTeams_ReturnsValidationProblemDetails(bool emptyText)
    {
        var seeded = await SeedSessionAsync(SessionState.Active);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await PostClueAsync(
            seeded.LiveSessionId,
            emptyText ? " " : ClueText,
            emptyText ? [seeded.PrimaryTeamId] : []);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest, "Validation failed.");
    }

    [Fact]
    public async Task AddOperativeClue_WhenSessionIsScheduled_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedSessionAsync(SessionState.Scheduled);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await PostClueAsync(seeded.LiveSessionId, ClueText, [seeded.PrimaryTeamId]);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemDetailsAsync(response, StatusCodes.Status409Conflict, "Conflict.");
    }

    [Fact]
    public async Task AddOperativeClue_WithUnknownTeam_ReturnsNotFoundProblemDetails()
    {
        var seeded = await SeedSessionAsync(SessionState.Active);
        AddTrustedHeaders(OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await PostClueAsync(seeded.LiveSessionId, ClueText, [Guid.NewGuid()]);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemDetailsAsync(response, StatusCodes.Status404NotFound, "Resource not found.");
    }

    private async Task<ParticipantTeamBoardDto> GetBoardAsync(Guid liveSessionId, Guid teamId)
    {
        var response = await _client.GetAsync(
            $"/api/sessions/{liveSessionId:D}/participants/team-board?teamId={teamId:D}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ParticipantTeamBoardDto>())!;
    }

    private Task<HttpResponseMessage> PostClueAsync(
        Guid liveSessionId,
        string clueText,
        IReadOnlyList<Guid> teamIds) =>
        _client.PostAsJsonAsync(
            $"/api/sessions/{liveSessionId:D}/operative-clues",
            new { clueText, teamIds });

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        int expectedStatus,
        string expectedTitle)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(expectedStatus);
        problem.Title.Should().Be(expectedTitle);
    }

    private async Task<SeededSession> SeedSessionAsync(SessionState state)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var snapshot = CreateTreasureHuntSnapshot(sourceMissionId);
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"OPC-{Guid.NewGuid():N}"[..12],
            "Operative Clue Test",
            45,
            createdAt,
            snapshot);
        var primaryReferenceTeamId = Guid.NewGuid();
        var secondaryReferenceTeamId = Guid.NewGuid();
        var primaryTeam = session.AssociateTeam(primaryReferenceTeamId, "Alpha", "ALP-01", 4);
        var secondaryTeam = session.AssociateTeam(secondaryReferenceTeamId, "Bravo", "BRV-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));

        var primaryParticipantId = Guid.NewGuid();
        var secondaryParticipantId = Guid.NewGuid();

        if (state is SessionState.Active or SessionState.Paused)
        {
            var transitionPolicy = new SessionStateTransitionPolicy();
            session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
            session.AdmitParticipant(
                primaryParticipantId, "Alpha-1", primaryTeam.TeamId, createdAt.AddMinutes(2).AddSeconds(10), new JoinPolicy());
            session.AdmitParticipant(
                secondaryParticipantId, "Bravo-1", secondaryTeam.TeamId, createdAt.AddMinutes(2).AddSeconds(20), new JoinPolicy());
            session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);
            if (state == SessionState.Paused)
            {
                session.MoveTo(SessionState.Paused, createdAt.AddMinutes(4), transitionPolicy);
            }
        }

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(
            session.LiveSessionId,
            primaryTeam.TeamId,
            secondaryTeam.TeamId,
            primaryReferenceTeamId,
            secondaryReferenceTeamId,
            primaryParticipantId,
            secondaryParticipantId,
            snapshot.StageSnapshots.Single().SubstageSnapshots.Single().SubstageSnapshotId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operative Clue Mission",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [
                TargetSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Target Alpha",
                    "TARGET-ALPHA",
                    1,
                    isActive: true,
                    100,
                    4.711,
                    -74.0721,
                    null,
                    "VisibleAtStart")
            ],
            []);
    }

    private void SetCurrentOperator(int userId, string externalIdentityId)
    {
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            userId,
            externalIdentityId,
            "Operator",
            true);
    }

    private void AddTrustedHeaders(string userId, string role, string email)
    {
        _client.DefaultRequestHeaders.Remove("X-User-Id");
        _client.DefaultRequestHeaders.Remove("X-User-Role");
        _client.DefaultRequestHeaders.Remove("X-User-Email");
        _client.DefaultRequestHeaders.Add("X-User-Id", userId);
        _client.DefaultRequestHeaders.Add("X-User-Role", role);
        _client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record SeededSession(
        Guid LiveSessionId,
        Guid PrimaryTeamId,
        Guid SecondaryTeamId,
        Guid PrimaryReferenceTeamId,
        Guid SecondaryReferenceTeamId,
        Guid PrimaryParticipantId,
        Guid SecondaryParticipantId,
        Guid ActiveSubstageSnapshotId);
}
