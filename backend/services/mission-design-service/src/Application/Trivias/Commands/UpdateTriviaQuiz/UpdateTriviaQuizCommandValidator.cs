using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;

public sealed class UpdateTriviaQuizCommandValidator : TriviaQuizAuthoringCommandValidator<UpdateTriviaQuizCommand>
{
    protected override void AddOperationSpecificRules()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
