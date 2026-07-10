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

    /// <summary>
    /// Optional client-safe explanation for the RFC 7807 <c>detail</c> field. Defaults to
    /// <see langword="null"/>: the exception message is treated as diagnostic-only and never
    /// reaches the client, because domain messages routinely interpolate identifiers
    /// (mission ids, quiz ids, question ids). An exception opts in by overriding this with a
    /// curated, identifier-free sentence when its explanation is safe to expose.
    /// </summary>
    string? PublicDetail => null;
}
