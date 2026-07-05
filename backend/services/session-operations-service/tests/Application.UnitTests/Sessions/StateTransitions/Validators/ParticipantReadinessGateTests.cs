using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Sessions.StateTransitions.Validators;

/// Pins the third transition gate, which checks a session has participants
/// before it goes live. Activating with zero associated teams is rejected;
/// activating with at least one team passes.
public sealed class ParticipantReadinessGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheckAsync_WhenActivatingWithoutTeams_ThrowsRequiresAtLeastOneTeam()
    {
        var gate = new ParticipantReadinessGate();
        var context = new SessionTransitionContext(CreateSession(withTeam: false), SessionState.Active, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<LiveSessionRequiresAtLeastOneTeamException>();
    }

    [Fact]
    public async Task CheckAsync_WhenActivatingWithTeam_DoesNotThrow()
    {
        var gate = new ParticipantReadinessGate();
        var context = new SessionTransitionContext(CreateSession(withTeam: true), SessionState.Active, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAsync_WhenTargetIsNotActive_DoesNotThrowEvenWithoutTeams()
    {
        // Only Active demands teams; Preparing with zero teams passes this gate.
        var gate = new ParticipantReadinessGate();
        var context = new SessionTransitionContext(CreateSession(withTeam: false), SessionState.Preparing, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static LiveSession CreateSession(bool withTeam)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-TEAMGATE", "Readiness Gate", 45, Now.AddHours(1));

        if (withTeam)
        {
            session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        }

        return session;
    }
}
