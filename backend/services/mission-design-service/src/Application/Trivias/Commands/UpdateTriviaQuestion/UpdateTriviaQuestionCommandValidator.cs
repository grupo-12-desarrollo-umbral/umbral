using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;

public sealed class UpdateTriviaQuestionCommandValidator : TriviaQuestionAuthoringCommandValidator<UpdateTriviaQuestionCommand>
{
    protected override void AddOperationSpecificRules()
    {
        RuleFor(command => command.QuestionId)
            .GreaterThan(0);
    }
}
