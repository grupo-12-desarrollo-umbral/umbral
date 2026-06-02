using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class UpdateTriviaQuestionCommandHandler : TriviaQuestionAuthoringCommandHandler<UpdateTriviaQuestionCommand>
{
    public UpdateTriviaQuestionCommandHandler(ITriviaQuizRepository triviaQuizRepository)
        : base(triviaQuizRepository)
    {
    }

    protected override void EnsureOperationTargetExists(TriviaQuiz triviaQuiz, UpdateTriviaQuestionCommand request)
    {
        if (triviaQuiz.Questions.All(question => question.Id != request.QuestionId))
        {
            throw new Application.Common.Exceptions.NotFoundException("TriviaQuestion", request.QuestionId);
        }
    }

    protected override void ApplyQuestionAuthoring(
        TriviaQuiz triviaQuiz,
        UpdateTriviaQuestionCommand request,
        IReadOnlyCollection<TriviaOption> options)
    {
        triviaQuiz.UpdateQuestion(
            request.QuestionId,
            request.Prompt,
            request.SequenceOrder,
            request.ScoreValue,
            request.TimeLimitSeconds,
            request.Explanation,
            options,
            request.IsActive);
    }
}
