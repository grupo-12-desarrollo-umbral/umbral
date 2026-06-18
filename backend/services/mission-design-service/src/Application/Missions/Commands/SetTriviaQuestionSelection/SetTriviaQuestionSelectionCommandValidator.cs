namespace umbral_backend.Application.Missions.Commands.SetTriviaQuestionSelection;

public sealed class SetTriviaQuestionSelectionCommandValidator : AbstractValidator<SetTriviaQuestionSelectionCommand>
{
    public SetTriviaQuestionSelectionCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TriviaQuizId).GreaterThan(0);
    }
}
