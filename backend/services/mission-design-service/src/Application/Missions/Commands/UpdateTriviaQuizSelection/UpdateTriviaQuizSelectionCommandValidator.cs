namespace umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;

public sealed class UpdateTriviaQuizSelectionCommandValidator : AbstractValidator<UpdateTriviaQuizSelectionCommand>
{
    public UpdateTriviaQuizSelectionCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TriviaQuizId).GreaterThan(0);
    }
}
