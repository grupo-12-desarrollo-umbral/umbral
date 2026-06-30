using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Resolves the authenticated <see cref="User"/> behind the current request.
/// Scoped + memoized: the actor row is read at most once per request, no matter
/// how many slices (proxy + inner service + handler) ask for it.
/// </summary>
public interface ICurrentActor
{
    Task<User> GetActorAsync(CancellationToken cancellationToken);
}
