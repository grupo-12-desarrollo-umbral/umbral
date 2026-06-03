using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class DuplicateTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizExists_CreatesAuthoringCopyWithLineage()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new DuplicateTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new DuplicateTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        repository.LastAddedTriviaQuiz.Should().NotBeNull();
        repository.LastAddedTriviaQuiz.Should().NotBeSameAs(triviaQuiz);
        result.Id.Should().Be(repository.LastAddedTriviaQuiz!.Id);
        result.Status.Should().Be("Draft");
        result.SourceTriviaQuizId.Should().Be(triviaQuiz.Id);
        result.IsDuplicate.Should().BeTrue();
        result.HasUsageHistory.Should().BeFalse();
        result.Questions.Should().HaveCount(triviaQuiz.Questions.Count);
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var handler = new DuplicateTriviaQuizCommandHandler(
            new InMemoryTriviaQuizRepository(),
            new StubClock(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new DuplicateTriviaQuizCommand(42), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (42) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsArchived_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsArchived();
        repository.Seed(triviaQuiz);
        var handler = new DuplicateTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new DuplicateTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeArchivedInCurrentStateException>()
            .WithMessage("*Archived*");
    }

    private static TriviaQuiz CreatePublishableTriviaQuiz()
    {
        var triviaQuiz = TriviaQuiz.Create("Source Quiz", "Reusable source");
        triviaQuiz.AddQuestion(
            "Question?",
            1,
            100,
            30,
            "Baseline explanation",
            [
                TriviaOption.Create("Correct", 1, true),
                TriviaOption.Create("Incorrect", 2, false)
            ]);

        return triviaQuiz;
    }
}
