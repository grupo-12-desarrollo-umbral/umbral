using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common.EvidenceValidation;

/// <summary>
/// Immutable common context evaluated after an evidence record has been registered Pending.
/// </summary>
public sealed class EvidenceValidationContext
{
    public EvidenceValidationContext(
        EvidenceSubmission submission,
        LiveSession session,
        Guid teamId,
        Guid activeSubstageId,
        string? origin,
        DateTimeOffset submittedAt)
    {
        Submission = submission;
        Session = session;
        TeamId = teamId;
        ActiveSubstageId = activeSubstageId;
        Origin = origin;
        SubmittedAt = submittedAt;
    }

    public EvidenceSubmission Submission { get; }

    public LiveSession Session { get; }

    public Guid TeamId { get; }

    public Guid ActiveSubstageId { get; }

    public string? Origin { get; }

    public DateTimeOffset SubmittedAt { get; }
}
