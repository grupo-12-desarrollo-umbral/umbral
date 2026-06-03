namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public interface IValidateParticipantMembershipAccessExecutor
{
    Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken);
}
