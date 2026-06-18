namespace umbral_backend.Application.Missions.Commands.UpdateMissionNode;

public sealed class UpdateMissionNodeCommandValidator : AbstractValidator<UpdateMissionNodeCommand>
{
    private static readonly string[] ClueVisibilityPolicies = ["VisibleWhenSubstageStarts", "HiddenUntilOperatorRelease"];

    public UpdateMissionNodeCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.NodeId).GreaterThan(0);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);
        RuleFor(command => command.ClueText).MaximumLength(2000);
        RuleFor(command => command.ClueVisibilityPolicy)
            .Must(policy => policy is null || ClueVisibilityPolicies.Contains(policy))
            .WithMessage("ClueVisibilityPolicy must be VisibleWhenSubstageStarts or HiddenUntilOperatorRelease.");
    }
}
