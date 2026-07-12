using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-23: proves the team-board SignalR update reaches the correct team group and does NOT reach
// a different team group. Uses real hub connections through the WebApplicationFactory.
[Collection(PostgreSqlCollection.Name)]
public sealed class SignalRTeamBoardDeliveryTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public SignalRTeamBoardDeliveryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.AccessClient.IsAllowed = true;
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastTeamBoardUpdated_ReachesOwnTeamGroup()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        var connection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            seeded.PrimaryParticipantExternalIdentityId,
            seeded.PrimaryTeamId);

        try
        {
            ParticipantTeamBoardDto? received = null;
            connection.On<ParticipantTeamBoardDto>(
                SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
                payload => received = payload);

            var broadcaster = _factory.Services.GetRequiredService<Application.Common.Interfaces.ITeamBoardBroadcaster>();
            var board = CreateBoardDto(seeded.LiveSessionId, seeded.PrimaryTeamId);

            await broadcaster.BroadcastTeamBoardUpdatedAsync(board, CancellationToken.None);

            await Task.Delay(500);
            received.Should().NotBeNull();
            received!.TeamId.Should().Be(seeded.PrimaryTeamId);
            received.LiveSessionId.Should().Be(seeded.LiveSessionId);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task BroadcastTeamBoardUpdated_DoesNotReachOtherTeamGroup()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();
        var otherConnection = await ConnectParticipantAsync(
            seeded.LiveSessionId,
            seeded.SecondaryParticipantExternalIdentityId,
            seeded.SecondaryTeamId);

        try
        {
            ParticipantTeamBoardDto? received = null;
            otherConnection.On<ParticipantTeamBoardDto>(
                SignalRTeamBoardBroadcaster.TeamBoardUpdatedMethod,
                payload => received = payload);

            var broadcaster = _factory.Services.GetRequiredService<Application.Common.Interfaces.ITeamBoardBroadcaster>();
            var board = CreateBoardDto(seeded.LiveSessionId, seeded.PrimaryTeamId);

            await broadcaster.BroadcastTeamBoardUpdatedAsync(board, CancellationToken.None);

            await Task.Delay(500);
            received.Should().BeNull();
        }
        finally
        {
            await otherConnection.DisposeAsync();
        }
    }

    private async Task<HubConnection> ConnectParticipantAsync(
        Guid liveSessionId,
        Guid externalIdentityId,
        Guid teamId)
    {
        var client = _factory.CreateClient();
        AddTrustedHeaders(client, externalIdentityId.ToString(), "Participant", "participant@example.com");

        var hubUrl = _factory.Server.BaseAddress + "hubs/sessions";
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

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var createdAt = now.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var participantExternalIdentityId = Guid.NewGuid();
        var otherParticipantExternalIdentityId = Guid.NewGuid();

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SR-{Guid.NewGuid():N}"[..12],
            "SignalR Team Board",
            maximumTimeMinutes: 45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
        var primaryTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var secondaryTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var primaryParticipantExternalIdentityId = Guid.NewGuid();
        var secondaryParticipantExternalIdentityId = Guid.NewGuid();
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

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, now, policy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(
            session.LiveSessionId,
            primaryTeam.TeamId,
            secondaryTeam.TeamId,
            primaryParticipantExternalIdentityId,
            secondaryParticipantExternalIdentityId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);
        var target = TargetSnapshot.Create(
            substage.SubstageSnapshotId,
            "Find the key",
            "KEY-001",
            1,
            isActive: true,
            100,
            "Look near the entrance.",
            "VisibleAtStart");

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "SignalR Board Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [target],
            []);
    }

    private static ParticipantTeamBoardDto CreateBoardDto(Guid liveSessionId, Guid teamId)
    {
        var timer = new SessionTimerSnapshotDto(
            liveSessionId,
            teamId,
            nameof(SessionState.Active),
            2700,
            2600,
            "Advancing",
            true,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null);

        return new ParticipantTeamBoardDto(
            liveSessionId,
            teamId,
            "Alpha",
            "A-01",
            0,
            timer,
            new ActiveSubstageContextDto(Guid.NewGuid(), "TreasureHunt", "Treasure Hunt", 1, 0, null, null),
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
        Guid PrimaryTeamId,
        Guid SecondaryTeamId,
        Guid PrimaryParticipantExternalIdentityId,
        Guid SecondaryParticipantExternalIdentityId);
}
