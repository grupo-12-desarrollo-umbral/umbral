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
        quiz.PublishedAt.Should().BeNull();
        quiz.IsSourceReady.Should().BeFalse();
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
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        quiz.Publish(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var act = () => quiz.UpdateDetails("Updated Quiz", "Refined summary");

        act.Should().Throw<TriviaQuizNotEditableException>();
    }

    [Fact]
    public void UpdateDetails_WhenQuizIsArchived_ThrowsNotEditableException()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.Archive(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var act = () => quiz.UpdateDetails("Updated Quiz", "Refined summary");

        act.Should().Throw<TriviaQuizNotEditableException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenTitleIsInvalid_Throws(string? title)
    {
        var act = () => TriviaQuiz.Create(title!, "Warm-up trivia");

        act.Should().Throw<TriviaQuizTitleRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenDescriptionIsInvalid_Throws(string? description)
    {
        var act = () => TriviaQuiz.Create("Intro Quiz", description!);

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

    [Fact]
    public void Publish_WhenQuizIsPublishable_UsesLifecycleWorkflowAndRaisesPublishedEvent()
    {
        var publishedAt = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        quiz.ClearDomainEvents();

        quiz.Publish(publishedAt);

        quiz.Status.Should().Be(TriviaQuizStatus.Published);
        quiz.PublishedAt.Should().Be(publishedAt);
        quiz.IsSourceReady.Should().BeTrue();
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizPublishedEvent);
    }

    [Fact]
    public void Publish_WhenQuizHasNoQuestions_Throws()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");

        var act = () => quiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuizMustHaveAtLeastOneQuestionToPublishException>();
    }

    [Fact]
    public void Publish_WhenQuestionHasNoScoreValue_Throws()
    {
        var quiz = TriviaQuiz.Create(
            "Intro Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Question 1",
                    1,
                    [
                        TriviaOption.Create("Option A", 1, true),
                        TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        var act = () => quiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuestionScoreValueRequiredToPublishException>();
    }

    [Fact]
    public void Publish_WhenQuestionHasNoTimeLimit_Throws()
    {
        var quiz = TriviaQuiz.Create(
            "Intro Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Question 1",
                    1,
                    100,
                    null,
                    null,
                    [
                        TriviaOption.Create("Option A", 1, true),
                        TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        var act = () => quiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuestionTimeLimitRequiredToPublishException>();
    }

    [Theory]
    [InlineData(TriviaQuizStatus.Published)]
    [InlineData(TriviaQuizStatus.Archived)]
    public void Publish_WhenStateDoesNotAllowTransition_Throws(TriviaQuizStatus targetState)
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);

        if (targetState == TriviaQuizStatus.Published)
        {
            quiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));
        }
        else
        {
            quiz.Archive(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));
        }

        var act = () => quiz.Publish(new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuizCannotBePublishedInCurrentStateException>();
    }

    [Fact]
    public void Archive_FromDraft_UsesLifecycleWorkflowAndRaisesArchivedEvent()
    {
        var archivedAt = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.ClearDomainEvents();

        quiz.Archive(archivedAt);

        quiz.Status.Should().Be(TriviaQuizStatus.Archived);
        quiz.IsSourceReady.Should().BeFalse();
        quiz.PublishedAt.Should().BeNull();
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizArchivedEvent);
    }

    [Fact]
    public void Archive_FromPublished_PreservesPublicationHistoryAndRaisesArchivedEvent()
    {
        var publishedAt = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);
        var archivedAt = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        quiz.Publish(publishedAt);
        quiz.ClearDomainEvents();

        quiz.Archive(archivedAt);

        quiz.Status.Should().Be(TriviaQuizStatus.Archived);
        quiz.PublishedAt.Should().Be(publishedAt);
        quiz.IsSourceReady.Should().BeFalse();
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizArchivedEvent);
    }

    [Fact]
    public void Archive_WhenQuizIsAlreadyArchived_Throws()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.Archive(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        var act = () => quiz.Archive(new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuizCannotBeArchivedInCurrentStateException>();
    }

    [Fact]
    public void Duplicate_CreatesSeparateDraftCopyPreservesLineageAndRaisesEvents()
    {
        var sourceQuiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        sourceQuiz.Id = 41;
        sourceQuiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            "Geography baseline",
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        sourceQuiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));
        sourceQuiz.MarkAsUsedInSession();
        sourceQuiz.ClearDomainEvents();

        var duplicate = sourceQuiz.Duplicate();

        duplicate.Should().NotBeSameAs(sourceQuiz);
        duplicate.Title.Should().Be(sourceQuiz.Title);
        duplicate.Description.Should().Be(sourceQuiz.Description);
        duplicate.Status.Should().Be(TriviaQuizStatus.Draft);
        duplicate.PublishedAt.Should().BeNull();
        duplicate.SourceTriviaQuizId.Should().Be(sourceQuiz.Id);
        duplicate.IsDuplicate.Should().BeTrue();
        duplicate.HasUsageHistory.Should().BeFalse();
        duplicate.IsSourceReady.Should().BeFalse();
        duplicate.Questions.Should().ContainSingle();
        duplicate.Questions.Single().Should().NotBeSameAs(sourceQuiz.Questions.Single());
        duplicate.Questions.Single().Options.Should().HaveCount(2);
        duplicate.Questions.Single().Options.First().Should().NotBeSameAs(sourceQuiz.Questions.Single().Options.First());
        duplicate.DomainEvents.Should().ContainSingle(e => e is TriviaQuizCreatedEvent);
        sourceQuiz.Status.Should().Be(TriviaQuizStatus.Published);
        sourceQuiz.PublishedAt.Should().Be(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));
        sourceQuiz.HasUsageHistory.Should().BeTrue();
        sourceQuiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizDuplicatedEvent);
    }

    [Fact]
    public void Duplicate_WhenQuestionHasNoTimeLimit_ClonesNullTimeLimit()
    {
        // A directly-assembled question with a null time limit exercises the CloneQuestion
        // null-conditional (TimeLimit?.Seconds), which the publishable-quiz path never reaches.
        var sourceQuiz = TriviaQuiz.Create(
            "Draft Quiz",
            "Warm-up trivia",
            [
                TriviaQuestion.Create(
                    "Capital of France?",
                    1,
                    100,
                    null,
                    null,
                    [
                        TriviaOption.Create("Paris", 1, true),
                        TriviaOption.Create("Berlin", 2, false)
                    ])
            ]);
        sourceQuiz.Id = 55;

        var duplicate = sourceQuiz.Duplicate();

        duplicate.Questions.Should().ContainSingle();
        duplicate.Questions.Single().TimeLimit.Should().BeNull();
        duplicate.SourceTriviaQuizId.Should().Be(55);
    }

    [Fact]
    public void Duplicate_WhenSourceIsItselfAnUnpersistedDuplicate_CarriesForwardOriginalLineage()
    {
        // A duplicate that was never persisted has Id 0, so ResolveDuplicateLineageSourceId must
        // fall back to the original source id instead of the (zero) transient id.
        var original = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        original.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        original.Id = 41;
        original.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        var firstCopy = original.Duplicate();
        firstCopy.Id.Should().Be(default);
        firstCopy.SourceTriviaQuizId.Should().Be(41);

        var secondCopy = firstCopy.Duplicate();

        secondCopy.SourceTriviaQuizId.Should().Be(41);
    }

    [Fact]
    public void RetireFromFutureUse_WhenQuizHasUsageHistory_ArchivesQuizThroughSharedWorkflow()
    {
        var publishedAt = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);
        var archivedAt = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        quiz.Publish(publishedAt);
        quiz.MarkAsUsedInSession();
        quiz.ClearDomainEvents();

        quiz.RetireFromFutureUse(archivedAt);

        quiz.Status.Should().Be(TriviaQuizStatus.Archived);
        quiz.PublishedAt.Should().Be(publishedAt);
        quiz.HasUsageHistory.Should().BeTrue();
        quiz.DomainEvents.Should().ContainSingle(e => e is TriviaQuizArchivedEvent);
    }

    [Fact]
    public void RetireFromFutureUse_WhenQuizHasNoUsageHistory_Throws()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.AddQuestion(
            "Capital of France?",
            1,
            100,
            45,
            null,
            [
                TriviaOption.Create("Paris", 1, true),
                TriviaOption.Create("Berlin", 2, false)
            ]);
        quiz.Publish(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));

        var act = () => quiz.RetireFromFutureUse(new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaQuizCannotBeRetiredWithoutUsageHistoryException>();
    }

    [Fact]
    public void EnsureCanBeDestructivelyRemoved_WhenQuizHasUsageHistory_Throws()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");
        quiz.MarkAsUsedInSession();

        var act = () => quiz.EnsureCanBeDestructivelyRemoved();

        act.Should().Throw<TriviaQuizCannotBeDestructivelyRemovedAfterUsageException>();
    }

    [Fact]
    public void EnsureCanBeDestructivelyRemoved_WhenQuizHasNoUsageHistory_AllowsRemoval()
    {
        var quiz = TriviaQuiz.Create("Intro Quiz", "Warm-up trivia");

        var act = () => quiz.EnsureCanBeDestructivelyRemoved();

        act.Should().NotThrow();
    }
}
