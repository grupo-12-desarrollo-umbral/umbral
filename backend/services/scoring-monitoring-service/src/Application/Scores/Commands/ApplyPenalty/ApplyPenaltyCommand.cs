using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Scores.Commands.ApplyPenalty;

[Authorize(Roles = "Administrator,Operator")]
public sealed record ApplyPenaltyCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string Reason) : IRequest<AppliedPenaltyDto>;
