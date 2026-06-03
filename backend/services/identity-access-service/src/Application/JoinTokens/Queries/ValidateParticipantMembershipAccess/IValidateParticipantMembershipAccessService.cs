namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public interface IValidateParticipantMembershipAccessService
{
    Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken);
}
