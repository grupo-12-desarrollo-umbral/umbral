namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantCommandValidator : AbstractValidator<DisconnectParticipantCommand>
{
    public DisconnectParticipantCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.SessionParticipantId)
            .NotEmpty();
    }
}
