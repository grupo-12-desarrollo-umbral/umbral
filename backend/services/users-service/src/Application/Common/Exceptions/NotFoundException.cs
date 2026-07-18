using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Common.Exceptions;

public sealed class NotFoundException : Exception, IErrorMetadata
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }

    public ErrorCategory Category => ErrorCategory.NotFound;

    public string ErrorCode => "not-found";
}
