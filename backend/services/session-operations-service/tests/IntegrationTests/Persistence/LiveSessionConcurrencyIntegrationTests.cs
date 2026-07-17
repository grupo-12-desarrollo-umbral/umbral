using Microsoft.EntityFrameworkCore;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Proves the two DB-level guards on concurrent writes to one live session actually fire, and that the
// repository reports both as the same recoverable ConcurrentModificationException. They cover
// complementary halves: the xmin token catches racing writes to the principal row, and the unique
// index catches racing inserts into an owned collection — which emit no UPDATE and so never reach the
// token. Neither alone is sufficient.
[Collection(PostgreSqlCollection.Name)]
public sealed class LiveSessionConcurrencyIntegrationTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly PersistenceTestContextFactory _contextFactory;

    public LiveSessionConcurrencyIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    // Two writers mutate a principal column from separately-loaded copies. The xmin token must let the
    // first commit and reject the second, rather than silently applying a last-write-wins overwrite.
    [Fact]
    public async Task UpdateAsync_WhenPrincipalRowChangedConcurrently_ThrowsConcurrentModification()
    {
        var sessionId = await SeedScheduledTreasureHuntSessionAsync();

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();

        var winnerRepository = new LiveSessionRepository(winnerContext);
        var loserRepository = new LiveSessionRepository(loserContext);

        // Both read the same xmin before either writes.
        var winnerSession = await winnerRepository.GetByIdAsync(sessionId, CancellationToken.None);
        var loserSession = await loserRepository.GetByIdAsync(sessionId, CancellationToken.None);

        var transitionPolicy = new SessionStateTransitionPolicy();
        winnerSession!.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        await winnerRepository.UpdateAsync(winnerSession, CancellationToken.None);

        loserSession!.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        var act = () => loserRepository.UpdateAsync(loserSession, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrentModificationException>(
            "the second writer read a stale xmin, so its UPDATE must match no row rather than overwrite the winner");
    }

    // The same race on an owned-collection insert. This is the case the token cannot see: adding a
    // submission touches no principal column, so EF emits no UPDATE on live_sessions and xmin is never
    // compared. Without the unique index both scans commit and the target is counted twice.
    [Fact]
    public async Task UpdateAsync_WhenSameTargetResolvedConcurrentlyByOneTeam_ThrowsConcurrentModification()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();

        var winnerRepository = new LiveSessionRepository(winnerContext);
        var loserRepository = new LiveSessionRepository(loserContext);

        // Both read before either writes, so each in-memory alreadyResolved check sees no accepted
        // resolution and both scans are accepted by the domain — the TOCTOU window the index closes.
        var winnerSession = await winnerRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var loserSession = await loserRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        winnerSession!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        await winnerRepository.UpdateAsync(winnerSession, CancellationToken.None);

        loserSession!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        var act = () => loserRepository.UpdateAsync(loserSession, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrentModificationException>(
            "ux_treasure_evidence_accepted_target must reject the second accepted resolution of one target");

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        reloaded!.TreasureEvidenceSubmissions
            .Count(submission => submission.ValidationState == EvidenceValidationState.Accepted)
            .Should()
            .Be(1, "a double-counted target would let a team clear a substage without resolving all of them");
    }

    // The index must not block the ordinary case of re-scanning an already-resolved code: those land as
    // *rejected* rows that keep the target id they resolved to, so an unfiltered index would collide on
    // the second rejection. Guards the Accepted filter against being dropped as an optimisation.
    [Fact]
    public async Task UpdateAsync_WhenTargetRescannedAfterResolution_PersistsRepeatedRejections()
    {
        var seeded = await SeedActiveTreasureHuntSessionAsync();

        await using var context = _contextFactory.Create();
        var repository = new LiveSessionRepository(context);
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        session!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        session.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(20));
        session.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(25));

        await repository.UpdateAsync(session, CancellationToken.None);

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        reloaded!.TreasureEvidenceSubmissions.Should().HaveCount(3);
        reloaded.TreasureEvidenceSubmissions
            .Count(submission => submission.ValidationState == EvidenceValidationState.Accepted)
            .Should()
            .Be(1);
        reloaded.TreasureEvidenceSubmissions
            .Count(submission => submission.ResolutionRejectionReason == TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam)
            .Should()
            .Be(2, "repeat scans of a resolved target are rejected, not rejected-and-then-blocked by the index");
    }

    private async Task<Guid> SeedScheduledTreasureHuntSessionAsync()
    {
        var session = BuildTreasureHuntSession();

        await using var seedContext = _contextFactory.Create();
        await seedContext.LiveSessions.ExecuteDeleteAsync();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return session.LiveSessionId;
    }

    private async Task<SeededSession> SeedActiveTreasureHuntSessionAsync()
    {
        var session = BuildTreasureHuntSession();

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, ActiveAt, transitionPolicy);

        await using var seedContext = _contextFactory.Create();
        await seedContext.LiveSessions.ExecuteDeleteAsync();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, session.Teams.Single().TeamId);
    }

    private static LiveSession BuildTreasureHuntSession()
    {
        var sourceMissionId = Guid.NewGuid();
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Concurrency Session",
            45,
            ActiveAt.AddMinutes(-10),
            MissionRuntimeSnapshot.Create(
                sourceMissionId,
                "Mission Runtime",
                MaximumTime.Create(45),
                [
                    StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
                ],
                [
                    TargetSnapshot.Create(
                        treasureHuntSubstage.SubstageSnapshotId,
                        "Target Alpha",
                        "QR-ALPHA",
                        1,
                        true,
                        100,
                        4.711,
                        -74.0721,
                        "Look under the stairs",
                        "AfterPreviousTarget")
                ],
                []));
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
