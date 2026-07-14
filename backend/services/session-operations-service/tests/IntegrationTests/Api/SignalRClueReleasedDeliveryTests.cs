using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class SignalRClueReleasedDeliveryTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public SignalRClueReleasedDeliveryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.AccessClient.IsAllowed = true;
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseClue_PushesTeamBoardUpdatedOnlyToReleasedTeam()
    {
        var participantExternalId = Guid.NewGuid();
        var otherParticipantExternalId = Guid.NewGuid();
        var seeded = await SeedActiveTreasureHuntSessionWithDisconnectedParticipantsAsync(
            participantExternalId,
            otherParticipantExternalId);

        await using var releasedTeamConnection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            participantExternalId,
            seeded.PrimaryTeamId);
        await using var otherTeamConnection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            otherParticipantExternalId,
            seeded.SecondaryTeamId);

        ParticipantTeamBoardDto? releasedReceived = null;
        ParticipantTeamBoardDto? otherReceived = null;

        releasedTeamConnection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => releasedReceived = payload);
        otherTeamConnection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => otherReceived = payload);

        var operatorClient = _factory.CreateClient();
        AddTrustedHeaders(operatorClient, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await operatorClient.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId, teamId = seeded.PrimaryTeamId });

        response.EnsureSuccessStatusCode();

        await Task.Delay(500);

        releasedReceived.Should().NotBeNull("team-board update should reach the released team group");
        releasedReceived!.TeamId.Should().Be(seeded.PrimaryTeamId);
        releasedReceived.LiveSessionId.Should().Be(seeded.LiveSessionId);

        otherReceived.Should().BeNull("team-board update must not reach non-released team group");
    }

    [Fact]
    public async Task ReleaseClueToAllTeams_PushesTeamBoardUpdatedToEachReleasedTeam()
    {
        var participantExternalId = Guid.NewGuid();
        var otherParticipantExternalId = Guid.NewGuid();
        var seeded = await SeedActiveTreasureHuntSessionWithDisconnectedParticipantsAsync(
            participantExternalId,
            otherParticipantExternalId);

        await using var primaryConnection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            participantExternalId,
            seeded.PrimaryTeamId);
        await using var secondaryConnection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            otherParticipantExternalId,
            seeded.SecondaryTeamId);

        ParticipantTeamBoardDto? primaryReceived = null;
        ParticipantTeamBoardDto? secondaryReceived = null;

        primaryConnection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => primaryReceived = payload);
        secondaryConnection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => secondaryReceived = payload);

        var operatorClient = _factory.CreateClient();
        AddTrustedHeaders(operatorClient, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await operatorClient.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { targetId = seeded.TargetSnapshotId });

        response.EnsureSuccessStatusCode();

        await Task.Delay(500);

        primaryReceived.Should().NotBeNull("team-board update should reach primary team group");
        primaryReceived!.TeamId.Should().Be(seeded.PrimaryTeamId);

        secondaryReceived.Should().NotBeNull("team-board update should reach secondary team group");
        secondaryReceived!.TeamId.Should().Be(seeded.SecondaryTeamId);
    }

    [Fact]
    public async Task ReleaseTriviaClue_PushesBoardContainingClueSnapshotIdToReleasedTeam()
    {
        var participantExternalId = Guid.NewGuid();
        var seeded = await SeedActiveTriviaSessionWithDisconnectedParticipantAsync(participantExternalId);

        await using var connection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            participantExternalId,
            seeded.TeamId);
        ParticipantTeamBoardDto? received = null;
        connection.On<ParticipantTeamBoardDto>(
            SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
            payload => received = payload);

        var operatorClient = _factory.CreateClient();
        AddTrustedHeaders(operatorClient, OperatorExternalIdentityId, "Operator", "operator@example.com");
        var response = await operatorClient.PostAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId}/clues/release",
            new { clueId = seeded.ClueSnapshotId, teamId = seeded.TeamId });

        response.EnsureSuccessStatusCode();
        await Task.Delay(500);

        received.Should().NotBeNull();
        received!.VisibleClues.Should().ContainSingle(clue =>
            clue.ClueSnapshotId == seeded.ClueSnapshotId &&
            clue.TargetSnapshotId == null &&
            clue.OperativeClueId == null);
    }

    private async Task<HubConnection> ConnectParticipantAsync(
        Guid liveSessionId,
        Guid externalIdentityId,
        Guid teamId)
    {
        var client = _factory.CreateClient();
        AddTrustedHeaders(client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var hubUrl = new Uri(_factory.Server.BaseAddress, "/hubs/sessions");
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers["X-User-Id"] = client.DefaultRequestHeaders.GetValues("X-User-Id").First();
                options.Headers["X-User-Role"] = client.DefaultRequestHeaders.GetValues("X-User-Role").First();
                options.Headers["X-User-Email"] = client.DefaultRequestHeaders.GetValues("X-User-Email").First();
            })
            .Build();

        await connection.StartAsync();

        await connection.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            liveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(teamId, "TestUser", null));

        return connection;
    }

    private async Task<SeededSession> SeedActiveTreasureHuntSessionWithDisconnectedParticipantsAsync(
        Guid primaryParticipantExternalIdentityId,
        Guid secondaryParticipantExternalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"CSR-{Guid.NewGuid():N}"[..12],
            "Clue SignalR Push",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var primaryTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        var secondaryTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "BRV-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));

        var primaryParticipant = session.AdmitParticipant(
            primaryParticipantExternalIdentityId,
            "AlphaUser",
            primaryTeam.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        var secondaryParticipant = session.AdmitParticipant(
            secondaryParticipantExternalIdentityId,
            "BravoUser",
            secondaryTeam.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(primaryParticipant.SessionParticipantId, createdAt.AddMinutes(2));
        session.DisconnectParticipant(secondaryParticipant.SessionParticipantId, createdAt.AddMinutes(2));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);

        var targetSnapshotId = session.MissionRuntimeSnapshot.TargetSnapshots.First().TargetSnapshotId;

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, targetSnapshotId, primaryTeam.TeamId, secondaryTeam.TeamId);
    }

    private async Task<SeededTriviaSession> SeedActiveTriviaSessionWithDisconnectedParticipantAsync(
        Guid participantExternalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var snapshot = CreateTriviaSnapshot(sourceMissionId);
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRS-{Guid.NewGuid():N}"[..12],
            "Trivia Clue SignalR Push",
            20,
            createdAt,
            snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));
        var participant = session.AdmitParticipant(
            participantExternalIdentityId,
            "AlphaUser",
            team.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(2));
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededTriviaSession(
            session.LiveSessionId,
            snapshot.ClueSnapshots.Single().ClueSnapshotId,
            team.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Clue SignalR Push",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [
                TargetSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Hidden Clue Target",
                    "QR-HIDDEN",
                    1,
                    isActive: true,
                    100,
                    4.711,
                    -74.0721,
                    "The treasure lies beneath.",
                    "HiddenUntilOperatorRelease")
            ],
            []);
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var question = TriviaQuestionSnapshot.Create(
            substage.SubstageSnapshotId,
            "Which planet is closest to the Sun?",
            1,
            100,
            30,
            "Mercury is closest.",
            [
                TriviaOptionSnapshot.Create("Mercury", 1, true),
                TriviaOptionSnapshot.Create("Venus", 2, false)
            ]);
        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Trivia Clue SignalR Push",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [],
            [question],
            [ClueSnapshot.Create(
                substage.SubstageSnapshotId,
                "Operator-only trivia clue.",
                ClueSnapshot.HiddenUntilOperatorReleasePolicy,
                1)]);
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
        Guid TargetSnapshotId,
        Guid PrimaryTeamId,
        Guid SecondaryTeamId);

    private sealed record SeededTriviaSession(Guid LiveSessionId, Guid ClueSnapshotId, Guid TeamId);
}
