using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class EvidenceIntakeFacadeTests
{
    [Fact]
    public async Task RegisterAsync_OrchestratesChainThenConcreteValidationThenDomainCoreThenPersistence()
    {
        var log = new List<string>();
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Callback(() => log.Add("persist"))
            .Returns(Task.CompletedTask);
        var chain = new EvidenceIntakeValidationChain(new[] { new RecordingLink(log) });
        var facade = new EvidenceIntakeFacade(chain, repository.Object);
        var submittedAt = LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5);

        var submission = await facade.RegisterAsync(
            new EvidenceIntakeValidationContext(session, teamId, substageId, token: null, submittedAt),
            _ =>
            {
                log.Add("concrete-validation");
                return Task.CompletedTask;
            },
            aggregate =>
            {
                log.Add("domain-core");
                return aggregate.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), submittedAt);
            },
            CancellationToken.None);

        log.Should().Equal("generic-chain", "concrete-validation", "domain-core", "persist");
        submission.Should().BeSameAs(session.TriviaAnswerSubmissions.Single());
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        repository.Verify(x => x.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenGenericChainRejects_DoesNotRunConcreteFormOrPersist()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId);
        var repository = new Mock<ILiveSessionRepository>();
        var chain = new EvidenceIntakeValidationChain(new[] { new RecordingLink([], reject: true) });
        var facade = new EvidenceIntakeFacade(chain, repository.Object);
        var concreteValidationRan = false;
        var domainRegistrationRan = false;

        var act = () => facade.RegisterAsync<TriviaAnswerSubmission>(
            new EvidenceIntakeValidationContext(
                session, teamId, substageId, token: null,
                LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5)),
            _ =>
            {
                concreteValidationRan = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                domainRegistrationRan = true;
                return null!;
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        concreteValidationRan.Should().BeFalse();
        domainRegistrationRan.Should().BeFalse();
        repository.Verify(x => x.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class RecordingLink : EvidenceIntakeValidationLink
    {
        private readonly List<string> _log;
        private readonly bool _reject;

        public RecordingLink(List<string> log, bool reject = false)
        {
            _log = log;
            _reject = reject;
        }

        protected override Task CheckAsync(
            EvidenceIntakeValidationContext context,
            CancellationToken cancellationToken)
        {
            _log.Add("generic-chain");
            return _reject
                ? Task.FromException(new InvalidOperationException("rejected"))
                : Task.CompletedTask;
        }
    }
}
