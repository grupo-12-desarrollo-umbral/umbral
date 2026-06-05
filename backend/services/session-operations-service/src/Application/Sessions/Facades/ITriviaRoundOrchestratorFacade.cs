using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Facades;

public interface ITriviaRoundOrchestratorFacade
{
    Task ActivateNextQuestionAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task CloseAndAdvanceAsync(
        LiveSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
