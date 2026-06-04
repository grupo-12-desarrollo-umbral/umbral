namespace umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

public sealed class GetAssociatedTeamsForSessionQueryValidator : AbstractValidator<GetAssociatedTeamsForSessionQuery>
{
    public GetAssociatedTeamsForSessionQueryValidator()
    {
        RuleFor(query => query.LiveSessionId)
            .NotEmpty();
    }
}
