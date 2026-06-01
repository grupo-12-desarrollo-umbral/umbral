using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class CreateTriviaQuizCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesTriviaQuizAndReturnsDetail()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new CreateTriviaQuizCommandHandler(repository);
        var command = new CreateTriviaQuizCommand(
            "Intro Quiz",
            "Warm-up trivia",
            [
                new TriviaQuestionInput(
                    "First question?",
                    1,
                    true,
                    [
                        new TriviaOptionInput("A", 1, true),
                        new TriviaOptionInput("B", 2, false)
                    ])
            ]);

        var result = await handler.Handle(command, CancellationToken.None);

        repository.LastAddedTriviaQuiz.Should().NotBeNull();
        repository.LastAddedTriviaQuiz!.Id.Should().Be(result.Id);
        result.Title.Should().Be("Intro Quiz");
        result.Description.Should().Be("Warm-up trivia");
        result.Status.Should().Be("Draft");
        result.Questions.Should().ContainSingle();
        result.Questions[0].Prompt.Should().Be("First question?");
        result.Questions[0].Options.Should().ContainSingle(option => option.IsCorrect);
    }
}
