using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Application.Trivias.Handlers;
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
                    1,
                    true,
                    [
                        new TriviaOptionDto(8, "Correct", 1, true),
                        new TriviaOptionDto(9, "Incorrect", 2, false)
                    ])
            ]);

        var repository = new InMemoryTriviaQuizReadModelRepository(
            details: new Dictionary<int, TriviaQuizDto> { [triviaQuiz.Id] = triviaQuiz });
        var handler = new GetTriviaDetailQueryHandler(repository);

        var result = await handler.Handle(new GetTriviaDetailQuery(triviaQuiz.Id), CancellationToken.None);

        result.Should().Be(triviaQuiz);
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
