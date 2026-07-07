namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Transport-agnostic classification of a domain or application error. The API layer maps
/// each category to an HTTP status; the Domain layer itself stays free of any HTTP concept.
/// </summary>
public enum ErrorCategory
{
    NotFound,
    Validation,
    Conflict,
    Forbidden,
    Unauthorized,
    Unprocessable,
    ServiceUnavailable
}
