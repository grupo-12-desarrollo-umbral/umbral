using Microsoft.EntityFrameworkCore;
using Npgsql;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
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

    // Finding 1 at the persistence layer. Two devices on one team resolve *different* targets from the
    // same stale xmin. No unique index spans two different targets — ux_treasure_evidence_accepted_target
    // is per-target — so nothing at the row level catches this: each insert is a child-only write that
    // emits no UPDATE to live_sessions. Only serialising every aggregate write against the root token can
    // force the second save to lose and re-read. RED until Phase 2 marks the root modified on every save.
    [Fact]
    public async Task UpdateAsync_WhenDifferentTargetsResolvedConcurrentlyByOneTeam_ThrowsConcurrentModification()
    {
        var seeded = await SeedActiveTwoTargetSessionAsync();

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();

        var winnerRepository = new LiveSessionRepository(winnerContext);
        var loserRepository = new LiveSessionRepository(loserContext);

        // Both read the same xmin before either writes; each copy therefore sees only its own scan and
        // the domain accepts both, since neither target was resolved in the state it observed.
        var winnerSession = await winnerRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var loserSession = await loserRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        winnerSession!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        await winnerRepository.UpdateAsync(winnerSession, CancellationToken.None);

        loserSession!.RegisterTargetScan(seeded.TeamId, "QR-BETA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        var act = () => loserRepository.UpdateAsync(loserSession, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrentModificationException>(
            "device B resolved a different target from a stale xmin, and only root-level serialisation can " +
            "catch a race no per-target unique index spans");
    }

    // Phase 2's mechanism in isolation: a child-only mutation must still advance the root's xmin. Resolving
    // just one of two targets leaves the substage uncleared, so no reveal opens and no principal column
    // changes — the scan is a pure owned-collection insert. Before Phase 2 that emits no UPDATE to
    // live_sessions and the token does not move, which is precisely why a second stale writer cannot be
    // detected. RED until UpdateAsync forces a principal-row UPDATE on every save.
    [Fact]
    public async Task UpdateAsync_WhenChildOnlyMutationCommitted_AdvancesRootConcurrencyToken()
    {
        var seeded = await SeedActiveTwoTargetSessionAsync();

        var before = await ReadConcurrencyTokenAsync(seeded.LiveSessionId);

        await using var context = _contextFactory.Create();
        var repository = new LiveSessionRepository(context);
        var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Resolve only Alpha; Beta stays open so the substage is not cleared and no reveal window is set.
        session!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), ActiveAt.AddSeconds(15));
        await repository.UpdateAsync(session, CancellationToken.None);

        var after = await ReadConcurrencyTokenAsync(seeded.LiveSessionId);

        after.Should().NotBe(
            before,
            "a child-only insert must still advance the aggregate root's xmin so concurrent writers collide");
    }

    // Phase 3 / Finding 1, full retry follow-through. Two devices on one team resolve *different* targets
    // of a two-target substage from the same stale xmin. Left alone the bug is silent: each device sees
    // only its own resolution, so neither reaches 2/2 and the substage never reveals — every target
    // resolved, no reveal. This proves the serialised path repairs it: device B loses the xmin race, and
    // the ConcurrencyRetryBehaviour's re-read (modelled here by a fresh context) re-runs the domain
    // against a graph where Alpha is already accepted, so resolving Beta now clears the substage and opens
    // the reveal — exactly once, on the committing attempt only.
    [Fact]
    public async Task RegisterTargetScan_WhenSecondDeviceRetriesAfterDifferentTargetResolved_OpensRevealExactlyOnce()
    {
        var seeded = await SeedActiveTwoTargetSessionAsync();

        var deviceARecorder = new RecordingOutboxDomainEventDispatcher();
        var retryRecorder = new RecordingOutboxDomainEventDispatcher();

        await using var deviceAContext = _contextFactory.Create(outboxDispatcher: deviceARecorder);
        await using var deviceBContext = _contextFactory.Create();
        var deviceARepository = new LiveSessionRepository(deviceAContext);
        var deviceBRepository = new LiveSessionRepository(deviceBContext);

        // Both devices read the same xmin before either writes, so each in-memory clear check sees only
        // its own scan; neither reaches 2/2 and the domain opens no reveal on the first pass.
        var deviceASession = await deviceARepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var deviceBSession = await deviceBRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Device A resolves Alpha (1 of 2): accepted, but not a clear, so no reveal window is opened.
        var alphaAt = ActiveAt.AddSeconds(15);
        deviceASession!.RegisterTargetScan(seeded.TeamId, "QR-ALPHA", Guid.NewGuid(), alphaAt);
        await deviceARepository.UpdateAsync(deviceASession, CancellationToken.None);

        // Device B resolves Beta from the now-stale xmin: the root-write barrier makes its save lose.
        deviceBSession!.RegisterTargetScan(seeded.TeamId, "QR-BETA", Guid.NewGuid(), alphaAt);
        var staleSave = () => deviceBRepository.UpdateAsync(deviceBSession, CancellationToken.None);
        await staleSave.Should().ThrowAsync<ConcurrentModificationException>(
            "device B resolved a different target from a stale xmin and only root serialisation catches it");

        // The retry the behaviour would run: a fresh read now sees Alpha accepted, so resolving Beta
        // completes the set and opens the reveal on this — the only committing — attempt.
        var betaAt = ActiveAt.AddSeconds(16);
        await using var retryContext = _contextFactory.Create(outboxDispatcher: retryRecorder);
        var retryRepository = new LiveSessionRepository(retryContext);
        var retrySession = await retryRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        retrySession!.RegisterTargetScan(seeded.TeamId, "QR-BETA", Guid.NewGuid(), betaAt);
        await retryRepository.UpdateAsync(retrySession, CancellationToken.None);

        // Only committed attempts emit: device A published its resolve with no reveal; the retry published
        // the second resolve *and* exactly one reveal-started fact. The stale attempt that rolled back
        // contributes nothing — the outbox insert shares its aborted transaction.
        deviceARecorder.Dispatched.OfType<TargetResolvedEvent>().Should().ContainSingle();
        deviceARecorder.Dispatched.OfType<SubstageRevealStartedEvent>().Should().BeEmpty(
            "resolving one of two targets is not a clear, so no reveal opens on device A's commit");
        retryRecorder.Dispatched.OfType<TargetResolvedEvent>().Should().ContainSingle();
        retryRecorder.Dispatched.OfType<SubstageRevealStartedEvent>().Should().ContainSingle(
            "the clearing scan opens the reveal exactly once, on the committing retry");

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Both distinct targets are resolved exactly once, and the substage is now revealing.
        reloaded!.TreasureEvidenceSubmissions
            .Where(submission => submission.ValidationState == EvidenceValidationState.Accepted)
            .Select(submission => submission.TargetSnapshotId)
            .Should()
            .OnlyHaveUniqueItems().And.HaveCount(2, "every active target is resolved once, none double-counted");
        reloaded.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        reloaded.SubstageRevealUntil.Should().Be(
            betaAt + LiveSession.SubstageRankingRevealDuration,
            "the reveal deadline is set once, by the clearing scan, and never restarted");
    }

    // Phase 4 / Finding 2, the hard cut across teams. One active target; two teams read the same xmin and
    // both scan it. Team A clears the substage and its scan commits, opening the reveal. Team B's in-flight
    // scan then loses the root-write race, and on the retry the domain sees the active reveal and returns
    // SubstageAlreadyCleared — the losing team's late scan is retained-rejected for audit, produces no
    // TargetResolved fact, and leaves the settled reveal untouched.
    [Fact]
    public async Task RegisterTargetScan_WhenLosingTeamRetriesPastHardCut_RejectsAsSubstageAlreadyCleared()
    {
        var seeded = await SeedActiveTwoTeamSessionAsync();

        var teamARecorder = new RecordingOutboxDomainEventDispatcher();
        var teamBRecorder = new RecordingOutboxDomainEventDispatcher();

        await using var teamAContext = _contextFactory.Create(outboxDispatcher: teamARecorder);
        await using var teamBContext = _contextFactory.Create();
        var teamARepository = new LiveSessionRepository(teamAContext);
        var teamBRepository = new LiveSessionRepository(teamBContext);

        // Both teams read the substage open, before either scan commits.
        var teamASession = await teamARepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var teamBSession = await teamBRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Team A's scan is the sole active target, so it clears the substage and opens the reveal.
        var clearAt = ActiveAt.AddSeconds(15);
        teamASession!.RegisterTargetScan(seeded.TeamAId, "QR-ALPHA", Guid.NewGuid(), clearAt);
        await teamARepository.UpdateAsync(teamASession, CancellationToken.None);

        await using var afterClearContext = _contextFactory.Create();
        var afterClear = await new LiveSessionRepository(afterClearContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var settledRevealUntil = afterClear!.SubstageRevealUntil;
        settledRevealUntil.Should().NotBeNull("Team A's clearing scan opens the reveal");

        // Team B's stale scan of the same target loses the root-write race.
        teamBSession!.RegisterTargetScan(seeded.TeamBId, "QR-ALPHA", Guid.NewGuid(), clearAt);
        var staleSave = () => teamBRepository.UpdateAsync(teamBSession, CancellationToken.None);
        await staleSave.Should().ThrowAsync<ConcurrentModificationException>(
            "the winning scan moved the root's xmin, so Team B's child-only insert must lose and retry");

        // On the retry, the fresh graph carries the active reveal, so the domain rejects the late scan.
        var lateAt = ActiveAt.AddSeconds(16);
        await using var retryContext = _contextFactory.Create(outboxDispatcher: teamBRecorder);
        var retryRepository = new LiveSessionRepository(retryContext);
        var retrySession = await retryRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var rejected = retrySession!.RegisterTargetScan(seeded.TeamBId, "QR-ALPHA", Guid.NewGuid(), lateAt);
        await retryRepository.UpdateAsync(retrySession, CancellationToken.None);

        rejected.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        rejected.ResolutionRejectionReason.Should().Be(TargetResolutionRejectionReason.SubstageAlreadyCleared);

        // The clearing team published its resolve and the one reveal; the losing team's retry published
        // neither a resolve nor a reveal — its late scan resolves nothing.
        teamARecorder.Dispatched.OfType<TargetResolvedEvent>().Should().ContainSingle();
        teamARecorder.Dispatched.OfType<SubstageRevealStartedEvent>().Should().ContainSingle();
        teamBRecorder.Dispatched.OfType<TargetResolvedEvent>().Should().BeEmpty(
            "a scan rejected past the hard cut resolves no target, so no scoring fact is emitted");
        teamBRecorder.Dispatched.OfType<SubstageRevealStartedEvent>().Should().BeEmpty();

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        // Exactly one accepted target (Team A); Team B's rejected evidence is retained and queryable.
        reloaded!.TreasureEvidenceSubmissions
            .Count(submission => submission.ValidationState == EvidenceValidationState.Accepted)
            .Should()
            .Be(1, "the losing team's late scan must not add a second accepted resolution");
        reloaded.TreasureEvidenceSubmissions
            .Should()
            .ContainSingle(submission =>
                submission.TeamId == seeded.TeamBId &&
                submission.ValidationState == EvidenceValidationState.Rejected &&
                submission.ResolutionRejectionReason == TargetResolutionRejectionReason.SubstageAlreadyCleared,
                "the hard-cut rejection is persisted for audit");
        reloaded.SubstageRevealUntil.Should().Be(
            settledRevealUntil, "the settled reveal deadline is untouched by the losing team's retry");
    }

    // Phase 5 / Finding 3, capacity. One slot, two devices for *different* participants both read it as
    // free and are admitted by the domain. Serialising every aggregate write makes the second save lose
    // the xmin race even though a join is a child-only insert (no principal column of its own changes);
    // its retry then re-reads the committed member and the domain's own capacity check refuses it. Nothing
    // over-fills the team.
    [Fact]
    public async Task AdmitParticipant_WhenTwoParticipantsRaceForTheLastSlot_OneJoinsAndTheLoserRetriesToCapacityConflict()
    {
        var seeded = await SeedPreparingSingleTeamSessionAsync(capacity: 1);

        await using var winnerContext = _contextFactory.Create();
        await using var loserContext = _contextFactory.Create();
        var winnerRepository = new LiveSessionRepository(winnerContext);
        var loserRepository = new LiveSessionRepository(loserContext);

        // Both read the single slot as free (0 active members) before either commits.
        var winnerSession = await winnerRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var loserSession = await loserRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        winnerSession!.AdmitParticipant(Guid.NewGuid(), "Ada", seeded.TeamId, ActiveAt.AddSeconds(5), new JoinPolicy());
        await winnerRepository.UpdateAsync(winnerSession, CancellationToken.None);

        var loserExternalId = Guid.NewGuid();
        loserSession!.AdmitParticipant(loserExternalId, "Grace", seeded.TeamId, ActiveAt.AddSeconds(5), new JoinPolicy());
        var staleSave = () => loserRepository.UpdateAsync(loserSession, CancellationToken.None);
        await staleSave.Should().ThrowAsync<ConcurrentModificationException>(
            "the second joiner admitted from a stale xmin, so its forced principal-row UPDATE must lose the race");

        // The retry the behaviour would run: a fresh read now shows the slot filled, so the domain's own
        // capacity check refuses the join — the DB never has to enforce capacity because the reread reaches
        // the verdict.
        await using var retryContext = _contextFactory.Create();
        var retryRepository = new LiveSessionRepository(retryContext);
        var retrySession = await retryRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var retryJoin = () =>
        {
            retrySession!.AdmitParticipant(loserExternalId, "Grace", seeded.TeamId, ActiveAt.AddSeconds(6), new JoinPolicy());
            return Task.CompletedTask;
        };
        await retryJoin.Should().ThrowAsync<TeamCapacityReachedException>(
            "on retry the loser observes the committed member and the capacity-1 team is full");

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        reloaded!.Teams.Single().ActiveMemberCount.Should().Be(1, "concurrent joins cannot exceed a capacity-1 team");
    }

    // Phase 5 / Finding 3, active membership. A participant active in team A switches to two *different*
    // teams from two devices sharing one stale xmin. Left alone this leaves the participant active in both
    // B and C — two active memberships. Root serialisation forces device two to lose; its retry re-reads a
    // graph where the participant already sits in B, so SelectTeam releases B before assigning C and the
    // participant finishes in exactly one team. FindAssignedTeam (a SingleOrDefault over active rows) must
    // then not throw on reload.
    [Fact]
    public async Task SelectTeam_WhenParticipantSwitchesToTwoTeamsConcurrently_LeavesExactlyOneActiveMembership()
    {
        var seeded = await SeedPreparingSwitchScenarioAsync();
        var selectionPolicy = new OpenTeamSelectionPolicy();
        var noWhitelist = new HashSet<Guid>();

        await using var deviceOneContext = _contextFactory.Create();
        await using var deviceTwoContext = _contextFactory.Create();
        var deviceOneRepository = new LiveSessionRepository(deviceOneContext);
        var deviceTwoRepository = new LiveSessionRepository(deviceTwoContext);

        // Both devices read the participant active in team A before either switch commits.
        var deviceOneSession = await deviceOneRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        var deviceTwoSession = await deviceTwoRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        deviceOneSession!.SelectTeam(seeded.ExternalIdentityId, "Ada", seeded.TeamBId, noWhitelist, ActiveAt.AddSeconds(5), selectionPolicy);
        await deviceOneRepository.UpdateAsync(deviceOneSession, CancellationToken.None);

        // Device two moves A -> C from the now-stale xmin. Without serialisation this leaves the participant
        // active in both B and C; the root-write barrier makes it lose instead.
        deviceTwoSession!.SelectTeam(seeded.ExternalIdentityId, "Ada", seeded.TeamCId, noWhitelist, ActiveAt.AddSeconds(5), selectionPolicy);
        var staleSave = () => deviceTwoRepository.UpdateAsync(deviceTwoSession, CancellationToken.None);
        await staleSave.Should().ThrowAsync<ConcurrentModificationException>(
            "device two switched from a stale xmin, so its save must lose to device one's committed switch");

        // Retry: a fresh read sees the participant in B, so switching to C releases B first — one active row.
        await using var retryContext = _contextFactory.Create();
        var retryRepository = new LiveSessionRepository(retryContext);
        var retrySession = await retryRepository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
        retrySession!.SelectTeam(seeded.ExternalIdentityId, "Ada", seeded.TeamCId, noWhitelist, ActiveAt.AddSeconds(6), selectionPolicy);
        await retryRepository.UpdateAsync(retrySession, CancellationToken.None);

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        reloaded!.Teams.Sum(team => team.ActiveMemberCount).Should().Be(1, "a participant must finish in exactly one team");
        reloaded.Teams.Single(team => team.TeamId == seeded.TeamCId).ActiveMemberCount.Should().Be(1);

        var findAssignedTeam = () => reloaded.FindTeamForExternalParticipant(seeded.ExternalIdentityId);
        findAssignedTeam.Should().NotThrow("reloading the aggregate must never surface a participant active in two teams");
        reloaded.FindTeamForExternalParticipant(seeded.ExternalIdentityId)!.TeamId.Should().Be(seeded.TeamCId);
    }

    // Phase 5 / Finding 3, the authoritative database guard in isolation from aggregate serialisation. A
    // second *Active* membership for one participant in a different team — the exact state the switch path's
    // release-then-assign ordering avoids — written straight to the table must collide with the
    // one-active-team filtered unique index. A *Removed* row for the same participant is per-stint history,
    // not a live membership, so the Active filter must let it through: that is what keeps a legitimate
    // switch/rejoin from failing with 23505.
    [Fact]
    public async Task LiveSessionTeamMembers_RejectSecondActiveMembershipForOneParticipantAcrossTeams()
    {
        var seeded = await SeedPreparingSwitchScenarioAsync();

        await using (var duplicateContext = _contextFactory.Create())
        {
            var duplicateActiveInsert = () => duplicateContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO live_session_team_members (id, team_id, session_participant_id, membership_status, joined_at)
                   VALUES ({Guid.NewGuid()}, {seeded.TeamBId}, {seeded.ParticipantId}, 'Active', {ActiveAt})");

            (await duplicateActiveInsert.Should().ThrowAsync<PostgresException>(
                    "ux_team_member_one_active_team_per_participant is the last-line guard on one active team per participant"))
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        await using (var historyContext = _contextFactory.Create())
        {
            await historyContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO live_session_team_members (id, team_id, session_participant_id, membership_status, joined_at, left_at)
                   VALUES ({Guid.NewGuid()}, {seeded.TeamBId}, {seeded.ParticipantId}, 'Removed', {ActiveAt}, {ActiveAt})");
        }
    }

    // Phase 5 / Finding 3, rejoin. Switching A -> B leaves a Removed A history row behind; rejoining A adds
    // a fresh Active row for the same participant in the same team. The participant-only Active index must
    // ignore the stale Removed row — an unfiltered index on session_participant_id would reject the rejoin
    // with 23505 — and the aggregate must still resolve to exactly one active membership.
    [Fact]
    public async Task SelectTeam_WhenParticipantRejoinsAPreviouslyLeftTeam_KeepsExactlyOneActiveMembership()
    {
        var seeded = await SeedPreparingSwitchScenarioAsync();
        var selectionPolicy = new OpenTeamSelectionPolicy();
        var noWhitelist = new HashSet<Guid>();

        await using (var context = _contextFactory.Create())
        {
            var repository = new LiveSessionRepository(context);
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            session!.SelectTeam(seeded.ExternalIdentityId, "Ada", seeded.TeamBId, noWhitelist, ActiveAt.AddSeconds(5), selectionPolicy);
            await repository.UpdateAsync(session, CancellationToken.None);
        }

        await using (var context = _contextFactory.Create())
        {
            var repository = new LiveSessionRepository(context);
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            session!.SelectTeam(seeded.ExternalIdentityId, "Ada", seeded.TeamAId, noWhitelist, ActiveAt.AddSeconds(10), selectionPolicy);
            await repository.UpdateAsync(session, CancellationToken.None);
        }

        await using var assertContext = _contextFactory.Create();
        var reloaded = await new LiveSessionRepository(assertContext)
            .GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);

        reloaded!.Teams.Sum(team => team.ActiveMemberCount).Should().Be(1, "rejoining leaves exactly one active membership");
        reloaded.FindTeamForExternalParticipant(seeded.ExternalIdentityId)!.TeamId.Should().Be(seeded.TeamAId);
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

    private async Task<SeededSession> SeedActiveTwoTargetSessionAsync()
    {
        var session = BuildTreasureHuntSession(includeSecondTarget: true);

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

    // A pre-start (Preparing) session with one team of the given capacity and no members yet: the setup for
    // two participants racing for the last slot, where the domain's own capacity check must settle it.
    private async Task<SeededSession> SeedPreparingSingleTeamSessionAsync(int capacity)
    {
        var session = BuildTreasureHuntSession();

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", capacity);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);

        await using var seedContext = _contextFactory.Create();
        await seedContext.LiveSessions.ExecuteDeleteAsync();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSession(session.LiveSessionId, session.Teams.Single().TeamId);
    }

    // A pre-start session with three attached teams and one participant already active in team A: the setup
    // for concurrent team switches, rejoin, and the one-active-team database guard.
    private async Task<SeededSwitchScenario> SeedPreparingSwitchScenarioAsync()
    {
        var session = BuildTreasureHuntSession();

        var transitionPolicy = new SessionStateTransitionPolicy();
        var teamA = session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        var teamB = session.AssociateTeam(Guid.NewGuid(), "Borealis", "BOR-01", 3);
        var teamC = session.AssociateTeam(Guid.NewGuid(), "Cascade", "CAS-01", 3);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);

        var externalIdentityId = Guid.NewGuid();
        var participant = session
            .AdmitParticipant(externalIdentityId, "Ada", teamA.TeamId, ActiveAt.AddSeconds(1), new JoinPolicy())
            .Participant;

        await using var seedContext = _contextFactory.Create();
        await seedContext.LiveSessions.ExecuteDeleteAsync();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededSwitchScenario(
            session.LiveSessionId,
            teamA.TeamId,
            teamB.TeamId,
            teamC.TeamId,
            externalIdentityId,
            participant.SessionParticipantId);
    }

    // One active target, two teams: the setup for the cross-team hard cut, where either team can clear the
    // sole target and the other's in-flight scan must lose and be rejected as SubstageAlreadyCleared.
    private async Task<SeededTwoTeamSession> SeedActiveTwoTeamSessionAsync()
    {
        var session = BuildTreasureHuntSession();

        var transitionPolicy = new SessionStateTransitionPolicy();
        var teamA = session.AssociateTeam(Guid.NewGuid(), "Aurora", "AUR-01", 3);
        var teamB = session.AssociateTeam(Guid.NewGuid(), "Borealis", "BOR-01", 3);
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, ActiveAt, transitionPolicy);

        await using var seedContext = _contextFactory.Create();
        await seedContext.LiveSessions.ExecuteDeleteAsync();
        seedContext.LiveSessions.Add(session);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return new SeededTwoTeamSession(session.LiveSessionId, teamA.TeamId, teamB.TeamId);
    }

    // Reads the aggregate root's Postgres xmin — the mapped concurrency token — so a test can assert a
    // mutation advanced it. A projection over EF.Property keeps this to the one column.
    private async Task<uint> ReadConcurrencyTokenAsync(Guid liveSessionId)
    {
        await using var context = _contextFactory.Create();
        return await context.LiveSessions
            .Where(session => session.LiveSessionId == liveSessionId)
            .Select(session => EF.Property<uint>(session, "xmin"))
            .SingleAsync(CancellationToken.None);
    }

    private static LiveSession BuildTreasureHuntSession(bool includeSecondTarget = false)
    {
        var sourceMissionId = Guid.NewGuid();
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        var targets = new List<TargetSnapshot>
        {
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
                "AfterPreviousTarget"),
        };

        if (includeSecondTarget)
        {
            // A second active target in the same substage lets two devices on one team resolve
            // *different* targets at once — the race no per-target unique index can span.
            targets.Add(TargetSnapshot.Create(
                treasureHuntSubstage.SubstageSnapshotId,
                "Target Beta",
                "QR-BETA",
                2,
                true,
                100,
                4.712,
                -74.0722,
                "Behind the fountain",
                "AfterPreviousTarget"));
        }

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
                targets,
                []));
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);

    private sealed record SeededTwoTeamSession(Guid LiveSessionId, Guid TeamAId, Guid TeamBId);

    private sealed record SeededSwitchScenario(
        Guid LiveSessionId,
        Guid TeamAId,
        Guid TeamBId,
        Guid TeamCId,
        Guid ExternalIdentityId,
        Guid ParticipantId);

    // Captures the domain events the interceptor routes to the outbox during a SaveChanges. Because the
    // interceptor enqueues inside the same transaction as the business write, only a *committed* save's
    // events survive in production; the tests therefore attach a recorder per context and assert on the
    // committing attempts, so the events observed here mirror the outbox rows that would actually be sent.
    private sealed class RecordingOutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
    {
        public List<BaseEvent> Dispatched { get; } = [];

        public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken)
        {
            Dispatched.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
