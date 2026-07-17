using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionEventHistoryRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public SessionEventHistoryRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Append_RedeliveredSourceFact_PersistsExactlyOnce()
    {
        var liveSessionId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;

        await using (var firstContext = _contextFactory.Create())
        {
            await new SessionEventHistoryRepository(firstContext).AppendAsync(
                SessionEvent.ForStateChange(
                    liveSessionId,
                    SessionState.Scheduled,
                    SessionState.Preparing,
                    changedAt,
                    Guid.NewGuid(),
                    "Preparing"),
                CancellationToken.None);
        }

        await using (var redeliveryContext = _contextFactory.Create())
        {
            await new SessionEventHistoryRepository(redeliveryContext).AppendAsync(
                SessionEvent.ForStateChange(
                    liveSessionId,
                    SessionState.Scheduled,
                    SessionState.Preparing,
                    changedAt,
                    Guid.NewGuid(),
                    "Preparing"),
                CancellationToken.None);
        }

        await using var verificationContext = _contextFactory.Create();
        var rows = await verificationContext.SessionEvents
            .Where(sessionEvent => sessionEvent.LiveSessionId == liveSessionId)
            .ToListAsync();

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Append_ConcurrentRedelivery_PersistsExactlyOnceWithoutThrowing()
    {
        var liveSessionId = Guid.NewGuid();
        var closedAt = DateTimeOffset.UtcNow;

        async Task AppendAsync()
        {
            await using var context = _contextFactory.Create();
            await new SessionEventHistoryRepository(context).AppendAsync(
                SessionEvent.ForQuestionClosed(liveSessionId, 4, closedAt),
                CancellationToken.None);
        }

        var act = () => Task.WhenAll(AppendAsync(), AppendAsync());

        await act.Should().NotThrowAsync();

        await using var verificationContext = _contextFactory.Create();
        var count = await verificationContext.SessionEvents
            .CountAsync(sessionEvent => sessionEvent.LiveSessionId == liveSessionId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetBySession_AppliesTeamFilterAndOrdersChronologically()
    {
        var liveSessionId = Guid.NewGuid();
        var selectedTeamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        await using (var context = _contextFactory.Create())
        {
            var repository = new SessionEventHistoryRepository(context);
            await repository.AppendAsync(
                SessionEvent.ForEvidenceSubmitted(
                    liveSessionId,
                    selectedTeamId,
                    Guid.NewGuid(),
                    "TriviaAnswer",
                    occurredAt.AddMinutes(1)),
                CancellationToken.None);
            await repository.AppendAsync(
                SessionEvent.ForEvidenceSubmitted(
                    liveSessionId,
                    selectedTeamId,
                    Guid.NewGuid(),
                    "TargetScan",
                    occurredAt),
                CancellationToken.None);
            await repository.AppendAsync(
                SessionEvent.ForEvidenceSubmitted(
                    liveSessionId,
                    otherTeamId,
                    Guid.NewGuid(),
                    "TargetScan",
                    occurredAt.AddMinutes(-1)),
                CancellationToken.None);
        }

        await using var readContext = _contextFactory.Create();
        var rows = await new SessionEventHistoryRepository(readContext).GetBySessionAsync(
            liveSessionId,
            selectedTeamId,
            CancellationToken.None);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(sessionEvent => sessionEvent.TeamId == selectedTeamId);
        rows.Select(sessionEvent => sessionEvent.OccurredAt).Should().BeInAscendingOrder();
    }
}
