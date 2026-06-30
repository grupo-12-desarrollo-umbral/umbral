namespace umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;

public sealed class SelectTriviaQuizCommandValidator : AbstractValidator<SelectTriviaQuizCommand>
{
    public SelectTriviaQuizCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TriviaQuizId).GreaterThan(0);
    }
}
