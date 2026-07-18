using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandValidator : AbstractValidator<CreateMissionCommand>
{
    private const int MaximumNameLength = 200;
    private const int MaximumDescriptionLength = 2000;
    private const int MaximumDifficultyLength = 100;

    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(MaximumNameLength);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(MaximumDescriptionLength);

        RuleFor(command => command.Difficulty)
            .NotEmpty()
            .MaximumLength(MaximumDifficultyLength);

        RuleFor(command => command.MaximumTimeMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaximumTime.MaximumMinutes);
    }
}
