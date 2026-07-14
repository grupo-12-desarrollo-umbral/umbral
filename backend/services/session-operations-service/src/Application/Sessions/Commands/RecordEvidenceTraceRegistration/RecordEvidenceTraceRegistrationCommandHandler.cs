using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;

public sealed class RecordEvidenceTraceRegistrationCommandHandler
    : IRequestHandler<RecordEvidenceTraceRegistrationCommand>
{
    private readonly IEvidenceTraceRepository _repository;

    public RecordEvidenceTraceRegistrationCommandHandler(IEvidenceTraceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(
        RecordEvidenceTraceRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByEvidenceSubmissionIdAsync(
            request.EvidenceSubmissionId, cancellationToken);

        var entry = EvidenceTraceEntry.ForRegistration(
            request.EvidenceSubmissionId,
            request.LiveSessionId,
            request.TeamId,
            request.ActiveSubstageId,
            request.SubmissionType,
            request.SubmittedByParticipantId,
            request.OriginReference,
            request.SubmittedAt);

        // If a resolution arrived first and created a stub, re-apply the terminal state
        // so the context fields are filled without flipping the resolution.
        if (existing is not null && existing.ValidationState != EvidenceValidationState.Pending)
        {
            if (existing.ValidationState == EvidenceValidationState.Accepted)
            {
                entry.MarkAccepted(existing.ResolvedAt!.Value);
            }
            else if (existing.ValidationState == EvidenceValidationState.Rejected)
            {
                entry.MarkRejected(existing.RejectionReason!, existing.ResolvedAt!.Value);
            }
        }

        await _repository.UpsertAsync(entry, cancellationToken);
    }
}
