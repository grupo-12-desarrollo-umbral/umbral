namespace umbral_backend.Application.Permissions.Queries.CheckProtectedCapabilityAccess;

public sealed class CheckProtectedCapabilityAccessQueryValidator : AbstractValidator<CheckProtectedCapabilityAccessQuery>
{
    public CheckProtectedCapabilityAccessQueryValidator()
    {
        RuleFor(query => query.Capability)
            .IsInEnum();
    }
}
