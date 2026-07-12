using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class GetTriviaDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizExists_ReturnsTriviaDetail()
    {
        var triviaQuiz = new TriviaQuizDto(
            7,
            "Quiz",
            "Warm-up trivia",
            "Draft",
            [
                new TriviaQuestionDto(
                    3,
                    "Question?",
                    true,
                    [
                        new TriviaOptionDto(8, "Correct", 1, true),
                        new TriviaOptionDto(9, "Incorrect", 2, false)
                    ],
                    100,
                    30,
                    "Because it is Paris.")
            ]);

        var repository = new InMemoryTriviaQuizReadModelRepository(
            details: new Dictionary<int, TriviaQuizDto> { [triviaQuiz.Id] = triviaQuiz });
        var handler = new GetTriviaDetailQueryHandler(repository);

        var result = await handler.Handle(new GetTriviaDetailQuery(triviaQuiz.Id), CancellationToken.None);

        result.Should().Be(triviaQuiz);
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(30);
        result.Questions[0].Explanation.Should().Be("Because it is Paris.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var handler = new GetTriviaDetailQueryHandler(new InMemoryTriviaQuizReadModelRepository());

        var act = () => handler.Handle(new GetTriviaDetailQuery(42), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (42) was not found.");
    }
}
