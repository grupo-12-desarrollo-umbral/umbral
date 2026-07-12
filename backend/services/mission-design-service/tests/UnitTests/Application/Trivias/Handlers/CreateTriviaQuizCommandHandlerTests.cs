using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
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
                    true,
                    [
                        new TriviaOptionInput("A", 1, true),
                        new TriviaOptionInput("B", 2, false)
                    ],
                    100,
                    30,
                    "Geography baseline")
            ]);

        var result = await handler.Handle(command, CancellationToken.None);

        repository.LastAddedTriviaQuiz.Should().NotBeNull();
        repository.LastAddedTriviaQuiz!.Id.Should().Be(result.Id);
        result.Title.Should().Be("Intro Quiz");
        result.Description.Should().Be("Warm-up trivia");
        result.Status.Should().Be("Draft");
        result.Questions.Should().ContainSingle();
        result.Questions[0].Prompt.Should().Be("First question?");
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(30);
        result.Questions[0].Explanation.Should().Be("Geography baseline");
        result.Questions[0].Options.Should().ContainSingle(option => option.IsCorrect);
    }
}
