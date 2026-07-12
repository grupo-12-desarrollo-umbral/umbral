using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Trivias.Commands.RemoveTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
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

public sealed class RemoveTriviaQuestionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenQuestionExists_RemovesQuestionAndReturnsUpdatedDetail()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Quiz", "Description");
        var question = triviaQuiz.AddQuestion(
            "Question to remove?",
            50,
            20,
            null,
            [
                TriviaOption.Create("Correct", 1, true),
                TriviaOption.Create("Incorrect", 2, false)
            ]);
        question.Id = 10;
        repository.Seed(triviaQuiz);
        var handler = new RemoveTriviaQuestionCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveTriviaQuestionCommand(triviaQuiz.Id, 10),
            CancellationToken.None);

        repository.LastUpdatedTriviaQuiz.Should().BeSameAs(triviaQuiz);
        result.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMultipleQuestionsExist_RemovesTargetAndReconcilesOrder()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Quiz", "Description");
        var firstQuestion = triviaQuiz.AddQuestion(
            "First?",
            50,
            20,
            null,
            [
                TriviaOption.Create("A", 1, true),
                TriviaOption.Create("B", 2, false)
            ]);
        var secondQuestion = triviaQuiz.AddQuestion(
            "Second?",
            50,
            20,
            null,
            [
                TriviaOption.Create("C", 1, true),
                TriviaOption.Create("D", 2, false)
            ]);
        var thirdQuestion = triviaQuiz.AddQuestion(
            "Third?",
            50,
            20,
            null,
            [
                TriviaOption.Create("E", 1, true),
                TriviaOption.Create("F", 2, false)
            ]);
        firstQuestion.Id = 10;
        secondQuestion.Id = 20;
        thirdQuestion.Id = 30;
        repository.Seed(triviaQuiz);
        var handler = new RemoveTriviaQuestionCommandHandler(repository);

        var result = await handler.Handle(
            new RemoveTriviaQuestionCommand(triviaQuiz.Id, 20),
            CancellationToken.None);

        result.Questions.Should().HaveCount(2);
        result.Questions[0].Id.Should().Be(firstQuestion.Id);
        result.Questions[1].Id.Should().Be(thirdQuestion.Id);
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var handler = new RemoveTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new RemoveTriviaQuestionCommand(99, 7),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"TriviaQuiz\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenQuestionDoesNotExist_ThrowsDomainException()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Quiz", "Description");
        var existingQuestion = triviaQuiz.AddQuestion(
            "Existing question?",
            50,
            20,
            null,
            [
                TriviaOption.Create("A", 1, true),
                TriviaOption.Create("B", 2, false)
            ]);
        existingQuestion.Id = 10;
        repository.Seed(triviaQuiz);
        var handler = new RemoveTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new RemoveTriviaQuestionCommand(triviaQuiz.Id, 999),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuestionNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTriviaQuizIsNotEditable_PropagatesDomainRejection()
    {
        var repository = new InMemoryTriviaQuizRepository();
        var triviaQuiz = TriviaQuiz.Create("Quiz", "Description");
        var question = triviaQuiz.AddQuestion(
            "Question?",
            50,
            20,
            null,
            [
                TriviaOption.Create("A", 1, true),
                TriviaOption.Create("B", 2, false)
            ]);
        question.Id = 10;
        triviaQuiz.MarkAsPublished();
        repository.Seed(triviaQuiz);
        var handler = new RemoveTriviaQuestionCommandHandler(repository);

        var act = () => handler.Handle(
            new RemoveTriviaQuestionCommand(triviaQuiz.Id, 10),
            CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizNotEditableException>()
            .WithMessage("*cannot be edited*");
    }
}
