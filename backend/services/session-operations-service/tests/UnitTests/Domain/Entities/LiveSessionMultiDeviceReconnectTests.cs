using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// HU-08 X.1 (verify + lock): the multi-device / reconnect contract is emergent from HU-07B —
// LiveSession.AdmitParticipant routes a returning identity through SessionParticipant.RefreshPresence
// (idempotent, never ParticipantAlreadyConnectedException) and JoinPolicy.EnsureCanReconnect. These
// tests lock that contract end-to-end at the aggregate boundary. No product code is changed.
public sealed class LiveSessionMultiDeviceReconnectTests
{
    private static readonly DateTimeOffset JoinedAt = new(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly JoinPolicy _joinPolicy = new();

    // ── (a) second AdmitParticipant for the same identity is idempotent ──────────────────────────────

    // Gate (a): a 2nd AdmitParticipant for the same externalIdentityId returns the SAME participant with
    // IsReconnect=true and Active status — the reconnect / multi-device branch.
    [Fact]
    public void AdmitParticipant_SecondAdmitForSameIdentity_IsIdempotentReconnect()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();

        var first = session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt, _joinPolicy);
        var second = session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt.AddMinutes(1), _joinPolicy);

        second.IsReconnect.Should().BeTrue();
        second.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        second.Participant.SessionParticipantId.Should().Be(first.Participant.SessionParticipantId);
        second.Team.TeamId.Should().Be(team.TeamId);
        session.Participants.Should().ContainSingle("a returning identity never creates a second participant");
    }

    // Gate (a) — the load-bearing multi-device assertion: a 2nd device connecting while the participant
    // is STILL Active must NOT throw ParticipantAlreadyConnectedException. That first-join guard lives
    // on MarkActive; the reconnect path uses RefreshPresence, which is idempotent from Active.
    [Fact]
    public void AdmitParticipant_SecondDeviceWhileStillActive_DoesNotThrowAlreadyConnected()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt, _joinPolicy);

        var act = () => session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt.AddSeconds(30), _joinPolicy);

        act.Should().NotThrow<ParticipantAlreadyConnectedException>();
        session.Participants.Single().ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    // Gate (a): reconnect after a disconnect restores presence to Active and refreshes the heartbeat
    // (LastSeenAt), the operational marker for multi-device visibility.
    [Fact]
    public void AdmitParticipant_ReconnectAfterDisconnect_RestoresActiveAndRefreshesLastSeenAt()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        var admitted = session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt, _joinPolicy);
        session.DisconnectParticipant(admitted.Participant.SessionParticipantId, JoinedAt.AddMinutes(1));

        var reconnectAt = JoinedAt.AddMinutes(2);
        var reconnected = session.AdmitParticipant(identityId, "Nora", team.TeamId, reconnectAt, _joinPolicy);

        reconnected.IsReconnect.Should().BeTrue();
        reconnected.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        reconnected.Participant.LastSeenAt.Should().Be(reconnectAt);
    }

    // ── (b) reconnect to a different team → AC4 rejection ────────────────────────────────────────────

    // Gate (b, AC4): a returning identity requesting a DIFFERENT team than the one it is assigned to is
    // rejected with ParticipantAssignedToDifferentTeamException — team isolation on reconnect.
    [Fact]
    public void AdmitParticipant_ReconnectRequestingDifferentTeam_ThrowsAssignedToDifferentTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        var identityId = Guid.NewGuid();
        session.AdmitParticipant(identityId, "Nora", alpha.TeamId, JoinedAt, _joinPolicy);

        var act = () => session.AdmitParticipant(identityId, "Nora", bravo.TeamId, JoinedAt.AddMinutes(1), _joinPolicy);

        act.Should().Throw<ParticipantAssignedToDifferentTeamException>();
    }

    // ── (c) reconnect while Removed / Finished / Cancelled → AC3 rejection ────────────────────────────

    // Gate (c): reconnect while the participant is Removed is rejected with
    // ParticipantRemovedFromSessionException (JoinPolicy.EnsureCanReconnect guards IsRemoved first).
    [Fact]
    public void AdmitParticipant_ReconnectWhileParticipantRemoved_ThrowsParticipantRemoved()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt, _joinPolicy);
        session.Participants.Single().Remove(JoinedAt.AddMinutes(1));

        var act = () => session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt.AddMinutes(2), _joinPolicy);

        act.Should().Throw<ParticipantRemovedFromSessionException>();
    }

    // Gate (c): reconnect while the session is Finished or Cancelled is rejected with
    // LateJoinNotAllowedException — no late reconnect into a closed session.
    [Theory]
    [InlineData(SessionState.Finished)]
    [InlineData(SessionState.Cancelled)]
    public void AdmitParticipant_ReconnectWhileSessionClosed_ThrowsLateJoinNotAllowed(SessionState closedState)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt, _joinPolicy);
        DriveToClosedState(session, closedState);

        var act = () => session.AdmitParticipant(identityId, "Nora", team.TeamId, JoinedAt.AddMinutes(10), _joinPolicy);

        act.Should().Throw<LateJoinNotAllowedException>();
    }

    private static void DriveToClosedState(LiveSession session, SessionState closedState)
    {
        var policy = new SessionStateTransitionPolicy();

        if (closedState == SessionState.Cancelled)
        {
            session.MoveTo(SessionState.Cancelled, JoinedAt.AddMinutes(1), policy);
            return;
        }

        session.MoveTo(SessionState.Preparing, JoinedAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, JoinedAt.AddMinutes(2), policy);
        session.MoveTo(SessionState.Finished, JoinedAt.AddMinutes(3), policy);
    }
}
