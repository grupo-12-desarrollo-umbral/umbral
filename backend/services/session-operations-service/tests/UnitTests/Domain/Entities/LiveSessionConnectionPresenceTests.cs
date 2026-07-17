using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Concurrency Finding 5 (reconnect/disconnect overlap): presence is decrement-guarded on a
// per-connection lease keyed by ConnectionId. These aggregate-level tests lock that a participant is
// marked Disconnected only when its LAST socket drops, that a duplicate disconnect callback is a
// no-op, and that an old socket dropping after a fresh socket registered never disconnects an
// actively connected participant.
public sealed class LiveSessionConnectionPresenceTests
{
    private static readonly DateTimeOffset JoinedAt = new(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly JoinPolicy _joinPolicy = new();

    [Fact]
    public void DropConnection_WhileAnotherConnectionRemains_KeepsParticipantActive()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");
        session.RegisterParticipantConnection(participantId, "conn-B", JoinedAt.AddSeconds(5));

        session.DisconnectParticipantConnection(participantId, "conn-A", JoinedAt.AddSeconds(10));

        var participant = session.Participants.Single();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        participant.ActiveConnectionCount.Should().Be(1);
        participant.Connections.Single().ConnectionId.Should().Be("conn-B");
    }

    [Fact]
    public void DropConnection_OfLastConnection_MarksParticipantDisconnected()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");

        var disconnectedAt = JoinedAt.AddSeconds(10);
        session.DisconnectParticipantConnection(participantId, "conn-A", disconnectedAt);

        var participant = session.Participants.Single();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Disconnected);
        participant.ActiveConnectionCount.Should().Be(0);
        participant.LastSeenAt.Should().Be(disconnectedAt);
    }

    [Fact]
    public void DropConnection_DuplicateCallbackForSameConnection_IsIdempotent()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");
        session.DisconnectParticipantConnection(participantId, "conn-A", JoinedAt.AddSeconds(10));

        // A second (duplicate/late) disconnect callback for the already-removed socket must not throw
        // and must not move presence again.
        var act = () => session.DisconnectParticipantConnection(participantId, "conn-A", JoinedAt.AddSeconds(20));

        act.Should().NotThrow();
        var participant = session.Participants.Single();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Disconnected);
        participant.LastSeenAt.Should().Be(JoinedAt.AddSeconds(10));
    }

    // The load-bearing overlap case: an old socket's disconnect callback fires AFTER a fresh socket
    // for the same participant has already registered. The old socket removing only its own lease must
    // leave the participant Active — never recorded as disconnected while genuinely connected.
    [Fact]
    public void DropConnection_OldSocketAfterFreshSocketRegistered_LeavesParticipantActive()
    {
        var (session, participantId) = AdmitParticipantWithConnection("old-socket");

        // Fresh socket reconnects (registers its own ConnectionId), then the old socket's delayed
        // disconnect callback arrives.
        session.RegisterParticipantConnection(participantId, "new-socket", JoinedAt.AddSeconds(3));
        session.DisconnectParticipantConnection(participantId, "old-socket", JoinedAt.AddSeconds(4));

        var participant = session.Participants.Single();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        participant.Connections.Single().ConnectionId.Should().Be("new-socket");
    }

    [Fact]
    public void RegisterConnection_SameConnectionTwice_DoesNotDoubleCount()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");

        // A retried admission (e.g. after an xmin conflict) re-registers the same ConnectionId.
        session.RegisterParticipantConnection(participantId, "conn-A", JoinedAt.AddSeconds(5));

        session.Participants.Single().ActiveConnectionCount.Should().Be(1);
    }

    [Fact]
    public void RegisterConnection_AfterDisconnect_RestoresActive()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");
        session.DisconnectParticipantConnection(participantId, "conn-A", JoinedAt.AddSeconds(5));
        session.Participants.Single().IsDisconnected.Should().BeTrue();

        session.RegisterParticipantConnection(participantId, "conn-B", JoinedAt.AddSeconds(10));

        var participant = session.Participants.Single();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        participant.ActiveConnectionCount.Should().Be(1);
    }

    [Fact]
    public void HardDisconnect_ClearsEveryHeldConnection()
    {
        var (session, participantId) = AdmitParticipantWithConnection("conn-A");
        session.RegisterParticipantConnection(participantId, "conn-B", JoinedAt.AddSeconds(2));

        session.DisconnectParticipant(participantId, JoinedAt.AddSeconds(5));

        var participant = session.Participants.Single();
        participant.IsDisconnected.Should().BeTrue();
        participant.ActiveConnectionCount.Should().Be(0);
    }

    private (LiveSession Session, Guid ParticipantId) AdmitParticipantWithConnection(string connectionId)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var participantId = session
            .AdmitParticipant(Guid.NewGuid(), "Nora", team.TeamId, JoinedAt, _joinPolicy)
            .Participant.SessionParticipantId;
        session.RegisterParticipantConnection(participantId, connectionId, JoinedAt);
        return (session, participantId);
    }
}
