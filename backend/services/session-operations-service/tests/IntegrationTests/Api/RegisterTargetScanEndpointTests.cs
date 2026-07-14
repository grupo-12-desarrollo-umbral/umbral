using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-31 X.4 participant QR/target-scan write. Intake is unconditional: a correct scan resolves the target
// and returns 200 acceptance metadata WITHOUT leaking the score; a wrong/duplicate/out-of-context scan is
// retained as Rejected and returned as RFC 7807 (422) with a consistent reason; a scan on a non-admitting
// session (Paused/Finished/Cancelled) is blocked pre-intake and surfaces as ProblemDetails; a denied
// participation fact is Forbidden. Mirrors SubmitTriviaAnswerEndpointTests.
[Collection(PostgreSqlCollection.Name)]
public sealed class RegisterTargetScanEndpointTests : IAsyncLifetime
{
    private const string CorrectQr = "QR-ALPHA";
    private const string SecondQr = "QR-BETA";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public RegisterTargetScanEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _factory.AccessClient.IsAllowed = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterTargetScan_WithCorrectQr_ReturnsAcceptanceMetadataWithoutScore()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<RegisterTargetScanResponse>();
        payload.Should().NotBeNull();
        payload!.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.ActiveSubstageId.Should().Be(seeded.TreasureHuntSubstageSnapshotId);
        payload.TargetSnapshotId.Should().NotBeNull();
        payload.IsResolved.Should().BeTrue();
        payload.RejectionReason.Should().BeNull();
        payload.SubmittedAt.Should().NotBe(default);

        // Privacy gate: the accepted-scan response must never expose the score — it travels only on the
        // RabbitMQ TargetResolved fact for downstream scoring.
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var propertyNames = document.RootElement.EnumerateObject()
            .Select(property => property.Name.ToLowerInvariant())
            .ToList();
        propertyNames.Should().NotContain(name => name.Contains("score") || name.Contains("points"));
    }

    [Fact]
    public async Task RegisterTargetScan_WithUnknownQr_ReturnsUnprocessableEntityProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, "QR-BOGUS"));

        await AssertRetainedRejectionProblemDetails(
            response,
            "The scanned value does not resolve to a target.");
    }

    [Fact]
    public async Task RegisterTargetScan_WithDuplicateScan_ReturnsUnprocessableEntityProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var first = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second correct scan of the same target by the same team is retained-rejected as a duplicate.
        var repeat = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        await AssertRetainedRejectionProblemDetails(
            repeat,
            "The target has already been resolved by this team.");
    }

    [Fact]
    public async Task RegisterTargetScan_ForTargetOutsideActiveSubstage_ReturnsUnprocessableEntityProblemDetails()
    {
        // Seed a second treasure-hunt substage whose target is NOT the active substage's target.
        var seeded = await SeedActiveTreasureHuntSessionAsync(withInactiveSubstageTarget: true);
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, SecondQr));

        await AssertRetainedRejectionProblemDetails(
            response,
            "The resolved target does not belong to the active treasure-hunt substage.");
    }

    [Fact]
    public async Task RegisterTargetScan_WhenParticipationFactDenied_ReturnsForbidden()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        _factory.AccessClient.IsAllowed = false;
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task RegisterTargetScan_WhenCallerIsNotAnAdmittedParticipant_ReturnsForbidden()
    {
        // Participation is allowed, but the caller identity matches no admitted SessionParticipant, so
        // the scan cannot be attributed — AnswerSubmitterIsNotSessionParticipant maps to 403.
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "stranger@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden.");
    }

    [Fact]
    public async Task RegisterTargetScan_WhenCallerIdentityIsNotAGuid_ReturnsForbidden()
    {
        // A non-Guid caller id can never resolve to a participant, so the scan is unattributable (403).
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, "not-a-guid", "Participant", "stranger@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RegisterTargetScan_WithoutTrustedHeaders_ReturnsUnauthorized()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterTargetScan_WithOperatorRole_ReturnsForbidden()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, "kc-operator-1", "Operator", "operator@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegisterTargetScan_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        AddTrustedHeaders(_client, Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid():D}/participants/target-scans",
            new RegisterTargetScanRequest(Guid.NewGuid(), CorrectQr, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterTargetScan_WithBlankScannedValue_ReturnsBadRequest()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(
            BuildScansUrl(seeded),
            new RegisterTargetScanRequest(seeded.TeamId, "   ", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task RegisterTargetScan_WhenSessionIsPreparing_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedSessionInNonAdmittingStateAsync(SessionState.Preparing);
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        await AssertNonAdmittingSessionProblemDetails(response);
    }

    [Fact]
    public async Task RegisterTargetScan_WhenSessionIsPaused_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedSessionInNonAdmittingStateAsync(SessionState.Paused);
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        await AssertNonAdmittingSessionProblemDetails(response);
    }

    [Fact]
    public async Task RegisterTargetScan_WhenSessionIsCancelled_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedSessionInNonAdmittingStateAsync(SessionState.Cancelled);
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        await AssertNonAdmittingSessionProblemDetails(response);
    }

    [Fact]
    public async Task RegisterTargetScan_WhenSessionIsFinished_ReturnsConflictProblemDetails()
    {
        var seeded = await SeedSessionInNonAdmittingStateAsync(SessionState.Finished);
        AddTrustedHeaders(_client, seeded.ParticipantExternalIdentityId.ToString(), "Participant", "participant@example.com");

        var response = await _client.PostAsJsonAsync(BuildScansUrl(seeded), Scan(seeded, CorrectQr));

        await AssertNonAdmittingSessionProblemDetails(response);
    }

    // Retained-reject (AC#9): the scan was registered for audit but the target was not resolved; the API
    // reports it as RFC 7807 (422 Unprocessable Entity) with the consistent reason in Detail and never a score.
    private static async Task AssertRetainedRejectionProblemDetails(HttpResponseMessage response, string expectedReason)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problem.Type.Should().Be("target-scan-rejected");
        problem.Title.Should().Be("Unprocessable entity.");
        problem.Detail.Should().Be(expectedReason);

        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().NotContain("score");
    }

    private static async Task AssertNonAdmittingSessionProblemDetails(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json",
            "RFC 7807 mandates application/problem+json for ProblemDetails responses");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict.");
    }

    private static RegisterTargetScanRequest Scan(SeededSession seeded, string scannedValue) =>
        new(seeded.TeamId, scannedValue, null);

    private static string BuildScansUrl(SeededSession seeded) =>
        $"/api/sessions/{seeded.LiveSessionId:D}/participants/target-scans";

    // Seeds a treasure-hunt session in Active. Entering Active starts the first (treasure-hunt) substage,
    // so a scan against its target resolves. withInactiveSubstageTarget adds a SECOND substage with its own
    // target (QR-BETA) that is NOT the active substage's target — scanning it is out-of-context.
    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync(bool withInactiveSubstageTarget = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();

        var activeSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var stages = new List<StageSnapshot>();
        var targets = new List<TargetSnapshot>
        {
            TargetSnapshot.Create(activeSubstage.SubstageSnapshotId, "Target Alpha", CorrectQr, 1, true, 100,
                4.711, -74.0721, "Look under the stairs", "AfterPreviousTarget")
        };

        if (withInactiveSubstageTarget)
        {
            var secondSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt Two", 2);
            stages.Add(StageSnapshot.Create("Stage One", 1, [activeSubstage, secondSubstage]));
            targets.Add(TargetSnapshot.Create(secondSubstage.SubstageSnapshotId, "Target Beta", SecondQr, 1, true, 150,
                4.712, -74.0722, "Behind the fountain", "AfterPreviousTarget"));
        }
        else
        {
            stages.Add(StageSnapshot.Create("Stage One", 1, [activeSubstage]));
        }

        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId, "Treasure Mission", MaximumTime.Create(45), stages, targets, []);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"THQ-{Guid.NewGuid():N}"[..12],
            "Target Scan Session",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);

        var participantExternalIdentityId = Guid.NewGuid();
        session.AdmitParticipant(
            participantExternalIdentityId,
            "Red One",
            team.TeamId,
            createdAt.AddMinutes(1).AddSeconds(30),
            new JoinPolicy());

        session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(
            session.LiveSessionId, team.TeamId, activeSubstage.SubstageSnapshotId, participantExternalIdentityId);
    }

    // Seeds a treasure-hunt session in a non-Active state for the session-not-admitting-reception tests.
    private async Task<SeededSession> SeedSessionInNonAdmittingStateAsync(SessionState targetState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var activeSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Treasure Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [activeSubstage])
            ],
            [
                TargetSnapshot.Create(activeSubstage.SubstageSnapshotId, "Target Alpha", CorrectQr, 1, true, 100,
                    4.711, -74.0721, "Look under the stairs", "AfterPreviousTarget")
            ],
            []);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"THQ-{Guid.NewGuid():N}"[..12],
            "Non-Admitting Treasure Session",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), transitionPolicy);

        var participantExternalIdentityId = Guid.NewGuid();
        session.AdmitParticipant(
            participantExternalIdentityId,
            "Red One",
            team.TeamId,
            createdAt.AddMinutes(1).AddSeconds(30),
            new JoinPolicy());

        switch (targetState)
        {
            case SessionState.Preparing:
                break;
            case SessionState.Paused:
                session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);
                session.MoveTo(SessionState.Paused, now.AddMinutes(1), transitionPolicy);
                break;
            case SessionState.Cancelled:
                session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);
                session.MoveTo(SessionState.Cancelled, createdAt.AddMinutes(3), transitionPolicy);
                break;
            case SessionState.Finished:
                session.MoveTo(SessionState.Active, createdAt.AddMinutes(2), transitionPolicy);
                session.MoveTo(SessionState.Finished, createdAt.AddMinutes(3), transitionPolicy);
                break;
        }

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(
            session.LiveSessionId, team.TeamId, activeSubstage.SubstageSnapshotId, participantExternalIdentityId);
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
        Guid TeamId,
        Guid TreasureHuntSubstageSnapshotId,
        Guid ParticipantExternalIdentityId);

    private sealed record RegisterTargetScanRequest(
        Guid TeamId,
        string ScannedValue,
        string? Token);

    private sealed record RegisterTargetScanResponse(
        Guid LiveSessionId,
        Guid TeamId,
        Guid ActiveSubstageId,
        Guid? TargetSnapshotId,
        bool IsResolved,
        string? RejectionReason,
        DateTimeOffset SubmittedAt);
}
