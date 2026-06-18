namespace umbral_backend.Application.Missions.Commands.RemoveMissionNode;

public sealed class RemoveMissionNodeCommandValidator : AbstractValidator<RemoveMissionNodeCommand>
{
    public RemoveMissionNodeCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.NodeId).GreaterThan(0);
    }
}
