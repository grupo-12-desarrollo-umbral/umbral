using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class ForbiddenAccessException : Exception, IErrorMetadata
{
    public ErrorCategory Category => ErrorCategory.Forbidden;

    public string ErrorCode => "forbidden-access";
}
