namespace umbral_backend.Api.Services;

public static class AuthorizationPolicies
{
    public const string Administrator = nameof(Administrator);

    public const string Operator = nameof(Operator);

    public const string Participant = nameof(Participant);

    public const string AdministratorOrOperator = nameof(AdministratorOrOperator);

    public const string ParticipantOrOperator = nameof(ParticipantOrOperator);
}
