using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;

public sealed record RecordEvidenceTraceResolutionCommand(
    Guid EvidenceSubmissionId,
    Guid LiveSessionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    EvidenceSubmissionType SubmissionType,
    DateTimeOffset SubmittedAt,
    EvidenceValidationState ResolutionState,
    string? RejectionReason,
    DateTimeOffset ResolvedAt) : IRequest;
