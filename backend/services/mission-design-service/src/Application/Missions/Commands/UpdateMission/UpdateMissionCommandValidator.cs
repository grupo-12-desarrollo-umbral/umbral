using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Missions.Commands.UpdateMission;

public sealed class UpdateMissionCommandValidator : AbstractValidator<UpdateMissionCommand>
{
    private const int MaximumNameLength = 200;
    private const int MaximumDescriptionLength = 2000;
    private const int MaximumDifficultyLength = 100;

    public UpdateMissionCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0);

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
