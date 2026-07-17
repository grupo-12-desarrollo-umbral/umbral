# Archiving a trivia quiz is blocked while it is referenced by an active mission

Phase 3 of the "published-quiz readiness gap" remediation (DES-79, follow-up to HU-15/DES-22) closes the gap at its source: an operator could select a **published** quiz into a trivia substage, activate the mission, then **archive** the quiz, leaving the active mission pointing at a non-published quiz that resolves to zero questions only at play time (`TriviaSubstageSnapshotMustContainQuestionsException`). Phases 1 & 2 react downstream (readiness re-evaluates publication; snapshot build rejects an empty trivia substage). Phase 3 prevents the dead reference from ever being created.

The PRD deferred this with an explicit product decision required — **(A) block archival** while a quiz is referenced by any active mission, or **(B) cascade** (archive anyway and auto-deactivate affected missions). This ADR fixes the decision as **(A) block**.

## Status

accepted

## Considered Options

- **(A) Block archival (chosen).** Moving a quiz referenced by any active (`Ready`) mission to `Archived` — by archival or by retirement, which shares the same terminal state — is rejected (`409 Conflict`, `TriviaQuizReferencedByActiveMissionException`); the operator must first change the trivia selection or deactivate the mission, then archive. Symmetric with the existing `TriviaQuizSelectionGuard`, which already *blocks* selecting a non-published quiz — selection guarantees published-at-choose-time, archival enforcement guarantees it stays published while live. Atomic and side-effect-free: the archive operation touches only the quiz, and missions are never silently mutated.
- **(B) Cascade deactivate.** Rejected. Allowing archival to silently move other operators' live missions out of `Ready` is a surprising cross-aggregate side effect, and the "and notify" half of the option has no infrastructure yet (it would degrade to an un-consumed domain event). The reactive Phases 1 & 2 already cover the narrower pre-activation window where a quiz legitimately changes state.

## Consequences

- A new quiz→missions **inverse query** `IMissionRepository.GetActiveMissionsReferencingTriviaQuizAsync` returns the `Ready` missions whose trivia substages select a given quiz. "Active" is the `Ready` activation state — the only state from which a session can be created against the mission.
- Every command that lands a quiz on `Archived` consults the query via `ActiveMissionTriviaReferenceGuard` **before** the domain transition. On a hit it throws `TriviaQuizReferencedByActiveMissionException` (mapped to `409 Conflict`) and the quiz is never modified or persisted.
- The guarded commands are `ArchiveTriviaQuizCommandHandler` (`POST /api/trivias/{id}/archive`) and `RetireTriviaQuizCommandHandler` (`POST /api/trivias/{id}/retire`). Retirement is HU-13's used-quiz entry point into the same withdrawal: `RetireFromFutureUse` adds a usage-history precondition and then delegates to the same `ArchiveTemplate`, reaching the same `Archived` status and the same `TriviaQuizArchivedEvent`. It therefore carries the same reference check — the rule is "a quiz referenced by an active mission cannot reach `Archived`", not "the archive endpoint rejects". Retire is in fact the *more* exposed path, since it only applies to quizzes that have already been used in a session.
- Each handler calls the guard inline. An earlier revision of this ADR specified an `EnsureTransitionAllowedAsync` hook on a `TriviaQuizLifecycleCommandHandler` base class; that base class was removed by the Phase 1 Application-layer CQRS refactor, and the hook no longer exists. The placement decision below is what matters — the guard runs command-side, before the transition — not the mechanism that carries it.
- Enforcement is **command-side**, not via the `TriviaQuizArchivedEvent` consumer: a post-transition event handler cannot block an already-applied archive. `TriviaQuizArchivedEvent` keeps its current (no mission-touching) behavior.
- The operator escape hatch is to deactivate the mission (`DELETE /api/missions/{id}`) or swap the trivia selection; once the mission leaves `Ready`, archival proceeds.
- Reactive readiness (Phase 1/2) still matters for the pre-activation window: a quiz can be archived while its referencing mission is still `Draft`, and readiness then demotes the mission. The prior integration test was retargeted to that case; new integration tests cover blocked-while-`Ready` and allowed-after-deactivation.
