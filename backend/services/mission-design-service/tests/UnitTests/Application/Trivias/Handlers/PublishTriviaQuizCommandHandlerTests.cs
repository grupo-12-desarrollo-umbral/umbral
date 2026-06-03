using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class PublishTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizIsPublishable_PublishesTriviaQuiz()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = CreatePublishableTriviaQuiz();
        repository.Seed(triviaQuiz);
        var publishedAt = new DateTimeOffset(2026, 6, 2, 15, 0, 0, TimeSpan.Zero);
        var handler = new PublishTriviaQuizCommandHandler(repository, new StubClock(publishedAt));

        var result = await handler.Handle(new PublishTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        triviaQuiz.Status.ToString().Should().Be("Published");
        triviaQuiz.PublishedAt.Should().Be(publishedAt);
        triviaQuiz.IsSourceReady.Should().BeTrue();
        result.Status.Should().Be("Published");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new PublishTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 2, 15, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new PublishTriviaQuizCommand(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsNotReadyForPublication_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Incomplete Quiz", "Missing questions");
        repository.Seed(triviaQuiz);
        var handler = new PublishTriviaQuizCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 6, 2, 15, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new PublishTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizMustHaveAtLeastOneQuestionToPublishException>();
    }

    private static TriviaQuiz CreatePublishableTriviaQuiz()
    {
        var triviaQuiz = TriviaQuiz.Create("Capitals", "Country capitals quiz");
        triviaQuiz.AddQuestion(
            "What is the capital of France?",
            1,
            100,
            30,
            "European capitals",
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Lyon", 2, false)
            ]);

        return triviaQuiz;
    }
}
