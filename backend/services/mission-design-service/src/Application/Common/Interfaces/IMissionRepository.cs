using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionRepository
{
    Task<Mission?> GetByIdAsync(int missionId, CancellationToken cancellationToken);

    /// <summary>
    /// Quiz→missions inverse query: the active (Ready) missions whose trivia substages select
    /// <paramref name="triviaQuizId"/>. Used by archive-time enforcement to block archiving a
    /// quiz that a live mission still depends on.
    /// </summary>
    Task<IReadOnlyList<ActiveMissionReference>> GetActiveMissionsReferencingTriviaQuizAsync(
        int triviaQuizId,
        CancellationToken cancellationToken);

    Task AddAsync(Mission mission, CancellationToken cancellationToken);

    Task UpdateAsync(Mission mission, CancellationToken cancellationToken);
}
