namespace umbral_backend.Application.Missions.Commands.RemoveTarget;

public sealed class RemoveTargetCommandValidator : AbstractValidator<RemoveTargetCommand>
{
    public RemoveTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TargetId).GreaterThan(0);
    }
}
