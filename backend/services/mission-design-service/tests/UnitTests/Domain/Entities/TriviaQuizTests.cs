using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

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
}
