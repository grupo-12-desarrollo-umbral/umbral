namespace umbral_backend.Application.Missions.Commands.AddTarget;

public sealed class AddTargetCommandValidator : AbstractValidator<AddTargetCommand>
{
    // Score is intentionally absent: it is derived from the mission's difficulty
    // (see Mission.AddTarget), never supplied by the caller.
    public AddTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.QrCode).NotEmpty().MaximumLength(500);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);
        RuleFor(command => command.Latitude).InclusiveBetween(-90, 90);
        RuleFor(command => command.Longitude).InclusiveBetween(-180, 180);
    }
}
