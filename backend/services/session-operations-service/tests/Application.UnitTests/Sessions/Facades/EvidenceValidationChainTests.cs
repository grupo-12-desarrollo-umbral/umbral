using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common.EvidenceValidation;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class EvidenceValidationChainTests
{
    [Fact]
    public async Task ValidateAsync_RunsLinksInRegistrationOrder()
    {
        var log = new List<string>();
        var chain = new EvidenceValidationChain(new EvidenceValidationLink[]
        {
            new RecordingLink("binding", log),
            new RecordingLink("window", log),
            new RecordingLink("origin", log)
        });

        await chain.ValidateAsync(CreateContext(), CancellationToken.None);

        log.Should().Equal("binding", "window", "origin");
    }

    [Fact]
    public async Task ValidateAsync_WhenChainHasNoLinks_CompletesWithoutError()
    {
        // An empty chain has a null head, so ValidateAsync must fall through to Task.CompletedTask
        // rather than dereferencing the head — this is the shared EvidenceIntakeFacade's default
        // (contextless) construction path.
        var chain = new EvidenceValidationChain(Array.Empty<EvidenceValidationLink>());

        var act = () => chain.ValidateAsync(CreateContext(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_WhenLinkRejects_ShortCircuitsRemainingLinks()
    {
        var log = new List<string>();
        var chain = new EvidenceValidationChain(new EvidenceValidationLink[]
        {
            new RecordingLink("binding", log, EvidenceRejectionReason.SubstageBindingMismatch),
            new RecordingLink("window", log),
            new RecordingLink("origin", log)
        });

        var act = () => chain.ValidateAsync(CreateContext(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvidenceContextRejectedException>();
        exception.Which.Reason.Should().Be(EvidenceRejectionReason.SubstageBindingMismatch);
        log.Should().Equal("binding");
    }

    private static EvidenceValidationContext CreateContext()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        var submittedAt = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5);
        var submission = new TestEvidenceSubmission(
            session.LiveSessionId,
            teamId,
            substageId,
            submittedAt,
            EvidenceSubmissionType.TriviaAnswer);

        return new EvidenceValidationContext(
            submission,
            session,
            teamId,
            substageId,
            origin: null,
            submittedAt);
    }

    private sealed class RecordingLink : EvidenceValidationLink
    {
        private readonly string _name;
        private readonly List<string> _log;
        private readonly EvidenceRejectionReason? _rejectionReason;

        internal RecordingLink(
            string name,
            List<string> log,
            EvidenceRejectionReason? rejectionReason = null)
        {
            _name = name;
            _log = log;
            _rejectionReason = rejectionReason;
        }

        protected override Task CheckAsync(
            EvidenceValidationContext context,
            CancellationToken cancellationToken)
        {
            _log.Add(_name);
            return _rejectionReason.HasValue
                ? Task.FromException(new EvidenceContextRejectedException(_rejectionReason.Value))
                : Task.CompletedTask;
        }
    }
}
