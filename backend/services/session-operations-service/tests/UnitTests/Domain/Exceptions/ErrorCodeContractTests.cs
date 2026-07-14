using System.Runtime.CompilerServices;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Exceptions;

/// <summary>
/// Golden-file contract pinning every concrete <see cref="DomainException"/> to its public error code.
/// The code surfaces as the RFC 7807 <c>type</c> (ProblemDetails) and the SignalR error payload, so a
/// class rename or a changed <c>ErrorCode</c> override is a breaking API change. This test fails loudly
/// instead of letting that ship silently.
/// </summary>
public sealed class ErrorCodeContractTests
{
    // Expected class name -> public ErrorCode. Deliberately hand-pinned (NOT derived) so a rename that
    // changes the derived code is caught. Adding a new DomainException subclass is a one-line addition
    // here; removing/renaming one requires a deliberate edit — that is the whole point of the contract.
    private static readonly IReadOnlyDictionary<string, string> ExpectedErrorCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AnswerSubmitterIsNotSessionParticipantException"] = "answer-submitter-is-not-session-participant",
            ["ClueAlreadyReleasedToTeamException"] = "clue-already-released-to-team",
            ["ClueNotReleasableException"] = "clue-not-releasable",
            ["ClueSnapshotIdRequiredException"] = "clue-snapshot-id-required",
            ["ClueSnapshotSubstageRequiredException"] = "clue-snapshot-substage-required",
            ["DuplicateTeamAssociationInSessionException"] = "duplicate-team-association-in-session",
            ["DuplicateTeamCodeInSessionException"] = "duplicate-team-code-in-session",
            ["DuplicateTriviaAnswerException"] = "duplicate-trivia-answer",
            ["EvidenceAlreadyResolvedException"] = "evidence-already-resolved",
            ["EvidenceSubmissionContextRequiredException"] = "evidence-submission-context-required",
            // Overridden slug (does not follow the class name):
            ["InvalidSessionStateTransitionException"] = "invalid-state-transition",
            ["InvalidTriviaAnswerOptionException"] = "invalid-trivia-answer-option",
            ["JoinContextAlreadyClosedException"] = "join-context-already-closed",
            ["JoinContextExpirationInvalidException"] = "join-context-expiration-invalid",
            ["LateJoinNotAllowedException"] = "late-join-not-allowed",
            ["LateTriviaAnswerException"] = "late-trivia-answer",
            ["LiveSessionCodeRequiredException"] = "live-session-code-required",
            // Overridden slug:
            ["LiveSessionRequiresAtLeastOneTeamException"] = "session-no-teams",
            ["LiveSessionTitleRequiredException"] = "live-session-title-required",
            ["MaximumTimeMustBePositiveException"] = "maximum-time-must-be-positive",
            // Overridden slug:
            ["MissionNotEligibleForSessionCreationException"] = "mission-not-eligible-for-session",
            ["MissionRuntimeSnapshotMustContainStagesException"] = "mission-runtime-snapshot-must-contain-stages",
            ["MissionRuntimeSnapshotStageOrderInvalidException"] = "mission-runtime-snapshot-stage-order-invalid",
            ["MissionRuntimeSnapshotSubstageOrderInvalidException"] = "mission-runtime-snapshot-substage-order-invalid",
            ["MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException"] = "mission-runtime-snapshot-target-qr-codes-must-be-unique",
            ["NoActiveQuestionException"] = "no-active-question",
            ["NoActiveSubstageException"] = "no-active-substage",
            ["OpenTeamSelectionClosedException"] = "open-team-selection-closed",
            ["OperatorUserIdMustBePositiveException"] = "operator-user-id-must-be-positive",
            ["ParticipantAlreadyConnectedException"] = "participant-already-connected",
            ["ParticipantAssignedToDifferentTeamException"] = "participant-assigned-to-different-team",
            ["ParticipantDisplayNameRequiredException"] = "participant-display-name-required",
            ["ParticipantIdentityRequiredException"] = "participant-identity-required",
            ["ParticipantRemovedFromSessionException"] = "participant-removed-from-session",
            ["QuestionActivationRequiresActiveSessionException"] = "question-activation-requires-active-session",
            ["QuestionAlreadyActiveException"] = "question-already-active",
            ["QuestionIndexOutOfRangeException"] = "question-index-out-of-range",
            ["ReferenceTeamIdRequiredException"] = "reference-team-id-required",
            ["SessionSourceEntityRequiredException"] = "session-source-entity-required",
            ["SessionNotActiveForClueReleaseException"] = "session-not-active-for-clue-release",
            ["StageSnapshotIdRequiredException"] = "stage-snapshot-id-required",
            ["StageSnapshotMustContainSubstagesException"] = "stage-snapshot-must-contain-substages",
            ["SubstageAdvancementRequiresActiveSessionException"] = "substage-advancement-requires-active-session",
            ["SubstageSnapshotIdRequiredException"] = "substage-snapshot-id-required",
            ["TargetSnapshotIdRequiredException"] = "target-snapshot-id-required",
            ["TargetSnapshotSubstageRequiredException"] = "target-snapshot-substage-required",
            ["TeamAssociationRequiresScheduledSessionException"] = "team-association-requires-scheduled-session",
            ["TeamCapacityMustBePositiveException"] = "team-capacity-must-be-positive",
            ["TeamCapacityReachedException"] = "team-capacity-reached",
            ["TeamCodeRequiredException"] = "team-code-required",
            ["TeamDisplayNameRequiredException"] = "team-display-name-required",
            ["TeamIdentityRequiredException"] = "team-identity-required",
            ["TeamJoinClosedException"] = "team-join-closed",
            ["TeamNotFoundException"] = "team-not-found",
            ["TeamNotInAuthorizedSetException"] = "team-not-in-authorized-set",
            ["TreasureHuntSubstageSnapshotMustContainTargetsException"] = "treasure-hunt-substage-snapshot-must-contain-targets",
            ["TargetSnapshotScoreMustBePositiveException"] = "target-snapshot-score-must-be-positive",
            ["TriviaAnswerRequiresActiveQuestionException"] = "trivia-answer-requires-active-question",
            ["TriviaAnswerRequiresActiveSessionException"] = "trivia-answer-requires-active-session",
            ["TriviaAnswerRequiresTriviaSubstageException"] = "trivia-answer-requires-trivia-substage",
            ["TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException"] = "trivia-question-snapshot-requires-at-least-two-options",
            ["TriviaQuestionSnapshotRequiresCorrectOptionException"] = "trivia-question-snapshot-requires-correct-option",
            // Overridden slug:
            ["TriviaSubstageSnapshotMustContainQuestionsException"] = "trivia-substage-empty",
        };

    private static IReadOnlyDictionary<string, string> ActualErrorCodes()
    {
        var baseType = typeof(DomainException);
        return baseType.Assembly
            .GetTypes()
            .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
            // Read ErrorCode without running a constructor: arities vary and no override reads ctor state
            // (every override returns a constant literal; the default reads only GetType().Name).
            .ToDictionary(
                type => type.Name,
                type => ((DomainException)RuntimeHelpers.GetUninitializedObject(type)).ErrorCode,
                StringComparer.Ordinal);
    }

    [Fact]
    public void EveryConcreteDomainException_MapsToItsPinnedErrorCode()
    {
        var actual = ActualErrorCodes();

        actual.Should().BeEquivalentTo(ExpectedErrorCodes);
    }

    [Fact]
    public void ErrorCodes_AreUniqueAcrossExceptions()
    {
        var actual = ActualErrorCodes();

        var duplicates = actual
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        duplicates.Should().BeEmpty("each DomainException must expose a distinct public error code");
    }
}
