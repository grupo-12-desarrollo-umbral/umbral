namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenExpiredException : Exception
{
    public JoinTokenExpiredException(Guid joinTokenId, DateTimeOffset expiresAt, DateTimeOffset evaluatedAt)
        : base($"Join token '{joinTokenId}' expired at '{expiresAt:O}' and cannot be used at '{evaluatedAt:O}'.")
    {
    }
}
