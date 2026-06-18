using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using ApplicationNotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;

namespace umbral_backend.Application.Missions.Common;

internal static class TriviaQuestionSelectionGuard
{
    public static async Task EnsurePublishedSelectionAsync(
        ITriviaQuizRepository triviaQuizRepository,
        int triviaQuizId,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await triviaQuizRepository.GetByIdAsync(triviaQuizId, cancellationToken)
            ?? throw new ApplicationNotFoundException("TriviaQuiz", triviaQuizId);

        if (triviaQuiz.Status != TriviaQuizStatus.Published)
        {
            throw MissionStructureEditor.ValidationFailure(
                "TriviaQuizId",
                "TriviaQuestionSelection must reference a published TriviaQuiz.");
        }

        // Canon: the whole published quiz is selected. Every question must be runtime-ready.
        EnsurePublishedQuestionSet(triviaQuiz);
    }

    private static void EnsurePublishedQuestionSet(TriviaQuiz triviaQuiz)
    {
        if (triviaQuiz.Questions.Count == 0)
        {
            throw MissionStructureEditor.ValidationFailure(
                "TriviaQuizId",
                "TriviaQuestionSelection must reference a quiz with at least one question.");
        }

        if (triviaQuiz.Questions.Any(question =>
                !question.IsActive
                || question.ScoreValue is null
                || question.TimeLimit is null
                || question.Options.Count < 2
                || question.Options.Count(option => option.IsCorrect) != 1))
        {
            throw MissionStructureEditor.ValidationFailure(
                "TriviaQuizId",
                "Every question must be active and have options, one correct answer, score value, and time limit.");
        }
    }
}
