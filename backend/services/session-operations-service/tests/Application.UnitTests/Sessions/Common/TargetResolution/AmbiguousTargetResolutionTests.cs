using System.Reflection;
using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Application.Sessions.Common.TargetResolution.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TargetResolution;

/// <summary>
/// Regression: resolving a scan that matches two targets used to throw InvalidOperationException
/// (a 500 mid-game). It must now produce a defined rejection, and must never silently pick a winner.
/// <para>
/// <see cref="MissionRuntimeSnapshot.Create"/> rejects duplicate QR codes, so a snapshot built today
/// cannot reach the resolver ambiguous. EF materialization is the gap: it runs the parameterless
/// constructor and populates the backing collection directly, so a row persisted before that guard
/// existed (or written by hand) loads with duplicates intact. These tests reproduce that materialized
/// shape — see <see cref="ActiveDuplicateQrSession"/>.
/// </para>
/// </summary>
public sealed class AmbiguousTargetResolutionTests
{
    [Fact]
    public void Context_WhenTwoTargetsShareScannedCode_IsAmbiguousAndResolvesToNoTarget()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var context = Context(session, teamId, "QR-DUP");

        context.MatchedTargetCount.Should().Be(2);
        context.IsAmbiguous.Should().BeTrue();
        context.ResolvedTarget.Should().BeNull();
    }

    [Fact]
    public void Context_WhenTwoTargetsShareScannedCode_DoesNotThrow()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var act = () => Context(session, teamId, "QR-DUP");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task RealLinks_AmbiguousScan_ReturnsMultipleTargetsReason()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, "QR-DUP"), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets);
    }

    // The ambiguity link must precede the existence link: an ambiguous scan resolves to no target, so
    // the wrong order would report it as an unknown QR code.
    [Fact]
    public async Task RealLinks_AmbiguousScan_IsNotReportedAsUnknownCode()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, "QR-DUP"), CancellationToken.None);

        reason.Should().NotBe(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
    }

    [Fact]
    public async Task RealLinks_UnambiguousTargetInDuplicateBearingMission_StillAccepts()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, " qr-unique "), CancellationToken.None);

        reason.Should().BeNull();
    }

    [Fact]
    public async Task RealLinks_UnknownScanInDuplicateBearingMission_StillReportsUnknownCode()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, "nope"), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
    }

    // Matching mirrors the resolver's trimmed, case-insensitive comparison, so a differently-cased
    // scan of a duplicated code is ambiguous too.
    [Fact]
    public async Task RealLinks_AmbiguousScanIgnoringCase_ReturnsMultipleTargetsReason()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, " qr-dup "), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets);
    }

    [Fact]
    public void RegisterTargetScan_WhenScanIsAmbiguous_RejectsInsteadOfThrowing()
    {
        var session = ActiveDuplicateQrSession(out var teamId);

        var submission = session.RegisterTargetScan(teamId, "QR-DUP", Guid.NewGuid(), DateTimeOffset.UtcNow);

        submission.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        submission.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets);
        submission.TargetSnapshotId.Should().BeNull("an ambiguous scan must never bind to one of the duplicates");
    }

    private static TargetResolutionChain RealChain() => new(new TargetResolutionLink[]
    {
        new ScannedValueResolvesToSingleTargetLink(),
        new TargetExistsForScanLink(),
        new TargetBelongsToActiveSubstageLink(),
        new TargetNotAlreadyResolvedLink()
    });

    private static TargetResolutionContext Context(LiveSession session, Guid teamId, string scan) =>
        new(session, teamId, session.ActiveSubstageId!.Value, scan);

    // A treasure-hunt session whose snapshot holds two targets sharing "QR-DUP" plus one distinct
    // "QR-UNIQUE". Create() would reject that outright, so the duplicate is appended to the backing
    // collection afterwards — reproducing how EF rehydrates a row persisted before the guard existed,
    // which is the only way a duplicate now reaches the resolver.
    private static LiveSession ActiveDuplicateQrSession(out Guid teamId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Duplicated Hunt",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [
                Target(substage, "First Statue", "QR-DUP", 1),
                Target(substage, "Fountain", "QR-UNIQUE", 3)
            ],
            []);

        AppendMaterializedTarget(snapshot, Target(substage, "Second Statue", "QR-DUP", 2));

        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "dup123",
            "Duplicated Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
            snapshot);

        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow, policy);
        teamId = team.TeamId;
        return session;
    }

    // Writes straight to the private backing collection, exactly as EF does when it rehydrates a
    // snapshot: no constructor invariant runs, so the duplicate survives into memory.
    private static void AppendMaterializedTarget(MissionRuntimeSnapshot snapshot, TargetSnapshot target)
    {
        var field = typeof(MissionRuntimeSnapshot)
            .GetField("_targetSnapshots", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "MissionRuntimeSnapshot._targetSnapshots not found; update this EF-materialization stand-in.");

        ((List<TargetSnapshot>)field.GetValue(snapshot)!).Add(target);
    }

    private static TargetSnapshot Target(SubstageSnapshot substage, string name, string qrCode, int sequenceOrder) =>
        TargetSnapshot.Create(
            substage.SubstageSnapshotId,
            name,
            qrCode,
            sequenceOrder,
            isActive: true,
            score: 100,
            latitude: 4.711,
            longitude: -74.0721,
            clueText: null,
            clueVisibilityPolicy: null);
}
