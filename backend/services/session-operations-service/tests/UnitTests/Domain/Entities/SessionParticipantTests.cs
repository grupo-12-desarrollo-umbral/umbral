using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class SessionParticipantTests
{
    [Fact]
    public void Join_WithMissingIdentity_ThrowsException()
    {
        var act = () => SessionParticipant.Join(Guid.NewGuid(), Guid.Empty, "Nora", DateTimeOffset.UtcNow);

        act.Should().Throw<ParticipantIdentityRequiredException>();
    }

    [Fact]
    public void Disconnect_MarksParticipantDisconnected()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.MarkActive(DateTimeOffset.UtcNow.AddSeconds(1));

        participant.Disconnect(DateTimeOffset.UtcNow.AddMinutes(1));

        participant.ParticipantStatus.Should().Be(ParticipantStatus.Disconnected);
    }

    [Fact]
    public void Join_WithBlankDisplayName_ThrowsException()
    {
        var act = () => SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), " ", DateTimeOffset.UtcNow);

        act.Should().Throw<ParticipantDisplayNameRequiredException>();
    }

    [Fact]
    public void Block_MarksBlockedAndReportsTransition()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.MarkActive(DateTimeOffset.UtcNow.AddSeconds(1));

        var transitioned = participant.Block(DateTimeOffset.UtcNow.AddMinutes(1));

        transitioned.Should().BeTrue();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Blocked);
        participant.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public void Block_WhenAlreadyBlocked_IsNoOp()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Block(DateTimeOffset.UtcNow.AddSeconds(1));

        participant.Block(DateTimeOffset.UtcNow.AddSeconds(2)).Should().BeFalse();
    }

    [Fact]
    public void Block_WhenRemoved_StaysRemoved()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        participant.Block(DateTimeOffset.UtcNow.AddMinutes(2)).Should().BeFalse();
        participant.ParticipantStatus.Should().Be(ParticipantStatus.Removed);
    }

    [Fact]
    public void RefreshPresence_WhenBlocked_RecoversToActive()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Block(DateTimeOffset.UtcNow.AddSeconds(1));

        participant.RefreshPresence(DateTimeOffset.UtcNow.AddMinutes(1));

        participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    [Fact]
    public void MarkActive_WhenAlreadyActive_ThrowsException()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.MarkActive(DateTimeOffset.UtcNow.AddSeconds(1));

        var act = () => participant.MarkActive(DateTimeOffset.UtcNow.AddSeconds(2));

        act.Should().Throw<ParticipantAlreadyConnectedException>();
    }

    [Fact]
    public void Disconnect_WhenRemoved_ThrowsException()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        var act = () => participant.Disconnect(DateTimeOffset.UtcNow.AddMinutes(2));

        act.Should().Throw<ParticipantRemovedFromSessionException>();
    }

    [Fact]
    public void RefreshPresence_WhenRemoved_ThrowsException()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        var act = () => participant.RefreshPresence(DateTimeOffset.UtcNow.AddMinutes(2));

        act.Should().Throw<ParticipantRemovedFromSessionException>();
    }

    [Fact]
    public void MarkActive_WhenRemoved_ThrowsException()
    {
        var participant = SessionParticipant.Join(Guid.NewGuid(), Guid.NewGuid(), "Nora", DateTimeOffset.UtcNow);
        participant.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        var act = () => participant.MarkActive(DateTimeOffset.UtcNow.AddMinutes(2));

        act.Should().Throw<ParticipantRemovedFromSessionException>();
    }
}
