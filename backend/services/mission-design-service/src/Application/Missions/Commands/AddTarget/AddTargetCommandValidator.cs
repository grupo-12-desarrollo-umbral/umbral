namespace umbral_backend.Application.Missions.Commands.AddTarget;

public sealed class AddTargetCommandValidator : AbstractValidator<AddTargetCommand>
{
    private const int MinimumScore = 1;
    private const int MaximumScore = 100;

    public AddTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.QrCode).NotEmpty().MaximumLength(500);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);
        RuleFor(command => command.Score).InclusiveBetween(MinimumScore, MaximumScore);
    }
}
