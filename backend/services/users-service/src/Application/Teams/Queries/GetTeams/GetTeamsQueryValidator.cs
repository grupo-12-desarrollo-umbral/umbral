namespace umbral_backend.Application.Teams.Queries.GetTeams;

public sealed class GetTeamsQueryValidator : AbstractValidator<GetTeamsQuery>
{
    public GetTeamsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .GreaterThan(0);
    }
}
