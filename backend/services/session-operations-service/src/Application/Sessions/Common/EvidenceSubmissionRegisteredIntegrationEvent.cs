using MassTransit;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Generic evidence-intake fact published through the session-operations transactional bus outbox.
/// Concrete forms may publish additional, specialized facts for their own downstream consumers.
/// </summary>
[EntityName("session-evidence-submission-registered")]
public sealed record EvidenceSubmissionRegisteredIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid EvidenceSubmissionId,
    Guid ActiveSubstageId,
    EvidenceSubmissionType SubmissionType,
    DateTimeOffset SubmittedAt,
    EvidenceValidationState ValidationState);
