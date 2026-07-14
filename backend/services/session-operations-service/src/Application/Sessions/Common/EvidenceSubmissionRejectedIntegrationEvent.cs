using MassTransit;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common;

// AC#5 / ddd_solution_model.md:615 — first audit/history consumer in this service;
// override of matrix transport table :59 "neither"
[EntityName("session-evidence-submission-rejected")]
public sealed record EvidenceSubmissionRejectedIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid EvidenceSubmissionId,
    Guid ActiveSubstageId,
    EvidenceSubmissionType SubmissionType,
    DateTimeOffset SubmittedAt,
    string RejectionReason,
    DateTimeOffset ResolvedAt);
