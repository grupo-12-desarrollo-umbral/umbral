using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Commands.RegisterTargetScan;

[Authorize(Roles = "Participant")]
public sealed record RegisterTargetScanCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string ScannedValue,
    string? Token = null) : IRequest<RegisterTargetScanResultDto>;
