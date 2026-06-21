namespace umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

public sealed class GetMissionRuntimePlanQueryValidator : AbstractValidator<GetMissionRuntimePlanQuery>
{
    public GetMissionRuntimePlanQueryValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0);
    }
}
