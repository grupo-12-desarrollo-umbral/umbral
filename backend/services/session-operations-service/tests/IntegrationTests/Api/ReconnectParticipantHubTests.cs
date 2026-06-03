using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Api.Hubs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ReconnectParticipantHubTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public ReconnectParticipantHubTests(PostgreSqlFixture fixture)
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
    public async Task ReconnectAsync_WithAuthorizedDisconnectedParticipant_RestoresLiveContext()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedSessionWithDisconnectedParticipantAsync(externalIdentityId, SessionState.Active);
        await using var connection = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");

        await connection.StartAsync();

        var payload = await connection.InvokeAsync<ReconnectParticipantResultDto>(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", 4, null));

        payload.LiveSessionId.Should().Be(seeded.LiveSessionId);
        payload.TeamId.Should().Be(seeded.TeamId);
        payload.SessionParticipantId.Should().Be(seeded.SessionParticipantId!.Value);
        payload.TeamDisplayName.Should().Be("Red");
        payload.IsReconnect.Should().BeTrue();
        payload.SessionState.Should().Be(nameof(SessionState.Active));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedSession = await dbContext.LiveSessions
            .Include(session => session.Participants)
            .SingleAsync(session => session.LiveSessionId == seeded.LiveSessionId);
        var participant = persistedSession.Participants
            .Single(p => p.SessionParticipantId == seeded.SessionParticipantId);
        participant.IsDisconnected.Should().BeFalse();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    [Fact]
    public async Task StartAsync_WithoutTrustedHeaders_IsRejected()
    {
        await using var connection = CreateHubConnection();

        var act = () => connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task StartAsync_WithOperatorRole_IsRejected()
    {
        await using var connection = CreateHubConnection(Guid.NewGuid().ToString(), "Operator", "operator@example.com");

        var act = () => connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ReconnectAsync_FirstJoinIntoActiveSession_IsRejected()
    {
        var seeded = await SeedSessionWithoutParticipantAsync(SessionState.Active);
        await using var connection = CreateHubConnection(Guid.NewGuid().ToString(), "Participant", "newcomer@example.com");

        await connection.StartAsync();

        var act = () => connection.InvokeAsync<ReconnectParticipantResultDto>(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Newcomer", 4, null));

        await act.Should().ThrowAsync<HubException>();
    }

    private HubConnection CreateHubConnection(
        string? userId = null,
        string? role = null,
        string? email = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "/hubs/sessions"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();

                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        options.Headers["X-User-Id"] = userId;
                    }

                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        options.Headers["X-User-Role"] = role;
                    }

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        options.Headers["X-User-Email"] = email;
                    }
                })
            .Build();
    }

    private async Task<SeededParticipantSession> SeedSessionWithDisconnectedParticipantAsync(
        Guid externalIdentityId,
        SessionState sessionState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var session = CreateSession(createdAt);
        var team = session.RegisterTeam("Red", "RED-01");
        var participant = session.AdmitParticipant(
            externalIdentityId,
            "Nova",
            team.TeamId,
            createdAt.AddMinutes(1),
            4,
            new JoinPolicy()).Participant;

        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));
        MoveToState(session, sessionState, createdAt.AddMinutes(2));

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededParticipantSession(session.LiveSessionId, team.TeamId, participant.SessionParticipantId, null);
    }

    private async Task<SeededParticipantSession> SeedSessionWithoutParticipantAsync(SessionState sessionState)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var session = CreateSession(createdAt);
        var team = session.RegisterTeam("Red", "RED-01");
        MoveToState(session, sessionState, createdAt.AddMinutes(2));

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededParticipantSession(session.LiveSessionId, team.TeamId, null, null);
    }

    private static void MoveToState(LiveSession liveSession, SessionState state, DateTimeOffset occurredAt)
    {
        var transitionPolicy = new SessionStateTransitionPolicy();
        switch (state)
        {
            case SessionState.Scheduled:
                break;
            case SessionState.Active:
                liveSession.MoveTo(SessionState.Preparing, occurredAt, transitionPolicy);
                liveSession.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), transitionPolicy);
                break;
            case SessionState.Finished:
                liveSession.MoveTo(SessionState.Preparing, occurredAt, transitionPolicy);
                liveSession.MoveTo(SessionState.Active, occurredAt.AddMinutes(1), transitionPolicy);
                liveSession.MoveTo(SessionState.Finished, occurredAt.AddMinutes(2), transitionPolicy);
                break;
            case SessionState.Cancelled:
                liveSession.MoveTo(SessionState.Cancelled, occurredAt, transitionPolicy);
                break;
            default:
                liveSession.MoveTo(state, occurredAt, transitionPolicy);
                break;
        }
    }

    private static LiveSession CreateSession(DateTimeOffset scheduledAt)
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Reconnect Session",
            45,
            scheduledAt);
    }

    private sealed record SeededParticipantSession(
        Guid LiveSessionId,
        Guid TeamId,
        Guid? SessionParticipantId,
        Guid? OtherTeamId);
}
