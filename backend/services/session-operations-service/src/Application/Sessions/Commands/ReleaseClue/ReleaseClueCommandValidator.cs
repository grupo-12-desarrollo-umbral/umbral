namespace umbral_backend.Application.Sessions.Commands.ReleaseClue;

public sealed class ReleaseClueCommandValidator : AbstractValidator<ReleaseClueCommand>
{
    public ReleaseClueCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TargetId)
            .NotEmpty();
    }
}
