namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Metadata the ProblemDetails handler matches on so it never has to enumerate concrete
/// exception types. Any exception carrying this contract maps itself to an HTTP status and a
/// stable, machine-readable error code.
/// </summary>
public interface IErrorMetadata
{
    /// <summary>Semantic category the API layer uses to derive the HTTP status.</summary>
    ErrorCategory Category { get; }

    /// <summary>Stable kebab-case slug clients can branch on (RFC 7807 <c>type</c>).</summary>
    string ErrorCode { get; }
}
