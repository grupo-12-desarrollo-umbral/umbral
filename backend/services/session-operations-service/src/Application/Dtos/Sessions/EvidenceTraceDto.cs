namespace umbral_backend.Application.Dtos.Sessions;

public sealed record EvidenceTraceDto(
    Guid LiveSessionId,
    IReadOnlyList<EvidenceTraceItemDto> Items);

public sealed record EvidenceTraceItemDto(
    Guid EvidenceSubmissionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    string SubmissionType,
    string? OriginReference,
    DateTimeOffset SubmittedAt,
    string ValidationState,
    string? RejectionReason,
    DateTimeOffset? ResolvedAt);
