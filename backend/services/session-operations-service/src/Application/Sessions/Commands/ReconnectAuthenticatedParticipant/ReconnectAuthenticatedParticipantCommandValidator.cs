namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandValidator : AbstractValidator<ReconnectAuthenticatedParticipantCommand>
{
    public ReconnectAuthenticatedParticipantCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.DisplayName)
            .NotEmpty();

        RuleFor(command => command.TeamCapacity)
            .GreaterThan(0);
    }
}
