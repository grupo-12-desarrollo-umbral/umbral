namespace umbral_backend.Application.Sessions.Commands.ReleaseClue;

public sealed class ReleaseClueCommandValidator : AbstractValidator<ReleaseClueCommand>
{
    public ReleaseClueCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TargetId)
            .Must(targetId => targetId is null || targetId.Value != Guid.Empty)
            .WithMessage("TargetId must not be empty when provided.");

        RuleFor(command => command.ClueId)
            .Must(clueId => clueId is null || clueId.Value != Guid.Empty)
            .WithMessage("ClueId must not be empty when provided.");

        RuleFor(command => command)
            .Must(command => command.TargetId.HasValue ^ command.ClueId.HasValue)
            .WithMessage("Exactly one of TargetId or ClueId must be provided.");
    }
}
