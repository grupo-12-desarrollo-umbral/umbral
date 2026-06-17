# Mission Runtime Restructure Grilling Decisions

Date: 2026-06-16

This document records the domain decisions made during the grilling session for restructuring live sessions around missions, substages, treasure hunt, and trivia. The service glossaries remain the canonical language source:

- `backend/services/mission-design-service/CONTEXT.md`
- `backend/services/session-operations-service/CONTEXT.md`

## Source And Snapshot Model

- `Mission` is the only source for a `LiveSession`.
- `TriviaQuiz` remains reusable authoring content in `MissionDesign`, but is no longer a `SessionSource`.
- A trivia `Substage` references an ordered `TriviaQuestionSelection` from one published `TriviaQuiz`.
- Selecting the entire quiz means selecting all questions in quiz order.
- A `LiveSession` snapshots the full mission runtime plan at creation time.
- The snapshot is immutable after `LiveSession` creation.
- `LiveSession` creation immediately creates the session in `Scheduled`.
- `Scheduled` is the initial `SessionState`; team association is only allowed while `Scheduled`. `Preparing` is the operator-readiness state entered from `Scheduled`.
- Target QR identifiers must be unique within a single `MissionRuntimeSnapshot`.

## Mission Hierarchy And Play Modes

- A `Mission` contains ordered `Stage`s.
- Each `Stage` contains ordered `Substage`s.
- Each `Substage` has exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`.
- Runtime order is strict mission order: stage order, then substage order inside each stage.
- Mixed-mode substages are not allowed.
- Operators cannot manually force substage advancement.

## Treasure Hunt

- Treasure hunt progression is target-based, not clue-based.
- `Target` is the QR-validated objective teams are trying to find or validate.
- All targets in the active treasure-hunt substage are active immediately.
- Teams may resolve targets in any order.
- A team can successfully resolve each target at most once.
- A team wins a treasure-hunt substage by being first to resolve all targets in that substage.
- When the treasure-hunt substage winner is decided, all teams advance to the next substage.
- The `TreasureHuntSubstageWinner` receives a snapshotted `ScoreValue`.
- Non-winning teams receive zero for that treasure-hunt substage.
- Treasure-hunt winner score is authored per treasure-hunt substage as `WinnerScoreValue`, integer 1-100.
- `WinnerScoreValue` is snapshotted into runtime.

## Clues

- `Clue` is optional player-facing guidance associated with a `Target`.
- Each target has at most one optional clue.
- Target resolution does not require clue visibility.
- A clue can be visible to all teams when the treasure-hunt substage starts, or hidden until operator release.
- Operator can release clues to all teams or specific teams.
- Clue visibility is tracked per team.
- Operator clue release is allowed, but it does not advance the substage.

## Trivia

- Trivia is synchronized.
- One active snapshotted question is presented to all teams during the same authoritative timer window.
- Question advancement is timer-driven.
- A trivia substage ends when the final snapshotted question timer expires.
- If another substage exists, all teams advance together.
- Each team gets one accepted answer per question.
- Later answer attempts by the same team for the same question are duplicates.
- Answers are accepted only during the active question timer window.
- Late answers are not accepted.
- Correct answers award the question snapshot's `ScoreValue`.
- Wrong or missing answers award zero.
- Trivia question `ScoreValue` is an integer 1-100, required before publishing, and snapshotted into `TriviaQuestionSnapshot`.
- `TriviaSubstageWinner` is any team with the highest trivia score in that substage after the final question timer expires.
- Trivia substage ties are allowed.

## Session Lifecycle

Canonical `SessionState`s:

- `Scheduled`
- `Preparing`
- `Active`
- `Paused`
- `Finished`
- `Cancelled`

Lifecycle rules:

- `LiveSession` is created in `Scheduled`; team association is only allowed while `Scheduled`.
- `Scheduled -> Preparing` moves the session into operator readiness.
- `Preparing -> Active` immediately starts the first substage.
- Pause flow is only `Active -> Paused -> Active`.
- While paused, target submissions and trivia answers are not accepted.
- During paused trivia, the active question timer stops and resumes on the same question.
- Cancellation is allowed from `Scheduled`, `Preparing`, `Active`, or `Paused`.
- `Finished -> Cancelled` is not allowed.
- `Cancelled -> anything` is not allowed.
- `Finished` only happens through normal `SessionCompletion` when the final substage completes.
- Early stop uses `Cancelled`, which does not calculate a winner.
- Cancelled sessions accept no more target submissions or trivia answers.
- Cancelled sessions do not advance substages.
- Cancelled sessions do not calculate `SessionTeamWinner`.
- Cancelled session score history remains visible for audit.

## Completion, Scoring, And Ranking

- Final substage completion automatically transitions the `LiveSession` to `Finished`.
- `SessionTeamWinner` is based on final ranking, not count of substages won.
- Score totals and ranking derive from traceable `ScoreEntry` records.
- Direct mutation of total score is not the canonical scoring model.
- Ranking is sorted from highest to lowest total score.
- If total score ties, lower comparable `ResolutionTime` ranks first.
- If `ResolutionTime` is not comparable or is equal, teams share rank.
- `ResolutionTime` means elapsed active play time from `LiveSession` activation until a team completes the final applicable objective used for ranking.
- Paused time does not count toward `ResolutionTime`.
- In synchronized trivia, resolution time often will not break ties because all teams share timer-driven completion.

## Mission Readiness

A mission is ready to create a live session only when all runtime content is ready.

General readiness:

- Every stage has at least one substage.
- Every substage has one `SubstagePlayMode`.

Treasure-hunt substage readiness:

- Has at least one target.
- Has `WinnerScoreValue` 1-100.
- Clues are optional.

Trivia substage readiness:

- Has a `TriviaQuestionSelection` from a published `TriviaQuiz`.
- Selection has at least one question.
- Every selected question has `ScoreValue`.
- Every selected question has `TimeLimitSeconds`.
- Every selected question has valid options and a correct answer.

## Documentation And Ticket Implications

The following stale assumptions must be removed from broader docs and tickets:

- `LiveSession` source can be either `Mission` or `TriviaQuiz`.
- `LiveSession` has a session-level `SessionMode`.
- Trivia sessions are created directly from published quizzes.
- Treasure-hunt progression is clue-based.
- `Target` is attached to `Clue` instead of being the playable objective.
- Hybrid mission/trivia sessions are invalid.

Docs likely needing follow-up rewrite:

- `backend/docs/ddd_solution_model.md`
- `backend/docs/bd_umbral_entity_spec.md`
- `backend/docs/umbral_user_stories.md`
- `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
- HU prompt/context docs that mention session-level trivia mode, trivia-session creation, clue-based progression, or `TriviaQuiz` as a session source.

Ticket implications:

- DES-24 needs rebuild from the corrected mission-wrapper/substage-play-mode model.
- DES-22 still assumes old mission-session creation semantics and needs review.
- DES-23 implemented old trivia-session creation directly from a published quiz and needs a rebuild or supersession path after docs align.

