# Clues on a trivia substage are substage-scoped guidance, never per-question

A `Clue` under a trivia `Substage` applies to the substage as a whole, not to any individual question of the selected quiz. There are **no per-question clues**: `Substage.TriviaQuizId` (`services/mission-design-service/src/Domain/Entities/Substage.cs:42`) is a whole-quiz reference by identity — the entire quiz is selected in authored order — and a clue never targets a `TriviaQuestion`. Both `ClueVisibilityPolicy` values keep working in a substage with no targets, and neither ever advances or resolves anything: clues are guidance only, exactly as they already are in treasure hunt. This settles the "correct" behavior for the runtime-plan bug where substage-level trivia clues are dropped from the snapshot (issue #145).

## Status

accepted

## Considered Options

- **Per-question clues (rejected).** Attaching a clue to a single question of the selected quiz was raised in design discussion. Rejected on two independent grounds:
  - **Aggregate boundary.** ADR-0002 (`0002-trivia-question-score-and-timer-ranges.md`) and ADR-0003 (`0003-archive-time-enforcement-for-quizzes-referenced-by-active-missions.md`) treat `TriviaQuiz` as a separate aggregate that `Mission` references only by identity through a `TriviaQuizSelection`. A clue pointing at a `TriviaQuestion` would make `Mission` hold a reference into `TriviaQuiz`'s internals, contradicting `CONTEXT.md` (**TriviaQuizSelection**, "partial question subset" listed under _Avoid_) and the `Substage` XML doc ("The whole quiz is selected").
  - **No stable per-question identity in the snapshot.** The session-operations snapshot has no durable per-question key: `TriviaQuestionSnapshot` (`services/session-operations-service/src/Domain/ValueObjects/TriviaQuestionSnapshot.cs`) is a value object with equality over `(SubstageSnapshotId, SequenceOrder)` and no identity. `SequenceOrder` is not stable across a quiz edit, so there is nothing durable for a clue to point at across the authoring→runtime boundary.
- **Substage-scoped guidance (chosen).** A clue on a trivia substage is guidance for the whole substage, consistent with how clues already work in treasure hunt (where a `Target` may reference at most one `Clue`, guarded by `ClueMustBelongToSameSubstageException`, but the clue itself resolves nothing).

## Consequences

- **Both `ClueVisibilityPolicy` values are meaningful in a substage with no targets** (`services/mission-design-service/src/Domain/Enums/ClueVisibilityPolicy.cs`):
  - `VisibleWhenSubstageStarts` — the clue is shown to all participant teams when the trivia substage becomes active.
  - `HiddenUntilOperatorRelease` — the clue stays hidden until the operator releases it mid-round.
  - Neither value ever advances the substage, resolves a question, or affects scoring. Substage advancement stays timer-driven and generic across play modes (`0005-substage-advancement-pointer-and-timer-driven-orchestration.md`); clues do not participate in it.
- **The runtime plan must carry substage-level clues for trivia substages**, not just treasure-hunt ones. Dropping them (issue #145) is now a well-defined defect: the snapshot must include a trivia substage's clues so the two visibility policies can be honored at play time.
- **The authoring UI should scope clues to the substage, not to a question.** `frontend/app/dashboard/mission/MissionTree.tsx` renders the Clues section outside the play-mode conditional; that is correct for trivia (clues are allowed) — the fix is to give those clues runtime meaning, not to hide the section.
