using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using InMemoryMissionRepository =
    umbral_backend.Application.UnitTests.Application.Missions.TestDoubles.InMemoryMissionRepository;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class ArchiveTriviaQuizCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTriviaQuizCanBeArchived_ArchivesTriviaQuiz()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new ArchiveTriviaQuizCommandHandler(
            repository,
            new InMemoryMissionRepository(),
            new StubClock(Now));

        var result = await handler.Handle(new ArchiveTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        triviaQuiz.Status.ToString().Should().Be("Archived");
        triviaQuiz.IsSourceReady.Should().BeFalse();
        result.Status.Should().Be("Archived");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new ArchiveTriviaQuizCommandHandler(
            repository,
            new InMemoryMissionRepository(),
            new StubClock(Now));

        var act = () => handler.Handle(new ArchiveTriviaQuizCommand(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsAlreadyArchived_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Archived Quiz", "Already withdrawn");
        triviaQuiz.MarkAsArchived();
        repository.Seed(triviaQuiz);
        var handler = new ArchiveTriviaQuizCommandHandler(
            repository,
            new InMemoryMissionRepository(),
            new StubClock(Now));

        var act = () => handler.Handle(new ArchiveTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeArchivedInCurrentStateException>()
            .WithMessage("*Archived*");
    }

    [Fact]
    public async Task Handle_WhenReferencedByActiveMission_BlocksArchivalAndLeavesQuizPublished()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var missionRepository = new StubActiveMissionReferenceRepository(
            new ActiveMissionReference(7, "Forest Hunt"));
        var handler = new ArchiveTriviaQuizCommandHandler(
            repository,
            missionRepository,
            new StubClock(Now));

        var act = () => handler.Handle(new ArchiveTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<TriviaQuizReferencedByActiveMissionException>();
        assertion.Which.TriviaQuizId.Should().Be(triviaQuiz.Id);
        assertion.Which.Message.Should().Contain("Forest Hunt");
        // The transition never ran: the quiz stays published and is not persisted.
        triviaQuiz.Status.ToString().Should().Be("Published");
        repository.LastUpdatedTriviaQuiz.Should().BeNull();
        missionRepository.QueriedTriviaQuizId.Should().Be(triviaQuiz.Id);
    }

    private static TriviaQuiz CreatePublishableTriviaQuiz()
    {
        var triviaQuiz = TriviaQuiz.Create("Published Quiz", "Ready to archive");
        triviaQuiz.AddQuestion(
            "Which planet is known as the red planet?",
            100,
            30,
            "Solar system baseline",
            [
                TriviaOption.Create("Mars", 1, true),
                TriviaOption.Create("Venus", 2, false)
            ]);

        return triviaQuiz;
    }

    private sealed class StubActiveMissionReferenceRepository : IMissionRepository
    {
        private readonly IReadOnlyList<ActiveMissionReference> _references;

        public StubActiveMissionReferenceRepository(params ActiveMissionReference[] references)
        {
            _references = references;
        }

        public int? QueriedTriviaQuizId { get; private set; }

        public Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
            int triviaQuizId,
            CancellationToken cancellationToken)
        {
            QueriedTriviaQuizId = triviaQuizId;
            return Task.FromResult(_references);
        }

        public Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddAsync(Mission mission, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
