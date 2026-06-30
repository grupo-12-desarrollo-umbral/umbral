using umbral_backend.Application.Trivias.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Authoring;

internal static class TriviaQuizDtoMapper
{
    public static TriviaQuizDto Map(TriviaQuiz triviaQuiz)
    {
        return new TriviaQuizDto(
            triviaQuiz.Id,
            triviaQuiz.Title,
            triviaQuiz.Description,
            triviaQuiz.Status.ToString(),
            triviaQuiz.Questions
                .OrderBy(question => question.SequenceOrder)
                .Select(question => new TriviaQuestionDto(
                    question.Id,
                    question.Prompt,
                    question.SequenceOrder,
                    question.IsActive,
                    question.Options
                        .OrderBy(option => option.SequenceOrder)
                        .Select(option => new TriviaOptionDto(
                            option.Id,
                            option.OptionText,
                            option.SequenceOrder,
                            option.IsCorrect))
                        .ToArray(),
                    question.ScoreValue,
                    question.TimeLimit?.Seconds,
                    question.Explanation))
                .ToArray(),
            triviaQuiz.SourceTriviaQuizId,
            triviaQuiz.HasUsageHistory,
            triviaQuiz.IsDuplicate);
    }
}
