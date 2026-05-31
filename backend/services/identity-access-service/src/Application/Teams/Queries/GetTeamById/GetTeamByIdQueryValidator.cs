namespace umbral_backend.Application.Teams.Queries.GetTeamById;

public sealed class GetTeamByIdQueryValidator : AbstractValidator<GetTeamByIdQuery>
{
    public GetTeamByIdQueryValidator()
    {
        RuleFor(query => query.TeamId)
            .NotEmpty();
    }
}
