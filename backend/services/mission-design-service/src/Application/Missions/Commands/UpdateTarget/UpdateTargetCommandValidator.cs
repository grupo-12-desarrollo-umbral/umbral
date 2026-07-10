namespace umbral_backend.Application.Missions.Commands.UpdateTarget;

public sealed class UpdateTargetCommandValidator : AbstractValidator<UpdateTargetCommand>
{
    // Score is intentionally absent: it is derived from the mission's difficulty
    // (see Mission.UpdateTarget), never supplied by the caller.
    public UpdateTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.TargetId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.QrCode).NotEmpty().MaximumLength(500);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);
    }
}
