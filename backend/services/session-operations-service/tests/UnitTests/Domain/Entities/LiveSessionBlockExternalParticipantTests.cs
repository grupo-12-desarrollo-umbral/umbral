using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Participation Block (#91): mark the external participant blocked on a denied runtime re-check.
// Returns the participant only on the transition (persist/evict once); null if absent or re-blocked.
public sealed class LiveSessionBlockExternalParticipantTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BlockExternalParticipant_BlocksOnce_ThenReturnsNull()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var red = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, new HashSet<Guid>(), At, new OpenTeamSelectionPolicy());

        var blocked = session.BlockExternalParticipant(identity, At.AddMinutes(1));

        blocked.Should().NotBeNull();
        blocked!.ParticipantStatus.Should().Be(ParticipantStatus.Blocked);
        session.BlockExternalParticipant(identity, At.AddMinutes(2)).Should().BeNull();
    }

    [Fact]
    public void BlockExternalParticipant_WhenAbsent_ReturnsNull()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);

        session.BlockExternalParticipant(Guid.NewGuid(), At).Should().BeNull();
    }
}
