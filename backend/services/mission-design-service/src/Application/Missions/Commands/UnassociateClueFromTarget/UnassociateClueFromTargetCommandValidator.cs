namespace umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;

public sealed class UnassociateClueFromTargetCommandValidator : AbstractValidator<UnassociateClueFromTargetCommand>
{
    public UnassociateClueFromTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TargetId).GreaterThan(0);
    }
}
