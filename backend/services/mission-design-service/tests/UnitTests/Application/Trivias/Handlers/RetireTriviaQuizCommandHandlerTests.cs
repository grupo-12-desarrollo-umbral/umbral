using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class RetireTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUsedTriviaQuizExists_ArchivesItForFutureUse()
    {
        var archivedAt = new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        triviaQuiz.MarkAsUsedInSession();
        repository.Seed(triviaQuiz);
        var handler = new RetireTriviaQuizCommandHandler(repository, new StubClock(archivedAt));

        var result = await handler.Handle(new RetireTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        result.Id.Should().Be(triviaQuiz.Id);
        result.Status.Should().Be("Archived");
        result.HasUsageHistory.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        triviaQuiz.Status.ToString().Should().Be("Archived");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var handler = new RetireTriviaQuizCommandHandler(
            new InMemoryTriviaQuizRepository(),
            new StubClock(new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new RetireTriviaQuizCommand(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsAlreadyArchived_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsArchived();
        repository.Seed(triviaQuiz);
        var handler = new RetireTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new RetireTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeArchivedInCurrentStateException>()
            .WithMessage("*Archived*");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizHasNoUsageHistory_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new RetireTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new RetireTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeRetiredWithoutUsageHistoryException>()
            .WithMessage("Only trivia quizzes that have already been used in a session can be retired from future use.");
    }

    private static TriviaQuiz CreatePublishableTriviaQuiz()
    {
        var triviaQuiz = TriviaQuiz.Create("Used Quiz", "Ready to retire");
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
