using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;

/// <summary>
/// Immutable input shared by every concrete evidence form's admission pipeline.
/// </summary>
public sealed class EvidenceIntakeValidationContext
{
    public EvidenceIntakeValidationContext(
        LiveSession session,
        Guid teamId,
        Guid activeSubstageId,
        string? token,
        DateTimeOffset submittedAt)
    {
        Session = session;
        TeamId = teamId;
        ActiveSubstageId = activeSubstageId;
        Token = token;
        SubmittedAt = submittedAt;
    }

    public LiveSession Session { get; }

    public Guid TeamId { get; }

    public Guid ActiveSubstageId { get; }

    public string? Token { get; }

    public DateTimeOffset SubmittedAt { get; }
}
