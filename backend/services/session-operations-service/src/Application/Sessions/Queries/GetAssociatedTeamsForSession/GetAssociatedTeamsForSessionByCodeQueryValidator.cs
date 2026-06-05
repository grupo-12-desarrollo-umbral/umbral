namespace umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

public sealed class GetAssociatedTeamsForSessionByCodeQueryValidator
    : AbstractValidator<GetAssociatedTeamsForSessionByCodeQuery>
{
    public GetAssociatedTeamsForSessionByCodeQueryValidator()
    {
        RuleFor(query => query.SessionCode)
            .NotEmpty();
    }
}
