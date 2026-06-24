using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Trivias.Handlers;

public sealed class UpdateTriviaQuestionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenQuestionExists_UpdatesQuestionAndReturnsUpdatedDetail()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        var question = triviaQuiz.AddQuestion(
            "Original question?",
            1,
            50,
            20,
            "Original explanation",
            [
                TriviaOption.Create("Correct", 1, true),
                TriviaOption.Create("Incorrect", 2, false)
            ]);
        repository.Seed(triviaQuiz);
        var handler = new UpdateTriviaQuestionCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateTriviaQuestionCommand(
                triviaQuiz.Id,
                question.Id,
                "Updated question?",
                1,
                100,
                45,
                "Updated explanation",
                false,
                [
                    new TriviaOptionInput("Updated correct", 1, true),
                    new TriviaOptionInput("Updated incorrect", 2, false)
                ]),
            CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        result.Questions.Should().ContainSingle();
        result.Questions[0].Id.Should().Be(question.Id);
        result.Questions[0].Prompt.Should().Be("Updated question?");
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(45);
        result.Questions[0].Explanation.Should().Be("Updated explanation");
        result.Questions[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new UpdateTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateTriviaQuestionCommand(
                99,
                7,
                "Question?",
                1,
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
    public async Task Handle_WhenQuestionDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        repository.Seed(triviaQuiz);
        var handler = new UpdateTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateTriviaQuestionCommand(
                triviaQuiz.Id,
                999,
                "Question?",
                1,
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
            .WithMessage("Entity \"TriviaQuestion\" (999) was not found.");
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsNotEditable_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Original Quiz", "Original Description");
        var question = triviaQuiz.AddQuestion(
            "Original question?",
            1,
            50,
            20,
            null,
            [
                TriviaOption.Create("Correct", 1, true),
                TriviaOption.Create("Incorrect", 2, false)
            ]);
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new UpdateTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateTriviaQuestionCommand(
                triviaQuiz.Id,
                question.Id,
                "Updated question?",
                1,
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
