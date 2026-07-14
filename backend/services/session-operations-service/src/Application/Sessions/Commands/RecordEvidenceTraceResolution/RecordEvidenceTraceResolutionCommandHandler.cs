using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;

public sealed class RecordEvidenceTraceResolutionCommandHandler
    : IRequestHandler<RecordEvidenceTraceResolutionCommand>
{
    private readonly IEvidenceTraceRepository _repository;

    public RecordEvidenceTraceResolutionCommandHandler(IEvidenceTraceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(
        RecordEvidenceTraceResolutionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByEvidenceSubmissionIdAsync(
            request.EvidenceSubmissionId, cancellationToken);

        // Order-tolerant: once terminal, always terminal.
        if (existing is not null &&
            (existing.ValidationState == EvidenceValidationState.Accepted ||
             existing.ValidationState == EvidenceValidationState.Rejected))
        {
            return;
        }

        var entry = existing is not null
            ? existing
            : EvidenceTraceEntry.ForRegistration(
                request.EvidenceSubmissionId,
                request.LiveSessionId,
                request.TeamId,
                request.ActiveSubstageId,
                request.SubmissionType,
                submittedByParticipantId: null,
                originReference: null,
                request.SubmittedAt);

        if (request.ResolutionState == EvidenceValidationState.Accepted)
        {
            entry.MarkAccepted(request.ResolvedAt);
        }
        else if (request.ResolutionState == EvidenceValidationState.Rejected)
        {
            entry.MarkRejected(request.RejectionReason!, request.ResolvedAt);
        }

        await _repository.UpsertAsync(entry, cancellationToken);
    }
}
