namespace umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;

public sealed class SetTriviaQuizSelectionCommandValidator : AbstractValidator<SetTriviaQuizSelectionCommand>
{
    public SetTriviaQuizSelectionCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TriviaQuizId).GreaterThan(0);
    }
}
