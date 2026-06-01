namespace umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

public sealed class GetTriviaDetailQueryValidator : AbstractValidator<GetTriviaDetailQuery>
{
    public GetTriviaDetailQueryValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0);
    }
}
