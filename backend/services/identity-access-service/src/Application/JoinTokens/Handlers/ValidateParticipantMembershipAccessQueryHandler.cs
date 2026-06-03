using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

namespace umbral_backend.Application.JoinTokens.Handlers;

public sealed class ValidateParticipantMembershipAccessQueryHandler
    : IRequestHandler<ValidateParticipantMembershipAccessQuery, ParticipantMembershipAccessDecisionDto>
{
    private readonly IValidateParticipantMembershipAccessService _validationService;

    public ValidateParticipantMembershipAccessQueryHandler(IValidateParticipantMembershipAccessService validationService)
    {
        _validationService = validationService;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> Handle(
        ValidateParticipantMembershipAccessQuery request,
        CancellationToken cancellationToken)
    {
        return await _validationService.ValidateAsync(request, cancellationToken);
    }
}
