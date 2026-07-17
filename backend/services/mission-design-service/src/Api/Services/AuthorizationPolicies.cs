namespace umbral_backend.Api.Services;

// Policy names for ASP.NET `[Authorize(Policy = ...)]` on controllers. This is a second layer only:
// the service's primary gate is the MediatR AuthorizationBehaviour reading
// Application/Common/Security/AuthorizeAttribute. Roles mirror Domain/Constants/Roles — mission-design
// has no Participant role, so no Participant policy exists here.
public static class AuthorizationPolicies
{
    public const string Administrator = nameof(Administrator);

    public const string Operator = nameof(Operator);

    public const string AdministratorOrOperator = nameof(AdministratorOrOperator);
}
