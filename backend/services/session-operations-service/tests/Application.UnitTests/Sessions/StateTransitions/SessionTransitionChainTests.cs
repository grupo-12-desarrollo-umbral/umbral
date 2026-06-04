using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.StateTransitions;

public sealed class SessionTransitionChainTests
{
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
        var session = LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
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
