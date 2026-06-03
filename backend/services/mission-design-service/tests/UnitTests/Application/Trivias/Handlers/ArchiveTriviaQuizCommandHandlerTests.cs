using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class ArchiveTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizCanBeArchived_ArchivesTriviaQuiz()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new ArchiveTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)));

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
            new StubClock(new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)));

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
            new StubClock(new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new ArchiveTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeArchivedInCurrentStateException>()
            .WithMessage("*Archived*");
    }

    private static TriviaQuiz CreatePublishableTriviaQuiz()
    {
        var triviaQuiz = TriviaQuiz.Create("Published Quiz", "Ready to archive");
        triviaQuiz.AddQuestion(
            "Which planet is known as the red planet?",
            1,
            100,
            30,
            "Solar system baseline",
            [
                TriviaOption.Create("Mars", 1, true),
                TriviaOption.Create("Venus", 2, false)
            ]);

        return triviaQuiz;
    }
}
