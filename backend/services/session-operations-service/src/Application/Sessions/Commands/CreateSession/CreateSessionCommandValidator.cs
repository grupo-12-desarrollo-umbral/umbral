namespace umbral_backend.Application.Sessions.Commands.CreateSession;

public sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(command => command.MissionId)
            .GreaterThan(0);

        RuleFor(command => command.Title)
            .NotEmpty();

        RuleFor(command => command.MaximumTimeMinutes)
            .GreaterThan(0);

        RuleFor(command => command.ScheduledAt)
            .NotEqual(default(DateTimeOffset));
    }
}
