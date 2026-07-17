using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Opens a trivia question: persist the activation and push QuestionActivated. Extracted from
/// <see cref="TriviaRoundOrchestratorFacade"/> so <see cref="ISubstageAdvanceCoordinator"/> can
/// activate the first question of a trivia substage it just advanced into without depending on the
/// facade — which depends on the coordinator in turn, and would cycle.
/// </summary>
public interface IQuestionActivator
{
    /// <summary>
    /// Activates the substage's next question, or no-ops when one is already active or the substage
    /// holds none (a treasure hunt always holds none).
    /// </summary>
    Task ActivateNextQuestionAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ActivateQuestionAsync(
        LiveSession session,
        int questionIndex,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
