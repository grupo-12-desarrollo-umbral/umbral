namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed class GetSessionTeamsForParticipantQueryValidator : AbstractValidator<GetSessionTeamsForParticipantQuery>
{
    public GetSessionTeamsForParticipantQueryValidator()
    {
        RuleFor(query => query.SessionCode)
            .NotEmpty()
            .Length(6)
            .Matches("^[A-Za-z0-9]{6}$");
    }
}
