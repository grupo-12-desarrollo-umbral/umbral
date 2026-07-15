using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class EvidenceTraceRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;

    public EvidenceTraceRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task UpsertAndReadBack_RoundTripsAllFields()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var activeSubstageId = Guid.NewGuid();
        var submittedByParticipantId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);
        const string originReference = "question:2";

        var entry = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            liveSessionId,
            teamId,
            activeSubstageId,
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId,
            originReference,
            submittedAt);

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var assertRepository = new EvidenceTraceRepository(assertContext);
        var reloaded = await assertRepository.GetByEvidenceSubmissionIdAsync(
            evidenceSubmissionId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.EvidenceSubmissionId.Should().Be(evidenceSubmissionId);
        reloaded.LiveSessionId.Should().Be(liveSessionId);
        reloaded.TeamId.Should().Be(teamId);
        reloaded.ActiveSubstageId.Should().Be(activeSubstageId);
        reloaded.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        reloaded.SubmittedByParticipantId.Should().Be(submittedByParticipantId);
        reloaded.OriginReference.Should().Be(originReference);
        reloaded.SubmittedAt.Should().BeCloseTo(submittedAt, TimeSpan.FromMicroseconds(1));
        reloaded.ValidationState.Should().Be(EvidenceValidationState.Pending);
        reloaded.RejectionReason.Should().BeNull();
        reloaded.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_WithSameEvidenceSubmissionId_Twice_YieldsOneRow()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);

        var entry = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId: null,
            originReference: null,
            submittedAt);

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry, CancellationToken.None);
        }

        // Second upsert with the same EvidenceSubmissionId — must not throw, must not duplicate.
        await using (var secondContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(secondContext);
            await repository.UpsertAsync(entry, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var count = await assertContext.Set<EvidenceTraceEntry>()
            .CountAsync(e => e.EvidenceSubmissionId == evidenceSubmissionId);

        count.Should().Be(1, "upserting the same EvidenceSubmissionId twice must result in one row");
    }

    [Fact]
    public async Task Upsert_ConcurrentRedeliveryOfSameEvidenceSubmissionId_IsIdempotentAndDoesNotThrow()
    {
        // Regression for the EvidenceSubmission_*error dead-letter storm: MassTransit is at-least-once,
        // so the same evidence_submission_id can be consumed concurrently. The read-then-Add in
        // UpsertAsync is a TOCTOU race — both deliveries read "no row" and both INSERT, so without the
        // duplicate-key catch the loser throws 23505 and dead-letters. Two separate contexts (one per
        // simulated delivery) upserting the same id concurrently must settle to a single row, no throw.
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);

        EvidenceTraceEntry NewEntry() => EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId: null,
            originReference: null,
            submittedAt);

        await using var firstContext = BuildContext();
        await using var secondContext = BuildContext();

        var act = () => Task.WhenAll(
            new EvidenceTraceRepository(firstContext).UpsertAsync(NewEntry(), CancellationToken.None),
            new EvidenceTraceRepository(secondContext).UpsertAsync(NewEntry(), CancellationToken.None));

        await act.Should().NotThrowAsync();

        await using var assertContext = BuildContext();
        var count = await assertContext.Set<EvidenceTraceEntry>()
            .CountAsync(e => e.EvidenceSubmissionId == evidenceSubmissionId);

        count.Should().Be(1, "a concurrent redelivery must be an idempotent no-op, not a duplicate-key fault");
    }

    [Fact]
    public async Task Upsert_RegistrationAfterResolutionStub_MergesContextAndKeepsTerminalState()
    {
        // Finding 1 residual: Accepted/Rejected can be consumed before Registered, in which case the
        // resolution handler writes a stub with no participant/origin. When the registration then
        // arrives it builds a *fresh* entry carrying that context — so the upsert must merge it onto
        // the existing row while leaving the terminal state alone ("once terminal, always terminal").
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var activeSubstageId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);
        var resolvedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 9, TimeSpan.Zero);
        var submittedByParticipantId = Guid.NewGuid();

        // 1. Resolution arrives first → stub row, terminal, context fields empty.
        var stub = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            liveSessionId,
            teamId,
            activeSubstageId,
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId: null,
            originReference: null,
            submittedAt);
        stub.MarkAccepted(resolvedAt);

        await using (var stubContext = BuildContext())
        {
            await new EvidenceTraceRepository(stubContext).UpsertAsync(stub, CancellationToken.None);
        }

        // 2. Registration arrives second → fresh entry with the context the stub lacks.
        var registration = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            liveSessionId,
            teamId,
            activeSubstageId,
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId,
            "question:7",
            submittedAt);

        await using (var registrationContext = BuildContext())
        {
            await new EvidenceTraceRepository(registrationContext)
                .UpsertAsync(registration, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloaded = await new EvidenceTraceRepository(assertContext)
            .GetByEvidenceSubmissionIdAsync(evidenceSubmissionId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.SubmittedByParticipantId.Should().Be(
            submittedByParticipantId,
            "the registration's context must survive the merge onto a resolution-first stub");
        reloaded.OriginReference.Should().Be("question:7");
        reloaded.ValidationState.Should().Be(
            EvidenceValidationState.Accepted,
            "merging the registration context must not flip a resolved entry back to Pending");
        reloaded.ResolvedAt.Should().BeCloseTo(resolvedAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task Upsert_ConcurrentCrossEventRace_PreservesBothContributions()
    {
        // Finding 1 residual, concurrent form: registration and resolution race, both read "no row",
        // both INSERT. The loser used to be swallowed as a no-op, dropping its half — leaving either a
        // stub with no context, or an entry stuck Pending despite having been resolved. Whichever wins,
        // the settled row must carry both contributions.
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var activeSubstageId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);
        var resolvedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 9, TimeSpan.Zero);
        var submittedByParticipantId = Guid.NewGuid();

        EvidenceTraceEntry Registration() => EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId, liveSessionId, teamId, activeSubstageId,
            EvidenceSubmissionType.TriviaAnswer, submittedByParticipantId, "question:9", submittedAt);

        EvidenceTraceEntry Resolution()
        {
            var stub = EvidenceTraceEntry.ForRegistration(
                evidenceSubmissionId, liveSessionId, teamId, activeSubstageId,
                EvidenceSubmissionType.TriviaAnswer, submittedByParticipantId: null,
                originReference: null, submittedAt);
            stub.MarkAccepted(resolvedAt);
            return stub;
        }

        await using var firstContext = BuildContext();
        await using var secondContext = BuildContext();

        var act = () => Task.WhenAll(
            new EvidenceTraceRepository(firstContext).UpsertAsync(Registration(), CancellationToken.None),
            new EvidenceTraceRepository(secondContext).UpsertAsync(Resolution(), CancellationToken.None));

        await act.Should().NotThrowAsync();

        await using var assertContext = BuildContext();
        var rows = await assertContext.Set<EvidenceTraceEntry>()
            .Where(e => e.EvidenceSubmissionId == evidenceSubmissionId)
            .ToListAsync();

        rows.Should().HaveCount(1, "a cross-event race must settle to one row");
        rows[0].SubmittedByParticipantId.Should().Be(
            submittedByParticipantId, "the registration's context must survive whichever side lost the insert");
        rows[0].OriginReference.Should().Be("question:9");
        rows[0].ValidationState.Should().Be(
            EvidenceValidationState.Accepted, "the resolution's terminal state must survive whichever side lost the insert");
        rows[0].ResolvedAt.Should().BeCloseTo(resolvedAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task Upsert_RoundTripsAcceptedState()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);
        var resolvedAt = submittedAt.AddSeconds(1);

        var entry = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId: null,
            originReference: "target:abc-def",
            submittedAt);

        entry.MarkAccepted(resolvedAt);

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloaded = await new EvidenceTraceRepository(assertContext)
            .GetByEvidenceSubmissionIdAsync(evidenceSubmissionId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        reloaded.RejectionReason.Should().BeNull();
        reloaded.ResolvedAt.Should().BeCloseTo(resolvedAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task Upsert_RoundTripsRejectedStateWithReason()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var evidenceSubmissionId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 5, TimeSpan.Zero);
        var resolvedAt = submittedAt.AddSeconds(1);
        const string reason = "TargetOutsideActiveSubstage";

        var entry = EvidenceTraceEntry.ForRegistration(
            evidenceSubmissionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId: null,
            originReference: "target:xyz-123",
            submittedAt);

        entry.MarkRejected(reason, resolvedAt);

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var reloaded = await new EvidenceTraceRepository(assertContext)
            .GetByEvidenceSubmissionIdAsync(evidenceSubmissionId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        reloaded.RejectionReason.Should().Be(reason);
        reloaded.ResolvedAt.Should().BeCloseTo(resolvedAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task ListBySessionAsync_ReturnsEntriesForSession()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var liveSessionId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        var entry1 = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(), liveSessionId, teamA, Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer, null, null, baseTime);

        var entry2 = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(), liveSessionId, teamB, Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan, null, null, baseTime.AddSeconds(5));

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry1, CancellationToken.None);
            await repository.UpsertAsync(entry2, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var results = await new EvidenceTraceRepository(assertContext)
            .ListBySessionAsync(liveSessionId, teamId: null, CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().BeInAscendingOrder(e => e.SubmittedAt);
    }

    [Fact]
    public async Task ListBySessionAsync_WithTeamId_FiltersToTeam()
    {
        await using var resetContext = BuildContext();
        await resetContext.LiveSessions.ExecuteDeleteAsync();
        await resetContext.Set<EvidenceTraceEntry>().ExecuteDeleteAsync();

        var liveSessionId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

        var entry1 = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(), liveSessionId, teamA, Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer, null, null, baseTime);

        var entry2 = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(), liveSessionId, teamB, Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan, null, null, baseTime.AddSeconds(5));

        await using (var seedContext = BuildContext())
        {
            var repository = new EvidenceTraceRepository(seedContext);
            await repository.UpsertAsync(entry1, CancellationToken.None);
            await repository.UpsertAsync(entry2, CancellationToken.None);
        }

        await using var assertContext = BuildContext();
        var results = await new EvidenceTraceRepository(assertContext)
            .ListBySessionAsync(liveSessionId, teamId: teamA, CancellationToken.None);

        results.Should().ContainSingle();
        results.Single().TeamId.Should().Be(teamA);
    }

    private ApplicationDbContext BuildContext()
    {
        return _contextFactory.Create();
    }
}
