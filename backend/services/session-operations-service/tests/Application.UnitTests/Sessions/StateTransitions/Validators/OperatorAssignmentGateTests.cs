using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.StateTransitions.Validators;

/// Pins the second transition gate, which guards going live. Moving to
/// Preparing or Active requires an assigned operator, so those transitions are
/// rejected when none is assigned and pass once one is; targets that don't go
/// live (e.g. Cancelled) are unaffected.
public sealed class OperatorAssignmentGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Active)]
    public async Task CheckAsync_WhenGoingLiveWithoutOperator_ThrowsSessionOperatorNotAssigned(SessionState target)
    {
        var gate = new OperatorAssignmentGate();
        var context = new SessionTransitionContext(CreateSession(assignOperator: false), target, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<SessionOperatorNotAssignedException>();
    }

    [Theory]
    [InlineData(SessionState.Preparing)]
    [InlineData(SessionState.Active)]
    public async Task CheckAsync_WhenGoingLiveWithOperator_DoesNotThrow(SessionState target)
    {
        var gate = new OperatorAssignmentGate();
        var context = new SessionTransitionContext(CreateSession(assignOperator: true), target, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckAsync_WhenTargetDoesNotRequireOperator_DoesNotThrowEvenWithoutOperator()
    {
        // Cancelled never requires an operator, so an unassigned session passes this gate.
        var gate = new OperatorAssignmentGate();
        var context = new SessionTransitionContext(CreateSession(assignOperator: false), SessionState.Cancelled, reason: null);

        var act = async () => await gate.ValidateAsync(context, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static LiveSession CreateSession(bool assignOperator)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-OPGATE", "Operator Gate", 45, Now.AddHours(1));

        if (assignOperator)
        {
            session.AssignOperator(42, Now.AddMinutes(-5));
        }

        return session;
    }
}
