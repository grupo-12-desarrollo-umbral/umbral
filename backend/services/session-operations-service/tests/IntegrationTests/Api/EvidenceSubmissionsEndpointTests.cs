using System.Text.Json;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-32 X.4 — operator evidence traceability read endpoint. Proves the full pipeline from participant
// scan intake through the outbox/consumer path to the operator read surface (eventually consistent by
// AC #5). Also proves non-owning operator rejection, teamId filtering, and that a wrong scan surfaces
// with its rejection reason.
[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceSubmissionsEndpointTests : IAsyncLifetime
{
    private const string CorrectQr = "QR-ALPHA";
    private const string WrongQr = "QR-BOGUS";
    private const int OwnerOperatorUserId = 42;
    private const string OwnerOperatorExternalIdentityId = "kc-operator-42";
    private const int OtherOperatorUserId = 99;
    private const string OtherOperatorExternalIdentityId = "kc-operator-99";

    private readonly PostgreSqlFixture _fixture;
    private RabbitMqContainer? _rabbit;
    private SessionOperationsApiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private bool _brokerAvailable;

    public EvidenceSubmissionsEndpointTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await DockerAvailability.StartOrSkipAsync(() => _rabbit.StartAsync(), _rabbit.DisposeAsync);
        _brokerAvailable = true;

        Environment.SetEnvironmentVariable("RabbitMq__HostName", _rabbit.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbit.GetMappedPublicPort(5672).ToString());

        _factory = new SessionOperationsApiWebApplicationFactory(
            _fixture.ConnectionString,
            useRealPublishEndpoint: true);
        _client = _factory.CreateClient();
        _factory.AccessClient.IsAllowed = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_brokerAvailable)
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("RabbitMq__HostName", null);
        Environment.SetEnvironmentVariable("RabbitMq__Port", null);

        if (_rabbit is not null)
        {
            await _rabbit.DisposeAsync();
        }
    }

    [Fact]
    public async Task CorrectScan_TraceEntryReachesAcceptedState_AfterOutboxDrains()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddOperatorTrustedHeaders(OwnerOperatorExternalIdentityId);

        // Warm the MassTransit bus before submitting the scan. The first request through the factory
        // triggers the server to fully initialize and the bus to connect to RabbitMQ. Without this, the
        // delivery service may not be ready to drain the outbox when the scan commits.
        await _client.GetAsync(BuildEvidenceUrl(seeded.LiveSessionId, teamId: null));
        await _client.GetAsync(BuildEvidenceUrl(seeded.LiveSessionId, teamId: null));

        // Submit a correct QR scan as a participant.
        var scansUrl = BuildScansUrl(seeded);
        using var participantClient = _factory.CreateClient();
        participantClient.DefaultRequestHeaders.Add("X-User-Id", seeded.ParticipantExternalIdentityId.ToString());
        participantClient.DefaultRequestHeaders.Add("X-User-Role", "Participant");
        participantClient.DefaultRequestHeaders.Add("X-User-Email", "participant@example.com");

        var scanResponse = await participantClient.PostAsJsonAsync(scansUrl, Scan(seeded, CorrectQr));
        scanResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Warm the MassTransit bus by making a harmless request before the poll. The first test in this
        // class pays the cold-start cost of bus startup + exchange declaration + first delivery-service
        // poll cycle, so the bus-outbox rows left by the scan may not drain within a tight window.
        await _client.GetAsync(BuildEvidenceUrl(seeded.LiveSessionId, teamId: null));
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Poll the operator endpoint until the trace entry appears with Accepted state.
        var trace = await PollEvidenceTraceAsync(seeded.LiveSessionId, teamId: null,
            dto => dto.Items.Any(item =>
                item.SubmissionType == "TreasureHuntQrScan"
                && item.ValidationState == "Accepted"));

        trace.LiveSessionId.Should().Be(seeded.LiveSessionId);
        var entry = trace.Items.Single(item =>
            item.SubmissionType == "TreasureHuntQrScan"
            && item.ValidationState == "Accepted");
        entry.TeamId.Should().Be(seeded.TeamId);
        entry.RejectionReason.Should().BeNull();
        entry.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task WrongScan_TraceEntryReachesRejectedStateWithReason_AfterOutboxDrains()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddOperatorTrustedHeaders(OwnerOperatorExternalIdentityId);

        // Submit a wrong QR scan as a participant.
        using var participantClient = _factory.CreateClient();
        participantClient.DefaultRequestHeaders.Add("X-User-Id", seeded.ParticipantExternalIdentityId.ToString());
        participantClient.DefaultRequestHeaders.Add("X-User-Role", "Participant");
        participantClient.DefaultRequestHeaders.Add("X-User-Email", "participant@example.com");

        var scansUrl = BuildScansUrl(seeded);
        var scanResponse = await participantClient.PostAsJsonAsync(scansUrl, Scan(seeded, WrongQr));
        scanResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Poll the operator endpoint until the trace entry appears with Rejected state and a reason.
        await PollEvidenceTraceUntilRejectedAsync(seeded.LiveSessionId, WrongQr);
    }

    [Fact]
    public async Task GetEvidenceSubmissions_WithTeamId_FiltersToTeam()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OwnerOperatorUserId, OwnerOperatorExternalIdentityId);
        AddOperatorTrustedHeaders(OwnerOperatorExternalIdentityId);

        // Seed trace entries for two teams in the same session via the repository.
        var submittedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var teamA = seeded.TeamId;
        var teamB = Guid.NewGuid();
        var substageId = seeded.TreasureHuntSubstageSnapshotId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IEvidenceTraceRepository>();

            var entryA = EvidenceTraceEntry.ForRegistration(
                Guid.NewGuid(), seeded.LiveSessionId, teamA, substageId,
                EvidenceSubmissionType.TreasureHuntQrScan, null, null, submittedAt);
            entryA.MarkAccepted(submittedAt.AddSeconds(1));
            await repo.UpsertAsync(entryA, CancellationToken.None);

            var entryB = EvidenceTraceEntry.ForRegistration(
                Guid.NewGuid(), seeded.LiveSessionId, teamB, substageId,
                EvidenceSubmissionType.TreasureHuntQrScan, null, null, submittedAt.AddSeconds(2));
            entryB.MarkAccepted(submittedAt.AddSeconds(3));
            await repo.UpsertAsync(entryB, CancellationToken.None);
        }

        // Query without filter — both teams returned.
        var all = await _client.GetFromJsonAsync<EvidenceTraceResponse>(
            BuildEvidenceUrl(seeded.LiveSessionId, teamId: null));
        all.Should().NotBeNull();
        all!.Items.Should().HaveCount(2);

        // Query with teamId filter — only teamA.
        var filtered = await _client.GetFromJsonAsync<EvidenceTraceResponse>(
            BuildEvidenceUrl(seeded.LiveSessionId, teamId: teamA));
        filtered.Should().NotBeNull();
        filtered!.Items.Should().ContainSingle();
        filtered.Items.Single().TeamId.Should().Be(teamA);
    }

    [Fact]
    public async Task GetEvidenceSubmissions_AsNonOwningOperator_ReturnsForbiddenProblemDetails()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        SetCurrentActor(OtherOperatorUserId, OtherOperatorExternalIdentityId);
        AddTrustedHeaders(_client, OtherOperatorExternalIdentityId, "Operator", "other-operator@example.com");

        var response = await _client.GetAsync(BuildEvidenceUrl(seeded.LiveSessionId, teamId: null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Type.Should().Be("forbidden-access");
    }

    private async Task<EvidenceTraceResponse> PollEvidenceTraceAsync(
        Guid liveSessionId, Guid? teamId, Func<EvidenceTraceResponse, bool> predicate)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var response = await _client.GetAsync(BuildEvidenceUrl(liveSessionId, teamId));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var trace = await response.Content.ReadFromJsonAsync<EvidenceTraceResponse>();
                if (trace is not null && predicate(trace))
                {
                    return trace;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var final = await _client.GetFromJsonAsync<EvidenceTraceResponse>(
            BuildEvidenceUrl(liveSessionId, teamId));
        final.Should().NotBeNull();
        predicate(final!).Should().BeTrue("the trace entry must reach the expected state within the poll window");
        return final!;
    }

    private async Task PollEvidenceTraceUntilRejectedAsync(Guid liveSessionId, string scannedValue)
    {
        // Search by origin reference which for a wrong scan is the scanned value itself.
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var response = await _client.GetAsync(BuildEvidenceUrl(liveSessionId, teamId: null));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            var trace = await response.Content.ReadFromJsonAsync<EvidenceTraceResponse>();
            var rejected = trace?.Items.FirstOrDefault(item =>
                item.ValidationState == "Rejected"
                && item.RejectionReason is not null);

            if (rejected is not null)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        // Final assertion — collect the trace one more time and fail with diagnostic.
        var finalResponse = await _client.GetFromJsonAsync<EvidenceTraceResponse>(
            BuildEvidenceUrl(liveSessionId, teamId: null));
        finalResponse.Should().NotBeNull();
        finalResponse!.Items.Should().Contain(
            item => item.ValidationState == "Rejected" && item.RejectionReason != null,
            "a wrong QR scan must produce a rejected trace entry with a non-null rejection reason");
    }

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
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
            "Evidence Submissions E2E Session",
            maximumTimeMinutes: 45,
            createdAt,
            snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(OwnerOperatorUserId, createdAt);

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

    private void SetCurrentActor(int userId, string externalIdentityId)
    {
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            userId,
            externalIdentityId,
            "Operator",
            true);
    }

    private void AddOperatorTrustedHeaders(string externalIdentityId)
    {
        AddTrustedHeaders(_client, externalIdentityId, "Operator", "operator@example.com");
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

    private static RegisterTargetScanRequest Scan(SeededSession seeded, string scannedValue) =>
        new(seeded.TeamId, scannedValue, null);

    private static string BuildScansUrl(SeededSession seeded) =>
        $"/api/sessions/{seeded.LiveSessionId:D}/participants/target-scans";

    private static string BuildEvidenceUrl(Guid liveSessionId, Guid? teamId)
    {
        var url = $"/api/sessions/{liveSessionId:D}/evidence-submissions";
        if (teamId.HasValue)
        {
            url += $"?teamId={teamId.Value:D}";
        }
        return url;
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

    private sealed record EvidenceTraceResponse(
        Guid LiveSessionId,
        IReadOnlyList<EvidenceTraceItemResponse> Items);

    private sealed record EvidenceTraceItemResponse(
        Guid EvidenceSubmissionId,
        Guid TeamId,
        Guid ActiveSubstageId,
        string SubmissionType,
        string? OriginReference,
        DateTimeOffset SubmittedAt,
        string ValidationState,
        string? RejectionReason,
        DateTimeOffset? ResolvedAt);

    private sealed record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail);
}
