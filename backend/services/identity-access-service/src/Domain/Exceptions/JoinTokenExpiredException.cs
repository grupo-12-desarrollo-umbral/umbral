namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenExpiredException : DomainException
{
    public JoinTokenExpiredException(Guid joinTokenId, DateTimeOffset expiresAt, DateTimeOffset evaluatedAt)
        : base($"Join token '{joinTokenId}' expired at '{expiresAt:O}' and cannot be used at '{evaluatedAt:O}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
