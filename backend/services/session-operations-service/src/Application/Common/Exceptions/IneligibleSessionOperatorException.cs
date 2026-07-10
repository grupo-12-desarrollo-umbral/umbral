using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class IneligibleSessionOperatorException : Exception, IErrorMetadata
{
    public IneligibleSessionOperatorException(int operatorUserId)
        : base($"User '{operatorUserId}' is not eligible to be assigned as a session operator.")
    {
        OperatorUserId = operatorUserId;
    }

    public int OperatorUserId { get; }

    public ErrorCategory Category => ErrorCategory.Validation;

    public string ErrorCode => "ineligible-session-operator";

    // Safe to expose: the eligibility rule is client-actionable and the identifier stays out.
    public string? PublicDetail => "The user is not eligible to be assigned as a session operator.";
}
