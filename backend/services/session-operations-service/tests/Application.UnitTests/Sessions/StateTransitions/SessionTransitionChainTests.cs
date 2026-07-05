using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.StateTransitions;

public sealed class SessionTransitionChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    // Verifies the session transition validator chain. The three gates
    // (current-state, operator-assigned, participant-readiness) run in a fixed
    // order and the chain stops at the first one that rejects — so a later gate
    // never runs once an earlier one has failed.

    [Fact]
    public async Task ValidateAsync_WithRealGates_PassesAllThreeInOrder_WhenTransitionIsFullyValid()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-ORDER", "Order", 45, Now.AddHours(1));
        session.AssignOperator(42, Now.AddMinutes(-5));
        var chain = RealChain();

        var act = async () => await chain.ValidateAsync(
            new SessionTransitionContext(session, SessionState.Preparing, reason: null),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_WhenCurrentStateGateFails_ShortCircuitsBeforeOperatorGate()
    {
        // Both gate 1 and gate 2 would fail; getting gate 1's rejection proves gate 2 never ran.
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-SC1", "ShortCircuit1", 45, Now.AddHours(1));
        var chain = RealChain();

        var act = async () => await chain.ValidateAsync(
            new SessionTransitionContext(session, SessionState.Active, reason: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSessionStateTransitionException>();
    }

    [Fact]
    public async Task ValidateAsync_WhenOperatorGateFails_ShortCircuitsBeforeParticipantGate()
    {
        // Both gate 2 and gate 3 would fail; getting gate 2's rejection proves gate 3 never ran.
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt("SES-SC2", "ShortCircuit2", 45, Now.AddHours(1));
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-5), new SessionStateTransitionPolicy());
        var chain = RealChain();

        var act = async () => await chain.ValidateAsync(
            new SessionTransitionContext(session, SessionState.Active, reason: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionOperatorNotAssignedException>();
    }

    private static SessionTransitionChain RealChain()
        => new(new SessionTransitionValidator[]
        {
            new CurrentStateGate(new SessionStateTransitionPolicy()),
            new OperatorAssignmentGate(),
            new ParticipantReadinessGate()
        });

    [Fact]
    public async Task ValidateAsync_RunsValidatorsInRegistrationOrder()
    {
        var log = new List<string>();
        var chain = new SessionTransitionChain(new SessionTransitionValidator[]
        {
            new RecordingValidator(log, "first"),
            new RecordingValidator(log, "second"),
            new RecordingValidator(log, "third")
        });

        await chain.ValidateAsync(CreateContext(), CancellationToken.None);

        log.Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task ValidateAsync_ShortCircuitsOnFirstFailingValidator()
    {
        var log = new List<string>();
        var chain = new SessionTransitionChain(new SessionTransitionValidator[]
        {
            new RecordingValidator(log, "first"),
            new RecordingValidator(log, "second", shouldThrow: true),
            new RecordingValidator(log, "third")
        });

        var act = async () => await chain.ValidateAsync(CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("second");
        log.Should().Equal("first", "second");
    }

    [Fact]
    public async Task ValidateAsync_WithNoValidators_DoesNothing()
    {
        var chain = new SessionTransitionChain(Array.Empty<SessionTransitionValidator>());

        var act = async () => await chain.ValidateAsync(CreateContext(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static SessionTransitionContext CreateContext()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(
            "SES-0001",
            "Chain Session",
            30,
            DateTimeOffset.UtcNow);

        return new SessionTransitionContext(session, SessionState.Preparing, reason: null);
    }

    private sealed class RecordingValidator : SessionTransitionValidator
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly bool _shouldThrow;

        public RecordingValidator(List<string> log, string name, bool shouldThrow = false)
        {
            _log = log;
            _name = name;
            _shouldThrow = shouldThrow;
        }

        protected override Task CheckAsync(SessionTransitionContext context, CancellationToken cancellationToken)
        {
            _log.Add(_name);

            if (_shouldThrow)
            {
                throw new InvalidOperationException(_name);
            }

            return Task.CompletedTask;
        }
    }
}
