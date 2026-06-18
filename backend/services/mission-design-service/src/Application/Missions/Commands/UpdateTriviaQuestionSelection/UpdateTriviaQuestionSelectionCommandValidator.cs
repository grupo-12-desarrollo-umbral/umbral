namespace umbral_backend.Application.Missions.Commands.UpdateTriviaQuestionSelection;

public sealed class UpdateTriviaQuestionSelectionCommandValidator : AbstractValidator<UpdateTriviaQuestionSelectionCommand>
{
    public UpdateTriviaQuestionSelectionCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TriviaQuizId).GreaterThan(0);
    }
}
