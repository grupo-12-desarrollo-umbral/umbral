namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Persistence-state control the retry path needs without knowing the ORM.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Drops every tracked instance. A handler retried after a concurrency loss must re-read the
    /// ranking and its score entries from the database; without this it would be handed back the
    /// stale instances its failed attempt left tracked, and would recompute against the state it
    /// already lost on.
    /// </summary>
    void ResetTracking();
}
