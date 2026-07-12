using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class AddTriviaQuestionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTriviaQuizExists_AddsQuestionAndReturnsUpdatedDetail()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        repository.Seed(triviaQuiz);
        var handler = new AddTriviaQuestionCommandHandler(repository);

        var result = await handler.Handle(
            new AddTriviaQuestionCommand(
                triviaQuiz.Id,
                "What is the capital of France?",
                100,
                30,
                "Paris is the capital city.",
                true,
                [
                    new TriviaOptionInput("Paris", 1, true),
                    new TriviaOptionInput("Berlin", 2, false)
                ]),
            CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        result.Questions.Should().ContainSingle();
        result.Questions[0].Prompt.Should().Be("What is the capital of France?");
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(30);
        result.Questions[0].Explanation.Should().Be("Paris is the capital city.");
        result.Questions[0].Options.Should().ContainSingle(option => option.IsCorrect);
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new AddTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new AddTriviaQuestionCommand(
                99,
                "Question?",
                100,
                30,
                null,
                true,
                [
                    new TriviaOptionInput("A", 1, true),
                    new TriviaOptionInput("B", 2, false)
                ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsNotEditable_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        triviaQuiz.AddQuestion("Question?", 100, 30, null, [TriviaOption.Create("A", 1, true), TriviaOption.Create("B", 2, false)]);
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new AddTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new AddTriviaQuestionCommand(
                triviaQuiz.Id,
                "Question?",
                100,
                30,
                null,
                true,
                [
                    new TriviaOptionInput("A", 1, true),
                    new TriviaOptionInput("B", 2, false)
                ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizNotEditableException>()
            .WithMessage("*cannot be edited*");
    }
}
