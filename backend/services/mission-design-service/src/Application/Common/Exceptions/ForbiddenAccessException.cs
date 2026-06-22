using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception, IErrorMetadata
{
    public ForbiddenAccessException() : base() { }

    public ErrorCategory Category => ErrorCategory.Forbidden;

    public string ErrorCode => "forbidden-access";
}
