using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

public static class EvidenceTraceDtoFactory
{
    public static EvidenceTraceDto Create(
        Guid liveSessionId,
        IReadOnlyList<EvidenceTraceEntry> entries)
    {
        var items = entries
            .Select(entry => new EvidenceTraceItemDto(
                entry.EvidenceSubmissionId,
                entry.TeamId,
                entry.ActiveSubstageId,
                entry.SubmissionType.ToString(),
                entry.OriginReference,
                entry.SubmittedAt,
                entry.ValidationState.ToString(),
                entry.RejectionReason,
                entry.ResolvedAt))
            .ToList();

        return new EvidenceTraceDto(liveSessionId, items);
    }
}
