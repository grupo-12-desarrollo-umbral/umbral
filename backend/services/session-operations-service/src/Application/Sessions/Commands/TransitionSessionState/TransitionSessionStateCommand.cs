using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.TransitionSessionState;

[Authorize(Roles = "Operator")]
public sealed record TransitionSessionStateCommand(
    Guid LiveSessionId,
    SessionState TargetState,
    string? Reason) : IRequest<TransitionSessionStateResultDto>;
