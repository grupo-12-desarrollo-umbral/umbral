namespace umbral_backend.Application.Missions.Commands.DeactivateMission;

public sealed class DeactivateMissionCommandValidator : AbstractValidator<DeactivateMissionCommand>
{
    public DeactivateMissionCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);
    }
}
