namespace umbral_backend.Application.Missions.Queries.GetMissionDetail;

public sealed class GetMissionDetailQueryValidator : AbstractValidator<GetMissionDetailQuery>
{
    public GetMissionDetailQueryValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0);
    }
}
