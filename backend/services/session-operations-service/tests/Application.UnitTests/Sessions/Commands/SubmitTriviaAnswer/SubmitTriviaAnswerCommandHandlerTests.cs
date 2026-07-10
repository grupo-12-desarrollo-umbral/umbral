using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.SubmitTriviaAnswer;

// One vertical slice: this single handler carries the accept-and-reject write. These lock the wiring
// (chain -> domain skeleton -> persist -> fact) and every rejection branch, plus the leakage gate on
// the result. Domain-invariant depth lives in the domain tests; here we prove orchestration.
public sealed class SubmitTriviaAnswerCommandHandlerTests
{
    private const string SessionCode = "tri-123";

    [Fact]
    public async Task Handle_WhenFirstInTimeAnswer_AcceptsPersistsAndAttributesToResolvedParticipant()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestionAndParticipant(
            out var teamId, out var substageId, out var participantExternalIdentityId, SessionCode);
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository, runtimeAllowed: true, atSecondsAfterActivation: 5,
            currentUserId: participantExternalIdentityId.ToString(), currentUserIdProvided: true);

        var result = await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(teamId);
        result.TriviaSubstageSnapshotId.Should().Be(substageId);
        result.QuestionSequenceOrder.Should().Be(1);
        session.TriviaAnswerSubmissions.Should().ContainSingle();
        // The resolved caller identity reaches the submission as a non-null attribution.
        var expectedParticipantId = session.Participants
            .Single(participant => participant.ExternalIdentityId == participantExternalIdentityId)
            .SessionParticipantId;
        session.TriviaAnswerSubmissions.Single().SubmittedByParticipantId.Should().Be(expectedParticipantId);
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().ContainSingle();
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Result_LeaksNoCorrectnessScoreOrSelectedOption()
    {
        var properties = typeof(umbral_backend.Application.Dtos.Sessions.SubmitTriviaAnswerResultDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        properties.Should().NotContain(new[] { "IsCorrect", "ScoreValue", "SelectedOptionSequenceOrder" });
        properties.Should().BeEquivalentTo("LiveSessionId", "TeamId", "TriviaSubstageSnapshotId", "QuestionSequenceOrder", "AnsweredAt");
    }

    [Fact]
    public async Task Handle_WhenTeamAlreadyAnswered_RejectsDuplicateAndDoesNotPersistAgain()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        session.RegisterTriviaAnswer(teamId, selectedOptionSequenceOrder: 1, submittedByParticipantId: Guid.NewGuid(),
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, runtimeAllowed: true, atSecondsAfterActivation: 6);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 2),
            CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateTriviaAnswerException>();
        session.TriviaAnswerSubmissions.Single().SelectedOptionSequenceOrder.Should().Be(1); // first write wins
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnswerLate_RejectsWithLateReasonAndRaisesNoEvent()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, runtimeAllowed: true, atSecondsAfterActivation: 31);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<LateTriviaAnswerException>();
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoQuestionActive_RejectsWithActiveQuestionReason()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        session.CloseActiveQuestion(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(2));
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, runtimeAllowed: true, atSecondsAfterActivation: 5);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaAnswerRequiresActiveQuestionException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRuntimeParticipationDenied_RejectsBeforeTouchingAnswerState()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, runtimeAllowed: false, atSecondsAfterActivation: 5);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Attribution gap (HU-34): the chain authorizes by (session, team, token), never by caller identity,
    // so an authenticated caller who is NOT a participant of this session passes every link. The handler
    // must reject before any write instead of persisting an answer attributable to nobody.
    [Fact]
    public async Task Handle_WhenAuthenticatedCallerIsNotSessionParticipant_RejectsAndDoesNotPersist()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        var repository = CreateRepository(session);
        // A valid, parseable identity that is simply not a member of this session's participants.
        var handler = CreateHandler(
            repository, runtimeAllowed: true, atSecondsAfterActivation: 5,
            currentUserId: Guid.NewGuid().ToString(), currentUserIdProvided: true);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<AnswerSubmitterIsNotSessionParticipantException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The same guard closes the absent/unparseable identity claim: no usable identity cannot resolve to
    // a participant, so the answer is rejected rather than silently attributed to NULL.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Handle_WhenCurrentUserIdIsAbsentOrUnparseable_RejectsAndDoesNotPersist(string? currentUserId)
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out var substageId, SessionCode);
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository, runtimeAllowed: true, atSecondsAfterActivation: 5,
            currentUserId: currentUserId, currentUserIdProvided: true);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(session.LiveSessionId, teamId, substageId, QuestionSequenceOrder: 1, SelectedOptionSequenceOrder: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<AnswerSubmitterIsNotSessionParticipantException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFound()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var handler = CreateHandler(repository, runtimeAllowed: true, atSecondsAfterActivation: 5);

        var act = async () => await handler.Handle(
            new SubmitTriviaAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static SubmitTriviaAnswerCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        bool runtimeAllowed,
        int atSecondsAfterActivation,
        string? currentUserId = null,
        bool currentUserIdProvided = false)
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        var setup = guard.Setup(g => g.EnsureAllowedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()));
        if (runtimeAllowed)
        {
            setup.Returns(Task.CompletedTask);
        }
        else
        {
            setup.ThrowsAsync(new ForbiddenAccessException());
        }

        var chain = new TriviaAnswerValidationChain(new TriviaAnswerValidationLink[]
        {
            new RuntimeParticipationLink(guard.Object),
            new ActiveTriviaQuestionLink(),
            new TriviaAnswerWindowLink(),
            new DuplicateTriviaAnswerLink()
        });

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id)
            .Returns(currentUserIdProvided ? currentUserId : Guid.NewGuid().ToString());

        var timeProvider = new FixedTimeProvider(
            LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(atSecondsAfterActivation));

        return new SubmitTriviaAnswerCommandHandler(repository.Object, chain, currentUser.Object, timeProvider);
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
