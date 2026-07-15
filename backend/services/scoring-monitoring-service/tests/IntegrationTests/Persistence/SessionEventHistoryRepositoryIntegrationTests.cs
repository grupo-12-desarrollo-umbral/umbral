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
                    42,
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
                    42,
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
}
