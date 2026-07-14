namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;

public sealed class RecordEvidenceTraceRegistrationCommandValidator
    : AbstractValidator<RecordEvidenceTraceRegistrationCommand>
{
    public RecordEvidenceTraceRegistrationCommandValidator()
    {
        RuleFor(command => command.EvidenceSubmissionId)
            .NotEmpty();

        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.ActiveSubstageId)
            .NotEmpty();

        RuleFor(command => command.SubmissionType)
            .IsInEnum();
    }
}
