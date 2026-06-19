namespace umbral_backend.Application.Missions.Queries.GetMissionReadiness;

public sealed class GetMissionReadinessQueryValidator : AbstractValidator<GetMissionReadinessQuery>
{
    public GetMissionReadinessQueryValidator()
    {
        RuleFor(query => query.Id).GreaterThan(0);
    }
}
