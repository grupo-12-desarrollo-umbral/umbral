using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.StateTransitions.Validators;

/// Pins the first transition gate, which checks whether the target state is
/// reachable from the session's current state. Allowed edges pass through; an
/// unreachable edge is rejected with the invalid-transition reason.
public sealed class CurrentStateGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheckAsync_WithAllowedEdge_DoesNotThrow()
    {
        var gate = new CurrentStateGate(new SessionStateTransitionPolicy());
        var context = new SessionTransitionContext(CreateScheduledSession(), SessionState.Preparing, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAsync_WithStructurallyUnreachableEdge_ThrowsInvalidSessionStateTransition()
    {
        var gate = new CurrentStateGate(new SessionStateTransitionPolicy());
        // Scheduled -> Active is not a canonical edge (must pass through Preparing).
        var context = new SessionTransitionContext(CreateScheduledSession(), SessionState.Active, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSessionStateTransitionException>();
    }

    private static Domain.Entities.LiveSession CreateScheduledSession()
        => LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-CURSTATE", "Current State Gate", 45, Now.AddHours(1));
}
