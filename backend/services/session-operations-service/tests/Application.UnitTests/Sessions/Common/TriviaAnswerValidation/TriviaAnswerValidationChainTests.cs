using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TriviaAnswerValidation;

// Locks the Chain of Responsibility contract for trivia-answer validation: the links run in a stable
// order and the chain short-circuits at the first rejecting link. Two layers of proof — a generic
// spy-link ordering/short-circuit proof, and a proof over the FOUR REAL links in their required order
// (runtime participation -> active question -> timer window -> duplicate team answer).
public sealed class TriviaAnswerValidationChainTests
{
    [Fact]
    public async Task ValidateAsync_RunsLinksInRegistrationOrderAndShortCircuitsOnFirstFailure()
    {
        var log = new List<string>();
        var first = new RecordingLink("first", log, throwOnCheck: true);
        var second = new RecordingLink("second", log);
        var third = new RecordingLink("third", log);
        var chain = new TriviaAnswerValidationChain(new[] { first, second, third });

        var context = CreateContext(LiveSessionTestFactory.CreateScheduledTrivia());

        var act = async () => await chain.ValidateAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // The first link ran and short-circuited; nothing after it executed.
        log.Should().ContainSingle().Which.Should().Be("first");
    }

    [Fact]
    public async Task ValidateAsync_WhenAllPass_RunsEveryLinkInOrder()
    {
        var log = new List<string>();
        var chain = new TriviaAnswerValidationChain(new[]
        {
            new RecordingLink("first", log),
            new RecordingLink("second", log),
            new RecordingLink("third", log)
        });

        await chain.ValidateAsync(CreateContext(LiveSessionTestFactory.CreateScheduledTrivia()), CancellationToken.None);

        log.Should().Equal("first", "second", "third");
    }

    // Order proof over the real links: with a context that fails EVERY gate at once, the chain must
    // surface the FIRST gate's rejection. Peeling back each earlier failure then exposes the next
    // gate, proving the exact order runtime -> active question -> timer window -> duplicate.
    [Fact]
    public async Task RealLinks_ShortCircuitAtRuntimeParticipation_BeforeAnyAnswerStateInspection()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);
        // Declares a stale/non-active question so the active-question gate would also fail if reached.
        var context = CreateContext(session, teamId, Guid.NewGuid(), questionSequenceOrder: 999);
        var chain = RealChain(runtimeAllowed: false);

        await FluentActions.Awaiting(() => chain.ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task RealLinks_WhenRuntimeAllowed_NextGateIsActiveQuestion()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);
        var context = CreateContext(session, teamId, Guid.NewGuid(), questionSequenceOrder: 999);
        var chain = RealChain(runtimeAllowed: true);

        await FluentActions.Awaiting(() => chain.ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<TriviaAnswerRequiresActiveQuestionException>();
    }

    [Fact]
    public async Task RealLinks_WhenRuntimeAndQuestionPass_NextGateIsTimerWindow()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        // Correct active question key, but submitted after the 30s window -> only the timer gate fails.
        var lateAt = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(31);
        var context = CreateContext(session, teamId, substageId, questionSequenceOrder: 1, submittedAt: lateAt);
        var chain = RealChain(runtimeAllowed: true);

        await FluentActions.Awaiting(() => chain.ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<LateTriviaAnswerException>();
    }

    [Fact]
    public async Task RealLinks_WhenRuntimeQuestionAndWindowPass_LastGateIsDuplicate()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        // Register a first accepted answer so only the duplicate gate remains to fail.
        session.RegisterTriviaAnswer(teamId, selectedOptionSequenceOrder: 1, submittedByParticipantId: null,
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));

        var inTime = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5);
        var context = CreateContext(session, teamId, substageId, questionSequenceOrder: 1, submittedAt: inTime);
        var chain = RealChain(runtimeAllowed: true);

        await FluentActions.Awaiting(() => chain.ValidateAsync(context, CancellationToken.None))
            .Should().ThrowAsync<DuplicateTriviaAnswerException>();
    }

    [Fact]
    public async Task RealLinks_WhenEveryGatePasses_Completes()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        var inTime = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5);
        var context = CreateContext(session, teamId, substageId, questionSequenceOrder: 1, submittedAt: inTime);
        var chain = RealChain(runtimeAllowed: true);

        await FluentActions.Awaiting(() => chain.ValidateAsync(context, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    // Real chain in the DI-documented order. RuntimeParticipationLink's guard is mocked to allow/deny.
    private static TriviaAnswerValidationChain RealChain(bool runtimeAllowed)
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        if (runtimeAllowed)
        {
            guard
                .Setup(g => g.EnsureAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
        else
        {
            guard
                .Setup(g => g.EnsureAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ForbiddenAccessException());
        }

        return new TriviaAnswerValidationChain(new TriviaAnswerValidationLink[]
        {
            new RuntimeParticipationLink(guard.Object),
            new ActiveTriviaQuestionLink(),
            new TriviaAnswerWindowLink(),
            new DuplicateTriviaAnswerLink()
        });
    }

    private static TriviaAnswerValidationContext CreateContext(
        LiveSession session,
        Guid? teamId = null,
        Guid? triviaSubstageSnapshotId = null,
        int questionSequenceOrder = 1,
        DateTimeOffset? submittedAt = null)
    {
        return new TriviaAnswerValidationContext(
            session,
            teamId ?? Guid.NewGuid(),
            triviaSubstageSnapshotId ?? Guid.NewGuid(),
            questionSequenceOrder,
            token: null,
            submittedAt ?? LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5));
    }

    private sealed class RecordingLink : TriviaAnswerValidationLink
    {
        private readonly string _name;
        private readonly List<string> _log;
        private readonly bool _throwOnCheck;

        public RecordingLink(string name, List<string> log, bool throwOnCheck = false)
        {
            _name = name;
            _log = log;
            _throwOnCheck = throwOnCheck;
        }

        protected override Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
        {
            _log.Add(_name);
            if (_throwOnCheck)
            {
                throw new InvalidOperationException($"link {_name} rejected");
            }

            return Task.CompletedTask;
        }
    }
}
