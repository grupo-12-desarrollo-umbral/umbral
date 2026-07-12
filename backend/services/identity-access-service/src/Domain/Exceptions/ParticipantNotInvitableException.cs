using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Raised when an admin-initiated invitation targets the <see cref="Role.Participant"/> role.
/// Participants self-register; only <see cref="Role.Operator"/> and <see cref="Role.Administrator"/>
/// accounts are created through the invitation flow.
/// </summary>
public sealed class ParticipantNotInvitableException : DomainException
{
    public ParticipantNotInvitableException()
        : base("Participants self-register and cannot be created through an invitation.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Unprocessable;

    // Safe to expose: no identifiers, and the rule is client-actionable.
    public override string? PublicDetail =>
        "Participants self-register and cannot be invited. Only Operator and Administrator accounts can be invited.";
}
