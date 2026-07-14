using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;

public sealed record RecordEvidenceTraceRegistrationCommand(
    Guid EvidenceSubmissionId,
    Guid LiveSessionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    EvidenceSubmissionType SubmissionType,
    Guid? SubmittedByParticipantId,
    string? OriginReference,
    DateTimeOffset SubmittedAt) : IRequest;
