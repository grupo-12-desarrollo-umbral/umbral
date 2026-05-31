namespace umbral_backend.Application.Teams.Queries.GetTeamParticipants;

public sealed class GetTeamParticipantsQueryValidator : AbstractValidator<GetTeamParticipantsQuery>
{
    public GetTeamParticipantsQueryValidator()
    {
        RuleFor(query => query.TeamId)
            .NotEmpty();
    }
}
