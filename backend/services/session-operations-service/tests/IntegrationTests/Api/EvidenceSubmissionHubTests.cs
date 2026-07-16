using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-24B operator-only evidence/submission transport: both pushes reach the operator-only group
// (live-session-operators:{id}) that operators join via JoinLiveSessionAsOperatorAsync, and never the
// participant-shared live-session:{id} group. A trace row names its team and origin, so a leak here
// would hand one team another's submissions — this is the only gate.
// The seed is trivia because this asserts the transport, not evidence production; the broadcaster is
// invoked directly, so the mission form the session runs is irrelevant to what is being proven.
[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceSubmissionHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 57;
    private const string OperatorExternalIdentityId = "kc-operator-57";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public EvidenceSubmissionHubTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.EligibleTeamsClient.IsEligible = true;
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
    public async Task EvidenceSubmissionRegistered_ReachesJoinedOperator_ButNotParticipantConnection()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveSessionAsync(externalIdentityId);

        await using var operatorConnection = CreateHubConnection(OperatorExternalIdentityId, "Operator", "operator@example.com");
        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");

        var operatorReceived = new TaskCompletionSource<EvidenceSubmissionRegisteredNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participantReceived = new TaskCompletionSource<EvidenceSubmissionRegisteredNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        operatorConnection.On<EvidenceSubmissionRegisteredNotificationDto>(
            SignalREvidenceSubmissionBroadcaster.EvidenceSubmissionRegisteredMethod,
            notification => operatorReceived.TrySetResult(notification));
        participant.On<EvidenceSubmissionRegisteredNotificationDto>(
            SignalREvidenceSubmissionBroadcaster.EvidenceSubmissionRegisteredMethod,
            notification => participantReceived.TrySetResult(notification));

        await operatorConnection.StartAsync();
        await participant.StartAsync();

        await operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var evidenceSubmissionId = Guid.NewGuid();
        var notification = new EvidenceSubmissionRegisteredNotificationDto(
            seeded.LiveSessionId,
            evidenceSubmissionId,
            seeded.TeamId,
            seeded.SubstageSnapshotId,
            SubmissionType: nameof(EvidenceSubmissionType.TreasureHuntQrScan),
            OriginReference: "target:9f1c",
            SubmittedAt: DateTimeOffset.UtcNow,
            ValidationState: nameof(EvidenceValidationState.Pending));

        await BroadcastAsync((broadcaster, token) =>
            broadcaster.BroadcastEvidenceSubmissionRegisteredAsync(notification, token));

        var completed = await Task.WhenAny(operatorReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(operatorReceived.Task, "the operator joined the operator-only group");

        var received = await operatorReceived.Task;
        received.LiveSessionId.Should().Be(seeded.LiveSessionId);
        received.EvidenceSubmissionId.Should().Be(evidenceSubmissionId);
        received.TeamId.Should().Be(seeded.TeamId);
        received.OriginReference.Should().Be("target:9f1c");
        received.ValidationState.Should().Be("Pending");

        // The participant shares live-session:{id} but not the operator-only group, so it must never see
        // the submission even though the broadcast already fired.
        participantReceived.Task.IsCompleted.Should().BeFalse("evidence submissions must not leak to participants");
    }

    [Fact]
    public async Task EvidenceSubmissionResolved_ReachesJoinedOperator_ButNotParticipantConnection()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveSessionAsync(externalIdentityId);

        await using var operatorConnection = CreateHubConnection(OperatorExternalIdentityId, "Operator", "operator@example.com");
        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");

        var operatorReceived = new TaskCompletionSource<EvidenceSubmissionResolvedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participantReceived = new TaskCompletionSource<EvidenceSubmissionResolvedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        operatorConnection.On<EvidenceSubmissionResolvedNotificationDto>(
            SignalREvidenceSubmissionBroadcaster.EvidenceSubmissionResolvedMethod,
            notification => operatorReceived.TrySetResult(notification));
        participant.On<EvidenceSubmissionResolvedNotificationDto>(
            SignalREvidenceSubmissionBroadcaster.EvidenceSubmissionResolvedMethod,
            notification => participantReceived.TrySetResult(notification));

        await operatorConnection.StartAsync();
        await participant.StartAsync();

        await operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var notification = new EvidenceSubmissionResolvedNotificationDto(
            seeded.LiveSessionId,
            Guid.NewGuid(),
            seeded.TeamId,
            seeded.SubstageSnapshotId,
            SubmissionType: nameof(EvidenceSubmissionType.TreasureHuntQrScan),
            SubmittedAt: DateTimeOffset.UtcNow.AddSeconds(-4),
            ValidationState: nameof(EvidenceValidationState.Rejected),
            RejectionReason: "Este objetivo ya fue resuelto por otro equipo.",
            ResolvedAt: DateTimeOffset.UtcNow);

        await BroadcastAsync((broadcaster, token) =>
            broadcaster.BroadcastEvidenceSubmissionResolvedAsync(notification, token));

        var completed = await Task.WhenAny(operatorReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(operatorReceived.Task, "the operator joined the operator-only group");

        var received = await operatorReceived.Task;
        received.ValidationState.Should().Be("Rejected");
        received.RejectionReason.Should().Be("Este objetivo ya fue resuelto por otro equipo.");

        participantReceived.Task.IsCompleted.Should().BeFalse("resolutions must not leak to participants");
    }

    private async Task BroadcastAsync(Func<IEvidenceSubmissionBroadcaster, CancellationToken, Task> broadcast)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IEvidenceSubmissionBroadcaster>();
        await broadcast(broadcaster, CancellationToken.None);
    }

    private HubConnection CreateHubConnection(string userId, string role, string email)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "/hubs/sessions"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers["X-User-Id"] = userId;
                    options.Headers["X-User-Role"] = role;
                    options.Headers["X-User-Email"] = email;
                })
            .Build();
    }

    private async Task<SeededSession> SeedActiveSessionAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Evidence Signal Quiz",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "What is the closest planet to the Sun?",
                    1,
                    100,
                    300,
                    "Mercury is the closest planet.",
                    [
                        TriviaOptionSnapshot.Create("Mercury", 1, true),
                        TriviaOptionSnapshot.Create("Venus", 2, false)
                    ])
            ]);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"EVD-{Guid.NewGuid():N}"[..12],
            "Evidence Signal Session",
            45,
            createdAt,
            snapshot);

        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));
        var participant = session.AdmitParticipant(
            externalIdentityId,
            "Nova",
            team.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);
        session.ActivateQuestion(0, createdAt.AddMinutes(3));

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId, triviaSubstage.SubstageSnapshotId);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId, Guid SubstageSnapshotId);
}
