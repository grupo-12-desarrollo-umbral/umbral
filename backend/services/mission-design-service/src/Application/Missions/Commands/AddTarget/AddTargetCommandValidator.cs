namespace umbral_backend.Application.Missions.Commands.AddTarget;

public sealed class AddTargetCommandValidator : AbstractValidator<AddTargetCommand>
{
    public AddTargetCommandValidator()
    {
        RuleFor(command => command.MissionId).GreaterThan(0);
        RuleFor(command => command.StageId).GreaterThan(0);
        RuleFor(command => command.SubstageId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.QrCode).NotEmpty().MaximumLength(500);
        RuleFor(command => command.SequenceOrder).GreaterThan(0);
        RuleFor(command => command.Score).GreaterThan(0).When(command => command.Score is not null);
        RuleFor(command => command.WinnerScore).GreaterThan(0).When(command => command.WinnerScore is not null);
    }
}
