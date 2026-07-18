using MassTransit;

namespace umbral_backend.Application.Scores.Common;

// The publisher (session-operations-service) declares this contract in its own
// `umbral_backend.Application.Sessions.Common` namespace. MassTransit routes the message to this
// consumer's queue by the [EntityName] exchange, but it *matches* the deserialized message to a
// consumer by the message-type URN, which defaults to `urn:message:{namespace}:{type}`. Because the
// two records live in different namespaces, the default URNs differ and every event would be moved to
// the `_skipped` queue unconsumed. Pinning the URN to the publisher's namespace makes the identity
// match so the scan actually scores. Keep this in sync with the publisher's namespace/type name.
// MassTransit prepends the `urn:message:` prefix itself, so supply only the namespace:type suffix.
[MessageUrn("umbral_backend.Application.Sessions.Common:TargetResolvedIntegrationEvent")]
[EntityName("session-target-resolved")]
public sealed record TargetResolvedIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid ReferenceTeamId,
    string TeamDisplayName,
    Guid EvidenceSubmissionId,
    Guid ActiveSubstageId,
    Guid TargetSnapshotId,
    int ScoreValue,
    // Mission difficulty multiplier (1/2/3) behind ScoreValue. Scoring's TargetScorePolicy applies it
    // to the base score so the difficulty rule is owned here, not reverse-engineered from ScoreValue.
    int DifficultyFactor,
    DateTimeOffset ResolvedAt);
