using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.TestData;

internal sealed class TestEvidenceSubmission : EvidenceSubmission
{
    internal TestEvidenceSubmission(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        DateTimeOffset submittedAt,
        EvidenceSubmissionType submissionType = EvidenceSubmissionType.TreasureHuntQrScan)
        : base(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            activeSubstageId,
            submissionType,
            submittedByParticipantId: null,
            submittedAt)
    {
    }

    internal void Accept() => MarkAcceptedByConcreteForm();
}
