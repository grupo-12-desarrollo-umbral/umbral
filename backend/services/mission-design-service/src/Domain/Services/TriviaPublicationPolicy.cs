using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public static class TriviaPublicationPolicy
{
    public static void EnsurePublishable(TriviaQuiz triviaQuiz)
    {
        if (triviaQuiz.Questions.Count == 0)
        {
            throw new TriviaQuizMustHaveAtLeastOneQuestionToPublishException();
        }

        foreach (var question in triviaQuiz.Questions)
        {
            if (question.ScoreValue is null)
            {
                throw new TriviaQuestionScoreValueRequiredToPublishException(question.Id);
            }

            if (question.TimeLimit is null)
            {
                throw new TriviaQuestionTimeLimitRequiredToPublishException(question.Id);
            }

            if (question.Options.Count is < 2 or > 4)
            {
                throw new TriviaQuestionMustHaveBetweenTwoAndFourOptionsException();
            }

            if (question.Options.Count(option => option.IsCorrect) != 1)
            {
                throw new TriviaQuestionMustHaveExactlyOneCorrectOptionException();
            }
        }
    }
}
