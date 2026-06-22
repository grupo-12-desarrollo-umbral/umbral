namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Raised when archiving a trivia quiz is rejected because the quiz is still selected by
/// one or more active (Ready) missions. Archival is blocked at its source so an active
/// mission can never silently point at a non-published quiz (the "published-quiz readiness
/// gap"). The operator must first swap the trivia selection or deactivate the mission.
/// </summary>
public sealed class TriviaQuizReferencedByActiveMissionException : Exception
{
    public TriviaQuizReferencedByActiveMissionException(
        int triviaQuizId,
        IReadOnlyCollection<string> activeMissionReferences)
        : base($"Trivia quiz {triviaQuizId} cannot be archived because it is referenced by active mission(s): "
            + string.Join("; ", activeMissionReferences)
            + ". Deactivate the mission or change its trivia selection before archiving the quiz.")
    {
        TriviaQuizId = triviaQuizId;
        ActiveMissionReferences = activeMissionReferences;
    }

    public int TriviaQuizId { get; }

    public IReadOnlyCollection<string> ActiveMissionReferences { get; }
}
