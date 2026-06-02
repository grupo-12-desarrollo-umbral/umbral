using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class TriviaQuizTests
{
    [Fact]
    public void Create_SetsDraftBaselineRaisesCreatedEventAndKeepsQuestionShape()
    {
        var questions = new[]
        {
            TriviaQuestion.Create(
                "Question 1",
                1,
                [
                    TriviaOption.Create("Option A", 1, true),
                    TriviaOption.Create("Option B", 2, false)
                ])
        };

        var quiz = TriviaQuiz.Create(" Intro Quiz ", " Warm-up trivia ", questions);

        quiz.Title.Should().Be("Intro Quiz");
        quiz.Description.Should().Be("Warm-up trivia");
        quiz.Status.Should().Be(TriviaQuizStatus.Draft);
        quiz.Questions.Should().ContainSingle().Which.Should().BeSameAs(questions[0]);
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizCreatedEvent);
    }

    [Fact]
    public void UpdateDetails_WhenQuizIsDraft_UsesSharedWorkflowAndRaisesUpdatedEvent()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        var replacementQuestions = new[]
        {
            TriviaQuestion.Create("Question 2", 1)
        };

        quiz.ClearDomainEvents();

        quiz.UpdateDetails(" Updated Quiz ", " Refined summary ", replacementQuestions);

        quiz.Title.Should().Be("Updated Quiz");
        quiz.Description.Should().Be("Refined summary");
        quiz.Status.Should().Be(TriviaQuizStatus.Draft);
        quiz.Questions.Should().ContainSingle().Which.Should().BeSameAs(replacementQuestions[0]);
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_WhenQuizIsPublished_ThrowsNotEditableException()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.MarkAsPublished();

        var act = () => quiz.UpdateDetails("Updated Quiz", "Refined summary");

        act.Should().Throw<TriviaQuizNotEditableException>();
    }

    [Fact]
    public void UpdateDetails_WhenQuizIsArchived_ThrowsNotEditableException()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.MarkAsArchived();

        var act = () => quiz.UpdateDetails("Updated Quiz", "Refined summary");

        act.Should().Throw<TriviaQuizNotEditableException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenTitleIsInvalid_Throws(string? title)
    {
        var act = () => TriviaQuiz.Create(title, "Warm-up trivia");

        act.Should().Throw<TriviaQuizTitleRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenDescriptionIsInvalid_Throws(string? description)
    {
        var act = () => TriviaQuiz.Create("Intro Quiz", description);

        act.Should().Throw<TriviaQuizDescriptionRequiredException>();
    }

    [Fact]
    public void AddQuestion_WhenQuizIsDraft_UsesStableWorkflowAndRaisesQuestionAddedEvent()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");

        var question = quiz.AddQuestion(
            " Capital of France? ",
            1,
            100,
            45,
            " Geography baseline ",
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);

        question.Prompt.Should().Be("Capital of France?");
        question.ScoreValue.Should().Be(100);
        question.TimeLimit.Should().Be(QuestionTimer.Create(45));
        question.Explanation.Should().Be("Geography baseline");
        question.Options.Should().ContainSingle(option => option.IsCorrect);
        quiz.Questions.Should().ContainSingle().Which.Should().BeSameAs(question);
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuestionAddedEvent);
        var addedEvent = quiz.DomainEvents.OfType<TriviaQuestionAddedEvent>().Single();
        addedEvent.TriviaQuestion.Should().BeSameAs(question);
    }

    [Fact]
    public void UpdateQuestion_WhenQuizIsDraft_UsesStableWorkflowAndRaisesQuestionUpdatedEvent()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        var question = quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);

        question.Id = 27;
        quiz.ClearDomainEvents();

        var updated = quiz.UpdateQuestion(
            27,
            " Capital of Germany? ",
            2,
            100,
            60,
            " Updated explanation ",
            [
                TriviaOption.Create("Paris", 1, false),
                TriviaOption.Create("Berlin", 2, true)
            ],
            isActive: false);

        updated.Should().BeSameAs(question);
        updated.Prompt.Should().Be("Capital of Germany?");
        updated.SequenceOrder.Should().Be(2);
        updated.ScoreValue.Should().Be(100);
        updated.TimeLimit.Should().Be(QuestionTimer.Create(60));
        updated.Explanation.Should().Be("Updated explanation");
        updated.IsActive.Should().BeFalse();
        updated.Options.Should().ContainSingle(option => option.IsCorrect && option.OptionText == "Berlin");
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuestionUpdatedEvent);
        var changedEvent = quiz.DomainEvents.OfType<TriviaQuestionUpdatedEvent>().Single();
        changedEvent.TriviaQuestion.Should().BeSameAs(question);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AddQuestion_WhenOptionCountIsOutsideAcceptedBounds_Throws(int optionCount)
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        var options = Enumerable.Range(1, optionCount)
            .Select(index => TriviaOption.Create($"Option {index}", index, index == 1))
            .ToArray();

        var act = () => quiz.AddQuestion("Prompt", 1, 10, 30, null, options);

        act.Should().Throw<TriviaQuestionMustHaveBetweenTwoAndFourOptionsException>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(9, 9)]
    public void AddQuestion_WhenCorrectOptionCountIsNotExactlyOne_Throws(int firstCorrectIndex, int secondCorrectIndex)
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        var options = new[]
        {
            TriviaOption.Create("Option A", 1, firstCorrectIndex == 1 || secondCorrectIndex == 1),
            TriviaOption.Create("Option B", 2, firstCorrectIndex == 2 || secondCorrectIndex == 2)
        };

        var act = () => quiz.AddQuestion("Prompt", 1, 10, 30, null, options);

        act.Should().Throw<TriviaQuestionMustHaveExactlyOneCorrectOptionException>();
    }

    [Fact]
    public void UpdateQuestion_WhenQuestionDoesNotExist_Throws()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");

        var act = () => quiz.UpdateQuestion(
            999,
            "Prompt",
            1,
            10,
            30,
            null,
            [
                TriviaOption.Create("Option A", 1, true),
                TriviaOption.Create("Option B", 2, false)
            ]);

        act.Should().Throw<TriviaQuestionNotFoundException>();
    }
}
