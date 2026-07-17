using PublisherContracts = umbral_backend.Application.Sessions.Common;
using PublisherSessionState = umbral_backend.Domain.Enums.SessionState;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

[Collection(PostgreSqlCollection.Name)]
public sealed class CrossServiceConsumerWiringTests : IAsyncLifetime
{
    private readonly CrossServiceConsumerHarness _harness;

    public CrossServiceConsumerWiringTests(PostgreSqlFixture fixture)
    {
        _harness = new CrossServiceConsumerHarness(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _harness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task AnswerRegistered_PublisherContract_ReachesProductionConsumer()
    {
        var submissionId = Guid.NewGuid();
        var message = new PublisherContracts.AnswerRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Guardrail Team",
            submissionId,
            Guid.NewGuid(),
            1,
            2,
            true,
            125,
            DateTimeOffset.UtcNow);

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.ScoreEntries
                .AnyAsync(entry => entry.SourceEntityId == submissionId, cancellationToken));
    }

    [Fact]
    public async Task TargetResolved_PublisherContract_ReachesProductionConsumer()
    {
        var evidenceSubmissionId = Guid.NewGuid();
        var message = new PublisherContracts.TargetResolvedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Guardrail Team",
            evidenceSubmissionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            300,
            DateTimeOffset.UtcNow);

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.ScoreEntries
                .AnyAsync(entry => entry.SourceEntityId == evidenceSubmissionId, cancellationToken));
    }

    [Fact]
    public async Task OperatorAssigned_PublisherContract_ReachesProductionConsumer()
    {
        var liveSessionId = Guid.NewGuid();
        var operatorUserId = Guid.NewGuid();
        var message = new PublisherContracts.LiveSessionOperatorAssignedIntegrationEvent(
            liveSessionId,
            operatorUserId,
            DateTimeOffset.UtcNow);

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.SessionOperatorAssignments.AnyAsync(
                assignment => assignment.LiveSessionId == liveSessionId
                    && assignment.AssignedOperatorUserId == operatorUserId,
                cancellationToken));
    }

    [Fact]
    public async Task SessionStateChanged_PublisherContract_AppendsHistoryRow()
    {
        var liveSessionId = Guid.NewGuid();
        var responsibleUserExternalId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;
        var message = new PublisherContracts.SessionStateChangedIntegrationEvent(
            liveSessionId,
            PublisherSessionState.Scheduled,
            PublisherSessionState.Preparing,
            changedAt,
            responsibleUserExternalId,
            "Operator started preparation");

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.SessionEvents.AnyAsync(
                sessionEvent => sessionEvent.LiveSessionId == liveSessionId
                    && sessionEvent.EventType == "SessionStateChanged"
                    && sessionEvent.ResponsibleUserExternalId == responsibleUserExternalId,
                cancellationToken));
    }

    [Fact]
    public async Task QuestionClosed_PublisherContract_AppendsHistoryRow()
    {
        var liveSessionId = Guid.NewGuid();
        var closedAt = DateTimeOffset.UtcNow;
        var message = new PublisherContracts.QuestionClosedIntegrationEvent(liveSessionId, 3, closedAt);

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.SessionEvents.AnyAsync(
                sessionEvent => sessionEvent.LiveSessionId == liveSessionId
                    && sessionEvent.EventType == "QuestionClosed",
                cancellationToken));
    }

    [Fact]
    public async Task SessionResultsFinalized_PublisherContract_AppendsHistoryRow()
    {
        var liveSessionId = Guid.NewGuid();
        var finishedAt = DateTimeOffset.UtcNow;
        var message = new PublisherContracts.SessionResultsFinalizedIntegrationEvent(liveSessionId, finishedAt);

        await _harness.PublishAndWaitForEffectAsync(
            message,
            (context, cancellationToken) => context.SessionEvents.AnyAsync(
                sessionEvent => sessionEvent.LiveSessionId == liveSessionId
                    && sessionEvent.EventType == "SessionResultsFinalized",
                cancellationToken));
    }
}
