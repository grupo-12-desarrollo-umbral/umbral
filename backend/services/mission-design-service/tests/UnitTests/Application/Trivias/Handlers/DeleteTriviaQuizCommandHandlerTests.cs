using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class DeleteTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizHasNoUsageHistory_RemovesIt()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Draft Quiz", "Still removable");
        repository.Seed(triviaQuiz);
        var handler = new DeleteTriviaQuizCommandHandler(repository);

        await handler.Handle(new DeleteTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        repository.LastRemovedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        var storedQuiz = await repository.GetByIdAsync(triviaQuiz.Id, CancellationToken.None);
        storedQuiz.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var handler = new DeleteTriviaQuizCommandHandler(new InMemoryTriviaQuizRepository());

        var act = () => handler.Handle(new DeleteTriviaQuizCommand(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizWasUsed_RejectsDestructiveRemoval()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Used Quiz", "Must not be deleted");
        triviaQuiz.MarkAsUsedInSession();
        repository.Seed(triviaQuiz);
        var handler = new DeleteTriviaQuizCommandHandler(repository);

        var act = () => handler.Handle(new DeleteTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeDestructivelyRemovedAfterUsageException>()
            .WithMessage("A trivia quiz that has already been used in a session cannot be removed destructively.");
        repository.LastRemovedTriviaQuiz.Should().BeNull();
    }
}
