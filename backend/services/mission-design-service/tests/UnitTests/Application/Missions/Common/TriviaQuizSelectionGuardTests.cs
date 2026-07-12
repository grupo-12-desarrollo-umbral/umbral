using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Application.Missions.Common;

// Covers every guard arm of EnsurePublishedSelectionAsync: missing quiz, unpublished quiz, and the
// per-question runtime-readiness re-check that a published selection must still satisfy. The readiness
// re-check is defensive against a published quiz whose questions were left in a non-runnable shape, so
// each malformed case is assembled directly (Status forced to Published) rather than through Publish(),
// which would reject them up front.
public sealed class TriviaQuizSelectionGuardTests
{
    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuizMissing_ThrowsNotFound()
    {
        var act = async () => await TriviaQuizSelectionGuard.EnsurePublishedSelectionAsync(
            new InMemoryTriviaQuizRepository(), triviaQuizId: 999, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuizNotPublished_ThrowsValidation()
    {
        var quiz = TriviaQuiz.Create("Draft Quiz", "Still in progress");

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_PublishedQuizWithNoQuestions_ThrowsValidation()
    {
        var quiz = Published(TriviaQuiz.Create("Empty Quiz", "No questions"));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_PublishedQuizWithRuntimeReadyQuestions_Succeeds()
    {
        var quiz = Published(TriviaQuiz.Create("Ready Quiz", "All set", [ValidQuestion()]));

        await Guard(quiz).Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuestionInactive_ThrowsValidation()
    {
        var quiz = Published(TriviaQuiz.Create(
            "Quiz", "desc", [ValidQuestion(isActive: false)]));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuestionMissingScoreValue_ThrowsValidation()
    {
        var question = TriviaQuestion.Create("Q", null, 45, null, TwoOptions());
        var quiz = Published(TriviaQuiz.Create("Quiz", "desc", [question]));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuestionMissingTimeLimit_ThrowsValidation()
    {
        var question = TriviaQuestion.Create("Q", 100, null, null, TwoOptions());
        var quiz = Published(TriviaQuiz.Create("Quiz", "desc", [question]));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuestionHasTooFewOptions_ThrowsValidation()
    {
        var question = TriviaQuestion.Create(
            "Q", 100, 45, null, [TriviaOption.Create("Only", 1, true)]);
        var quiz = Published(TriviaQuiz.Create("Quiz", "desc", [question]));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EnsurePublishedSelectionAsync_QuestionHasNoCorrectOption_ThrowsValidation()
    {
        var question = TriviaQuestion.Create(
            "Q", 100, 45, null,
            [TriviaOption.Create("A", 1, false), TriviaOption.Create("B", 2, false)]);
        var quiz = Published(TriviaQuiz.Create("Quiz", "desc", [question]));

        await Guard(quiz).Should().ThrowAsync<ValidationException>();
    }

    private static Func<Task> Guard(TriviaQuiz quiz)
    {
        var repository = new InMemoryTriviaQuizRepository();
        repository.Seed(quiz);
        return async () => await TriviaQuizSelectionGuard.EnsurePublishedSelectionAsync(
            repository, quiz.Id, CancellationToken.None);
    }

    private static TriviaQuestion ValidQuestion(bool isActive = true) =>
        TriviaQuestion.Create("Q", 100, 45, null, TwoOptions(), isActive);

    private static TriviaOption[] TwoOptions() =>
        [TriviaOption.Create("A", 1, true), TriviaOption.Create("B", 2, false)];

    // A published quiz whose questions violate runtime-readiness cannot be produced through Publish(),
    // so force the status the guard branches on directly.
    private static TriviaQuiz Published(TriviaQuiz quiz)
    {
        typeof(TriviaQuiz)
            .GetProperty(nameof(TriviaQuiz.Status))!
            .SetValue(quiz, TriviaQuizStatus.Published);
        return quiz;
    }
}
