namespace umbral_backend.Application.Common.Interfaces;

public interface IRankingSessionMembershipGuard
{
    Task EnsureAllowedAsync(Guid liveSessionId, Guid teamId, CancellationToken cancellationToken);
}
