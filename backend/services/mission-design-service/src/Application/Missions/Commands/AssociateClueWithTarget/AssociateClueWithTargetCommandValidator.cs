namespace umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;

public sealed class AssociateClueWithTargetCommandValidator : AbstractValidator<AssociateClueWithTargetCommand>
{
    public AssociateClueWithTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TargetId).GreaterThan(0);
        RuleFor(command => command.ClueId).GreaterThan(0);
    }
}
