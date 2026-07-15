namespace umbral_backend.Application.Scores.Commands.ApplyPenalty;

public sealed class ApplyPenaltyCommandValidator : AbstractValidator<ApplyPenaltyCommand>
{
    public ApplyPenaltyCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty();
    }
}
