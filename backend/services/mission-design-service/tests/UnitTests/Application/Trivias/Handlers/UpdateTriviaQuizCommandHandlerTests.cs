using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class UpdateTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizExists_UpdatesTriviaQuizAndReturnsDetail()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        repository.Seed(triviaQuiz);
        var handler = new UpdateTriviaQuizCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateTriviaQuizCommand(
                triviaQuiz.Id,
                "Updated Quiz",
                "Updated Description",
                [
                    new TriviaQuestionInput(
                        "Updated question?",
                        1,
                        true,
                        [
                            new TriviaOptionInput("Correct", 1, true),
                            new TriviaOptionInput("Incorrect", 2, false)
                        ])
                ]),
            CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        result.Id.Should().Be(triviaQuiz.Id);
        result.Title.Should().Be("Updated Quiz");
        result.Description.Should().Be("Updated Description");
        result.Status.Should().Be("Draft");
        result.Questions.Should().ContainSingle();
        result.Questions[0].Options.Should().ContainSingle(option => option.IsCorrect);
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new UpdateTriviaQuizCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateTriviaQuizCommand(99, "Quiz", "Description", []),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsNotEditable_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new UpdateTriviaQuizCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateTriviaQuizCommand(triviaQuiz.Id, "Updated Quiz", "Updated Description", []),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizNotEditableException>()
            .WithMessage("*cannot be edited*");
    }
}
