namespace umbral_backend.Application.Dtos.Sessions;

public sealed record RegisterTargetScanResultDto(
    Guid LiveSessionId,
    Guid TeamId,
    Guid ActiveSubstageId,
    Guid? TargetSnapshotId,
    bool IsResolved,
    string? RejectionReason,
    DateTimeOffset SubmittedAt);
