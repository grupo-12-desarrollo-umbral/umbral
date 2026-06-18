namespace umbral_backend.Application.Missions.Commands.ActivateMission;

public sealed class ActivateMissionCommandValidator : AbstractValidator<ActivateMissionCommand>
{
    public ActivateMissionCommandValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);
    }
}
