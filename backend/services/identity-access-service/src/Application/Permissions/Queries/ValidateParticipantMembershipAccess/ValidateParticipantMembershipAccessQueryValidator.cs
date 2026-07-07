namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed class ValidateParticipantMembershipAccessQueryValidator : AbstractValidator<ValidateParticipantMembershipAccessQuery>
{
    public ValidateParticipantMembershipAccessQueryValidator()
    {
        RuleFor(query => query.LiveSessionId)
            .NotEmpty();

        RuleFor(query => query.TeamId)
            .NotEmpty();
    }
}
