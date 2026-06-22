using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Missions.Common;

/// <summary>
/// Archive-time enforcement counterpart to <see cref="TriviaQuizSelectionGuard"/>: selection
/// guarantees a quiz is published when chosen, this guard guarantees it cannot be archived out
/// from under an active mission. Together they keep an active mission from ever resolving to a
/// non-published (e.g. empty) trivia substage at session/play time.
/// </summary>
internal static class ActiveMissionTriviaReferenceGuard
{
    public static async Task EnsureNotReferencedByActiveMissionAsync(
        IMissionRepository missionRepository,
        int triviaQuizId,
        CancellationToken cancellationToken)
    {
        var references = await missionRepository.GetActiveMissionsReferencingTriviaQuizAsync(
            triviaQuizId,
            cancellationToken);

        if (references.Count > 0)
        {
            throw new TriviaQuizReferencedByActiveMissionException(
                triviaQuizId,
                references.Select(reference => $"'{reference.Name}' (#{reference.MissionId})").ToList());
        }
    }
}
