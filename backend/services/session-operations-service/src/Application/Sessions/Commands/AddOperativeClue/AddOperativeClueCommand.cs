using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Commands.AddOperativeClue;

[Authorize(Roles = "Operator")]
public sealed record AddOperativeClueCommand(
    Guid LiveSessionId,
    string ClueText,
    IReadOnlyList<Guid> TeamIds) : IRequest<AddOperativeClueResultDto>;
