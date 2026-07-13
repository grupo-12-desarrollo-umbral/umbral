using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Sessions.Common.EvidenceIntakeValidation;

public sealed class EvidenceIntakeValidationChainTests
{
    [Fact]
    public async Task ValidateAsync_RunsLinksInRegistrationOrder()
    {
        var log = new List<string>();
        var chain = new EvidenceIntakeValidationChain(new EvidenceIntakeValidationLink[]
        {
            new RecordingLink("runtime", log),
            new RecordingLink("session", log),
            new RecordingLink("substage", log)
        });

        await chain.ValidateAsync(Context(ActiveSession(out var teamId, out var substageId), teamId, substageId), CancellationToken.None);

        log.Should().Equal("runtime", "session", "substage");
    }

    [Fact]
    public async Task ValidateAsync_WhenLinkRejects_ShortCircuitsRemainingLinks()
    {
        var log = new List<string>();
        var chain = new EvidenceIntakeValidationChain(new EvidenceIntakeValidationLink[]
        {
            new RecordingLink("runtime", log),
            new RecordingLink("session", log, reject: true),
            new RecordingLink("substage", log)
        });

        var session = ActiveSession(out var teamId, out var substageId);
        var act = () => chain.ValidateAsync(Context(session, teamId, substageId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        log.Should().Equal("runtime", "session");
    }

    [Fact]
    public async Task RealLinks_RuntimeParticipationRejectsBeforeSessionAndSubstageChecks()
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        guard.Setup(x => x.EnsureAllowedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());
        var session = LiveSessionTestFactory.CreateScheduledTrivia();

        var act = () => RealChain(guard.Object).ValidateAsync(
            Context(session, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task RealLinks_WhenRuntimePasses_SessionAdmissionIsNext()
    {
        var guard = AllowingGuard();
        var session = LiveSessionTestFactory.CreateScheduledTrivia();

        var act = () => RealChain(guard.Object).ValidateAsync(
            Context(session, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaAnswerRequiresActiveSessionException>();
    }

    [Fact]
    public async Task ActiveSubstagePresentLink_WhenPointerAbsent_RejectsContext()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();

        var act = () => new ActiveSubstagePresentLink().ValidateAsync(
            Context(session, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<EvidenceSubmissionContextRequiredException>();
    }

    [Fact]
    public async Task RealLinks_WhenEveryAdmissionCheckPasses_Completes()
    {
        var session = ActiveSession(out var teamId, out var substageId);

        var act = () => RealChain(AllowingGuard().Object).ValidateAsync(
            Context(session, teamId, substageId), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static EvidenceIntakeValidationChain RealChain(IRuntimeParticipationGuard guard) =>
        new(new EvidenceIntakeValidationLink[]
        {
            new RuntimeParticipationLink(guard),
            new SessionAdmitsReceptionLink(),
            new ActiveSubstagePresentLink()
        });

    private static Mock<IRuntimeParticipationGuard> AllowingGuard()
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        guard.Setup(x => x.EnsureAllowedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return guard;
    }

    private static LiveSession ActiveSession(out Guid teamId, out Guid substageId) =>
        LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out teamId, out substageId);

    private static EvidenceIntakeValidationContext Context(
        LiveSession session,
        Guid teamId,
        Guid substageId) =>
        new(session, teamId, substageId, token: null, LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5));

    private sealed class RecordingLink : EvidenceIntakeValidationLink
    {
        private readonly string _name;
        private readonly List<string> _log;
        private readonly bool _reject;

        public RecordingLink(string name, List<string> log, bool reject = false)
        {
            _name = name;
            _log = log;
            _reject = reject;
        }

        protected override Task CheckAsync(
            EvidenceIntakeValidationContext context,
            CancellationToken cancellationToken)
        {
            _log.Add(_name);
            return _reject
                ? Task.FromException(new InvalidOperationException($"{_name} rejected"))
                : Task.CompletedTask;
        }
    }
}
