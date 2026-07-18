using umbral_backend.Application.Dtos.Users;

namespace umbral_backend.Application.Users.Commands.RegisterParticipant;

// Anonymous by design (ADR-0016 §1): a person registering has no account yet, exactly as the login
// token call is unauthenticated. Deliberately carries NO [Authorize] attribute and NO role field —
// the role is server-fixed to Participant in the handler and can never be selected from the request,
// so this path can never mint an Operator or Administrator.
public sealed record RegisterParticipantCommand(string DisplayName, string Email, string Password)
    : IRequest<RegisterParticipantResultDto>;
