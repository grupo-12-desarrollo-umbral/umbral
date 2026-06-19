namespace umbral_backend.Application.Missions.Commands.AddMissionNode;

public sealed class AddMissionNodeCommandValidator : AbstractValidator<AddMissionNodeCommand>
{
    private static readonly string[] NodeTypes = ["Stage", "Substage", "Clue"];
    private static readonly string[] PlayModes = ["TreasureHunt", "Trivia"];
    private static readonly string[] ClueVisibilityPolicies = ["VisibleWhenSubstageStarts", "HiddenUntilOperatorRelease"];

    public AddMissionNodeCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);

        RuleFor(command => command.NodeType)
            .NotEmpty()
            .Must(nodeType => NodeTypes.Contains(nodeType))
            .WithMessage("NodeType must be Stage, Substage, or Clue.");

        When(command => command.NodeType == "Stage", () =>
        {
            RuleFor(command => command.StageId).Null();
            RuleFor(command => command.SubstageId).Null();
            RuleFor(command => command.PlayMode).Null();
            RuleFor(command => command.ClueText).Null();
        });

        When(command => command.NodeType == "Substage", () =>
        {
            RuleFor(command => command.StageId).NotNull().GreaterThan(0);
            RuleFor(command => command.SubstageId).Null();
            RuleFor(command => command.PlayMode)
                .NotEmpty()
                .Must(playMode => playMode is not null && PlayModes.Contains(playMode))
                .WithMessage("PlayMode must be TreasureHunt or Trivia.");
            RuleFor(command => command.ClueText).Null();
        });

        When(command => command.NodeType == "Clue", () =>
        {
            RuleFor(command => command.StageId).NotNull().GreaterThan(0);
            RuleFor(command => command.SubstageId).NotNull().GreaterThan(0);
            RuleFor(command => command.PlayMode).Null();
            RuleFor(command => command.ClueText).NotEmpty().MaximumLength(2000);
            RuleFor(command => command.ClueVisibilityPolicy)
                .Must(policy => policy is null || ClueVisibilityPolicies.Contains(policy))
                .WithMessage("ClueVisibilityPolicy must be VisibleWhenSubstageStarts or HiddenUntilOperatorRelease.");
        });
    }
}
