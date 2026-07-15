using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionAssignmentRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public SessionAssignmentRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task UpsertAsync_InsertThenRead_RoundTripsCorrectly()
    {
        var liveSessionId = Guid.NewGuid();
        var operatorUserId = Guid.NewGuid();

        await using (var context = _contextFactory.Create())
        {
            var repository = new SessionAssignmentRepository(context);

            await repository.UpsertAsync(liveSessionId, operatorUserId, CancellationToken.None);
        }

        await using (var readContext = _contextFactory.Create())
        {
            var repository = new SessionAssignmentRepository(readContext);

            var result = await repository.GetAssignedOperatorUserIdAsync(liveSessionId, CancellationToken.None);

            result.Should().Be(operatorUserId);
        }
    }

    [Fact]
    public async Task UpsertAsync_UpdateExisting_DoesNotCreateDuplicate()
    {
        var liveSessionId = Guid.NewGuid();
        var firstOperator = Guid.NewGuid();
        var secondOperator = Guid.NewGuid();

        await using (var context = _contextFactory.Create())
        {
            var repository = new SessionAssignmentRepository(context);

            await repository.UpsertAsync(liveSessionId, firstOperator, CancellationToken.None);
        }

        await using (var context = _contextFactory.Create())
        {
            var repository = new SessionAssignmentRepository(context);

            await repository.UpsertAsync(liveSessionId, secondOperator, CancellationToken.None);
        }

        await using (var readContext = _contextFactory.Create())
        {
            var repository = new SessionAssignmentRepository(readContext);

            var result = await repository.GetAssignedOperatorUserIdAsync(liveSessionId, CancellationToken.None);
            result.Should().Be(secondOperator);

            var assignments = await readContext.SessionOperatorAssignments.ToListAsync();
            assignments.Count(a => a.LiveSessionId == liveSessionId).Should().Be(1);
        }
    }

    [Fact]
    public async Task GetAssignedOperatorUserIdAsync_WhenNoAssignment_ReturnsNull()
    {
        var liveSessionId = Guid.NewGuid();

        await using var context = _contextFactory.Create();
        var repository = new SessionAssignmentRepository(context);

        var result = await repository.GetAssignedOperatorUserIdAsync(liveSessionId, CancellationToken.None);

        result.Should().BeNull();
    }
}
