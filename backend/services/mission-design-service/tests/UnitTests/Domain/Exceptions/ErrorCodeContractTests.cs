using System.Runtime.CompilerServices;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Exceptions;

/// <summary>
/// Golden-file contract pinning every concrete <see cref="DomainException"/> to its public error code.
/// The code surfaces as the RFC 7807 <c>type</c> (ProblemDetails), so a class rename or a changed
/// <c>ErrorCode</c> override is a breaking API change. This test fails loudly instead of letting that
/// ship silently. Complements <see cref="DomainExceptionCoverageTests"/> (which pins behaviour/messages).
/// </summary>
public sealed class ErrorCodeContractTests
{
    // Expected class name -> public ErrorCode. Deliberately hand-pinned (NOT derived) so a rename that
    // changes the derived code is caught. Adding a new DomainException subclass is a one-line addition
    // here; removing/renaming one requires a deliberate edit — that is the whole point of the contract.
    private static readonly IReadOnlyDictionary<string, string> ExpectedErrorCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ClueMustBelongToSameSubstageException"] = "clue-must-belong-to-same-substage",
            ["DifficultyValueRequiredException"] = "difficulty-value-required",
            ["InvalidDifficultyValueException"] = "invalid-difficulty-value",
            ["InvalidMissionNodeChildException"] = "invalid-mission-node-child",
            ["MaximumTimeMustBePositiveException"] = "maximum-time-must-be-positive",
            ["MissionAlreadyActiveException"] = "mission-already-active",
            ["MissionAlreadyDeactivatedException"] = "mission-already-deactivated",
            ["MissionDescriptionRequiredException"] = "mission-description-required",
            ["MissionNameRequiredException"] = "mission-name-required",
            ["MissionNodeNotFoundException"] = "mission-node-not-found",
            ["MissionNodeSequenceOrderMustBePositiveException"] = "mission-node-sequence-order-must-be-positive",
            ["MissionNodeTitleRequiredException"] = "mission-node-title-required",
            ["MissionNotReadyForActivationException"] = "mission-not-ready-for-activation",
            ["QuestionTimerExceedsMaximumException"] = "question-timer-exceeds-maximum",
            ["QuestionTimerMustBePositiveException"] = "question-timer-must-be-positive",
            ["ScoreValueExceedsMaximumException"] = "score-value-exceeds-maximum",
            ["ScoreValueMustBeMultipleOfTenException"] = "score-value-must-be-multiple-of-ten",
            ["ScoreValueMustBePositiveException"] = "score-value-must-be-positive",
            ["SubstagePlayModeMismatchException"] = "substage-play-mode-mismatch",
            ["SubstageRequiresPlayModeException"] = "substage-requires-play-mode",
            ["TargetLatitudeOutOfRangeException"] = "target-latitude-out-of-range",
            ["TargetLongitudeOutOfRangeException"] = "target-longitude-out-of-range",
            ["TargetMayReferenceAtMostOneClueException"] = "target-may-reference-at-most-one-clue",
            ["TargetNameRequiredException"] = "target-name-required",
            ["TargetNotFoundException"] = "target-not-found",
            ["TargetQrCodeRequiredException"] = "target-qr-code-required",
            ["TargetSequenceOrderMustBePositiveException"] = "target-sequence-order-must-be-positive",
            ["TriviaOptionSequenceOrderMustBePositiveException"] = "trivia-option-sequence-order-must-be-positive",
            ["TriviaOptionSequenceOrderMustBeUniqueException"] = "trivia-option-sequence-order-must-be-unique",
            ["TriviaOptionTextRequiredException"] = "trivia-option-text-required",
            ["TriviaQuestionMustHaveBetweenTwoAndFourOptionsException"] = "trivia-question-must-have-between-two-and-four-options",
            ["TriviaQuestionMustHaveExactlyOneCorrectOptionException"] = "trivia-question-must-have-exactly-one-correct-option",
            ["TriviaQuestionNotFoundException"] = "trivia-question-not-found",
            ["TriviaQuestionPromptRequiredException"] = "trivia-question-prompt-required",
            ["TriviaQuestionScoreValueExceedsMaximumException"] = "trivia-question-score-value-exceeds-maximum",
            ["TriviaQuestionScoreValueMustBePositiveException"] = "trivia-question-score-value-must-be-positive",
            ["TriviaQuestionScoreValueRequiredToPublishException"] = "trivia-question-score-value-required-to-publish",
            ["TriviaQuestionTimeLimitRequiredToPublishException"] = "trivia-question-time-limit-required-to-publish",
            ["TriviaQuizCannotBeArchivedInCurrentStateException"] = "trivia-quiz-cannot-be-archived-in-current-state",
            ["TriviaQuizCannotBeDestructivelyRemovedAfterUsageException"] = "trivia-quiz-cannot-be-destructively-removed-after-usage",
            ["TriviaQuizCannotBePublishedInCurrentStateException"] = "trivia-quiz-cannot-be-published-in-current-state",
            ["TriviaQuizCannotBeRetiredWithoutUsageHistoryException"] = "trivia-quiz-cannot-be-retired-without-usage-history",
            ["TriviaQuizDescriptionRequiredException"] = "trivia-quiz-description-required",
            ["TriviaQuizMustHaveAtLeastOneQuestionToPublishException"] = "trivia-quiz-must-have-at-least-one-question-to-publish",
            ["TriviaQuizNotEditableException"] = "trivia-quiz-not-editable",
            ["TriviaQuizReferencedByActiveMissionException"] = "trivia-quiz-referenced-by-active-mission",
            ["TriviaQuizTitleRequiredException"] = "trivia-quiz-title-required",
        };

    private static IReadOnlyDictionary<string, string> ActualErrorCodes()
    {
        var baseType = typeof(DomainException);
        return baseType.Assembly
            .GetTypes()
            .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
            // Read ErrorCode without running a constructor: arities vary and no override reads ctor state
            // (none override ErrorCode here; the default reads only GetType().Name).
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
