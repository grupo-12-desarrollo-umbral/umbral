using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class SessionOperatorNotAssignedException : Exception, IErrorMetadata
{
    public SessionOperatorNotAssignedException(Guid liveSessionId)
        : base($"Session '{liveSessionId}' cannot change state until an operator is assigned.")
    {
    }

    public ErrorCategory Category => ErrorCategory.Conflict;

    public string ErrorCode => "session-operator-unassigned";
}
