namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;

public sealed class RecordEvidenceTraceResolutionCommandValidator
    : AbstractValidator<RecordEvidenceTraceResolutionCommand>
{
    public RecordEvidenceTraceResolutionCommandValidator()
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

        RuleFor(command => command.ResolutionState)
            .Must(state => state == Domain.Enums.EvidenceValidationState.Accepted ||
                           state == Domain.Enums.EvidenceValidationState.Rejected)
            .WithMessage("ResolutionState must be Accepted or Rejected.");

        RuleFor(command => command.RejectionReason)
            .NotNull()
            .When(command => command.ResolutionState == Domain.Enums.EvidenceValidationState.Rejected);
    }
}
