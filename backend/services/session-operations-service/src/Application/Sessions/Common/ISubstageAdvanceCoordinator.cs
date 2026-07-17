using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Mode-agnostic substage endings (D-3). Advancement used to live inside
/// <see cref="TriviaRoundOrchestratorFacade"/> and was therefore reachable only from trivia question
/// exhaustion — the root cause of the mixed-mission hang this spec closes. It lives here instead so a
/// treasure hunt cleared by its first team (D-1) and a trivia substage out of questions are the same
/// path.
/// </summary>
public interface ISubstageAdvanceCoordinator
{
    /// <summary>
    /// Opens the 10s ranking reveal on the active substage and broadcasts it. Idempotent — a second
    /// caller (two teams clearing in the same tick) no-ops without restarting the window.
    /// </summary>
    Task BeginRankingRevealAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the reveal and either advances to the next substage or finishes the mission on it.
    /// Driven by the timer worker once the reveal deadline elapses. Idempotent.
    /// </summary>
    Task CompleteRankingRevealAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ends the mission where it stands because MaximumTime ran out (D-4). No reveal — the finished
    /// screen is already the ranking.
    /// </summary>
    Task FinishOnMissionDeadlineAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
