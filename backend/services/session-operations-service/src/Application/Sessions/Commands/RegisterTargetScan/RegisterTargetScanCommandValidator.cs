namespace umbral_backend.Application.Sessions.Commands.RegisterTargetScan;

public sealed class RegisterTargetScanCommandValidator : AbstractValidator<RegisterTargetScanCommand>
{
    public RegisterTargetScanCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty();
        RuleFor(command => command.TeamId).NotEmpty();
        RuleFor(command => command.ScannedValue)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value));
    }
}
