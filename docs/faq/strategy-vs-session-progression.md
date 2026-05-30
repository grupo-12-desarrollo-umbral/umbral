# Strategy vs Session Progression

## Question

What does it mean that `Strategy` should not "invade" the progression owned by `SessionOperations`?

## Answer

It means `Strategy` is appropriate for rules that change how scoring is calculated, but it should not be used to move ownership of live-session progression out of `SessionOperations`.

The boundary is:

- `SessionOperations` owns what may happen during a live session and when it may happen.
- `ScoringMonitoring` owns how many points, penalties, or derived ranking effects result from facts that already happened.

This separation is already reflected in the canonical docs:

- `SessionOperations` owns live progression, clue release, evidence intake, and runtime state transitions.
- `LiveSession` lifecycle behavior is enforced through `State`.
- `ScoringMonitoring` uses `Strategy` for scoring variation such as difficulty-based weighting, mode-specific evaluation, and normalization rules.

## What belongs to `SessionOperations`

These are progression or runtime-control decisions:

- advancing to the next trivia question
- releasing the next clue after a target is resolved
- rejecting submissions while the session is paused
- deciding whether a session may move from `Preparing` to `Active`
- deciding whether a participant may still join

Those rules control session behavior and lifecycle, so they belong to `SessionOperations`, not `ScoringMonitoring`.

## What belongs to `ScoringMonitoring`

These are scoring-policy decisions:

- `easy` difficulty grants fewer points than `hard`
- trivia mode applies a speed multiplier
- treasure mode applies a different normalization rule
- a penalty reduces score only when justification requirements are met
- ranking ties are resolved through a scoring-specific tie-break rule

Those rules do not control the session runtime. They interpret outcomes and derive scoring consequences from them.

## Short Rule

`SessionOperations` decides what can happen and when in a live session.

`ScoringMonitoring` decides how scoring is computed from what already happened.
