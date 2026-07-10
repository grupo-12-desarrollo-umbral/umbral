using umbral_backend.Api.Services;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// Pure unit coverage for the SignalR connection bookkeeping: replacing a reused connection id and the
// multi-connection reference-count paths (last connection removed vs. one of several).
public sealed class ConnectionTrackerTests
{
    [Fact]
    public void Add_ReusedConnectionId_ReplacesPreviousParticipant()
    {
        var tracker = new ConnectionTracker();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        tracker.Add("conn-1", Guid.NewGuid(), first);

        // Same connection id, different participant → the previous participant's count is decremented.
        tracker.Add("conn-1", Guid.NewGuid(), second);

        tracker.TryRemove("conn-1", out var participant, out var hasRemaining).Should().BeTrue();
        participant!.SessionParticipantId.Should().Be(second);
        hasRemaining.Should().BeFalse();
    }

    [Fact]
    public void TryRemove_UnknownConnection_ReturnsFalse()
    {
        var tracker = new ConnectionTracker();

        tracker.TryRemove("missing", out var participant, out var hasRemaining).Should().BeFalse();
        participant.Should().BeNull();
        hasRemaining.Should().BeFalse();
    }

    [Fact]
    public void TryRemove_ParticipantWithMultipleConnections_ReportsRemaining()
    {
        var tracker = new ConnectionTracker();
        var participantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        tracker.Add("conn-1", sessionId, participantId);
        tracker.Add("conn-2", sessionId, participantId);

        tracker.TryRemove("conn-1", out _, out var hasRemaining).Should().BeTrue();
        hasRemaining.Should().BeTrue();   // conn-2 still open → remaining count > 0

        tracker.TryRemove("conn-2", out _, out hasRemaining).Should().BeTrue();
        hasRemaining.Should().BeFalse();  // last connection → count drops to 0
    }
}
