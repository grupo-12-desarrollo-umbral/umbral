using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class AddTriviaQuestionCommandHandler : TriviaQuestionAuthoringCommandHandler<AddTriviaQuestionCommand>
{
    public AddTriviaQuestionCommandHandler(ITriviaQuizRepository triviaQuizRepository)
        : base(triviaQuizRepository)
    {
    }

    protected override void ApplyQuestionAuthoring(
        TriviaQuiz triviaQuiz,
        AddTriviaQuestionCommand request,
        IReadOnlyCollection<TriviaOption> options)
    {
        triviaQuiz.AddQuestion(
            request.Prompt,
            request.SequenceOrder,
            request.ScoreValue,
            request.TimeLimitSeconds,
            request.Explanation,
            options,
            request.IsActive);
    }
}
