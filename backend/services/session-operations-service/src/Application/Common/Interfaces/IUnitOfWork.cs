namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Persistence-state control the retry path needs without knowing the ORM.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Drops every tracked instance. A handler retried after a concurrency loss must re-read the
    /// aggregate from the database; without this it would be handed back the stale instance its
    /// failed attempt left tracked, and would re-decide against the state it already lost on.
    /// </summary>
    void ResetTracking();
}
