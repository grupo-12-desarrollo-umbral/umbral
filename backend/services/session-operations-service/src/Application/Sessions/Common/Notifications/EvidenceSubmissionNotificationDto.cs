namespace umbral_backend.Application.Sessions.Common.Notifications;

// Operator-only evidence/submission push (HU-24B). Mirrors EvidenceTraceItemDto field-for-field so a
// push and the REST trace snapshot describe the same row: the panel merges the two by
// EvidenceSubmissionId, and a field that disagreed between them would surface as a flickering row.
// Enum-valued fields are sent as their names, matching EvidenceTraceDtoFactory.
// Broadcast to the operator-only group only, never to the participant-visible live-session group.
public sealed record EvidenceSubmissionRegisteredNotificationDto(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    string SubmissionType,
    string? OriginReference,
    DateTimeOffset SubmittedAt,
    string ValidationState);

// The Accepted and Rejected facts share this shape — ValidationState discriminates them, and
// RejectionReason is populated only when Rejected. It carries no OriginReference because neither
// resolution event does; the panel already holds it from the registered push or the REST snapshot.
public sealed record EvidenceSubmissionResolvedNotificationDto(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    string SubmissionType,
    DateTimeOffset SubmittedAt,
    string ValidationState,
    string? RejectionReason,
    DateTimeOffset ResolvedAt);
