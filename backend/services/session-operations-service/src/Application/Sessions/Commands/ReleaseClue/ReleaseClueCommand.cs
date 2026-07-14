using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Commands.ReleaseClue;

[Authorize(Roles = "Operator")]
public sealed record ReleaseClueCommand(
    Guid LiveSessionId,
    Guid? TargetId,
    Guid? ClueId,
    Guid? TeamId) : IRequest<ReleaseClueResultDto>;
