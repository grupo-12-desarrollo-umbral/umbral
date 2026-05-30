namespace umbral_backend.Application.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(command => command.Difficulty)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.MaximumTimeMinutes)
            .GreaterThan(0);
    }
}
