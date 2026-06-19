namespace umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;

public sealed class AssignSubstagePlayModeCommandValidator : AbstractValidator<AssignSubstagePlayModeCommand>
{
    private static readonly string[] PlayModes = ["TreasureHunt", "Trivia"];

    public AssignSubstagePlayModeCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.PlayMode)
            .NotEmpty()
            .Must(playMode => PlayModes.Contains(playMode))
            .WithMessage("PlayMode must be TreasureHunt or Trivia.");
    }
}
