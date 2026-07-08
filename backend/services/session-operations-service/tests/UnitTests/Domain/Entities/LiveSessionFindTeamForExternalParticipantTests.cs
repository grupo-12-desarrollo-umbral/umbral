using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// Lobby read accessor (#108): maps an ExternalIdentityId to the attached team the participant is
// actively a member of, or null when they hold no active membership.
public sealed class LiveSessionFindTeamForExternalParticipantTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FindTeamForExternalParticipant_ReturnsActiveTeam_OrNullWhenAbsent()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var red = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, new HashSet<Guid>(), At, new OpenTeamSelectionPolicy());

        session.FindTeamForExternalParticipant(identity)!.TeamId.Should().Be(red.TeamId);
        session.FindTeamForExternalParticipant(Guid.NewGuid()).Should().BeNull();
    }
}
