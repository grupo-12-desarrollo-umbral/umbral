using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IEvidenceTraceRepository
{
    Task<EvidenceTraceEntry?> GetByEvidenceSubmissionIdAsync(
        Guid evidenceSubmissionId,
        CancellationToken cancellationToken);

    Task UpsertAsync(EvidenceTraceEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceTraceEntry>> ListBySessionAsync(
        Guid liveSessionId,
        Guid? teamId,
        CancellationToken cancellationToken);
}
