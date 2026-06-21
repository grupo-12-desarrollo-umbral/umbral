# Plan — Close the "archived trivia quiz → empty trivia substage" readiness gap

> Discovered during HU-15 / DES-22 review (finding #2). The defect lives in
> **mission-design-service**, not in HU-15's session-operations code. HU-15 itself
> meets all its acceptance criteria; this plan hardens the upstream gate it relies on.

## Problem

A mission can pass the readiness/activation gate and produce a session whose trivia
substage has **zero questions**, surfacing only at play time as
`TriviaSubstageSnapshotMustContainQuestionsException`.

Repro: author selects a **published** quiz into a trivia substage (allowed), activates the
mission (allowed), then **archives** the quiz. The mission stays `Ready`; a new session is
created successfully; the runtime-plan silently returns an empty question set for that substage.

## Root cause (cited)

- `MissionActivationPolicy.EvaluateTrivia()` (`mission-design/Domain/Services/MissionActivationPolicy.cs:101-108`)
  only checks `substage.TriviaQuizId is null` — it has **no access** to `TriviaQuiz.Status`.
  Its docstring claims "selects a **published** trivia quiz" but the code never verifies publication.
- Publication is checked **only at selection time** (`TriviaQuizSelectionGuard.EnsurePublishedSelectionAsync`,
  called from `SetTriviaQuizSelectionCommandHandler` / `UpdateTriviaQuizSelectionCommandHandler`).
  Nothing re-validates when a quiz is later archived (`ArchiveTriviaQuizCommandHandler` raises
  `TriviaQuizArchivedEvent`, which has no handler that touches missions).
- `MissionReadModelRepository.LoadTriviaQuestionsByQuizIdAsync()` filters `Status == Published`
  (`MissionReadModelRepository.cs:87`) and `MapSubstage` falls back to `Array.Empty<>()` for an
  unresolved quiz (`:105-108`) — silent, no error.

## Load-bearing fact (why the fix is well-targeted)

session-operations' readiness gate **is** invoked and **does** reject on `IsReady == false`:

```
CreateSessionFacade.cs:41-44      → _sessionCreationPolicy.EnsureMissionEligible(id, IsActive, IsReady)
SessionCreationPolicy.cs:23-25    → if (!isReady) throw MissionNotEligibleForSessionCreationException(...)
```

So if mission-design's `MissionReadinessDto.IsReady` becomes `false` when a referenced quiz is
not published, **the session-creation path closes with no session-operations change.**

## Approach

**Core fix (Phase 1): make readiness publication-aware in mission-design's Application layer.**
The Domain policy stays pure/structural (it can't see other aggregates). The cross-aggregate
"is the referenced quiz still published" check belongs in the Application layer, where repository
access exists. Reuse one helper in both readiness and activation so they can't drift.

**Defense-in-depth (Phase 2, optional but recommended): fail fast at session creation.** Guard
the runtime-plan→snapshot build so a trivia substage that resolves to zero questions is rejected
at creation time (clear error) instead of at play time. This also covers the race where a quiz is
archived *between* the readiness check and the runtime-plan fetch.

**Out of scope (deferred): archive-time enforcement (Phase 3 idea).** Blocking quiz archival, or
cascading mission-deactivation, when a quiz is referenced by an active mission. This needs a
quiz→mission inverse query that does not exist today (`IMissionRepository` is CRUD-only) and is a
product decision about quiz lifecycle. File as a separate ticket; do not bundle here.

---

## Phase 1 — Publication-aware readiness (mission-design-service)

1. **Repository capability.** Add to `ITriviaQuizRepository` a batched status lookup, e.g.
   `Task<IReadOnlyDictionary<int, TriviaQuizStatus>> GetStatusesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)`.
   Implement in the EF repository with a single `Where(q => ids.Contains(q.Id)).Select(q => new { q.Id, q.Status })` read.

2. **Shared Application helper.** Add a small Application-layer checker (e.g.
   `MissionTriviaPublicationChecker`) that, given a `Mission` + the status map, returns a
   `IReadOnlyList<string>` of failures for every trivia substage whose `TriviaQuizId` is missing
   from the map or whose status `!= Published`. Message mirrors the existing readiness style.

3. **Wire into readiness.** In `GetMissionReadinessQueryHandler.Handle`, after
   `MissionActivationPolicy.EvaluateReadiness(mission)`:
   - collect every `substage.TriviaQuizId` (non-null) across `mission.Stages`,
   - fetch statuses via the new repo method,
   - append publication failures, then compute `IsReady = failures.Count == 0`.

   Note: stored `mission.ActivationState` may still read `Ready`; that's fine — the gate keys off
   the `IsReady` bool, and this gives correct *live* re-evaluation. Keep returning the stored
   `ActivationState` string unchanged.

4. **Wire into activation (consistency).** In `ActivateMissionCommandHandler` (or `Mission.Activate`
   via injected statuses), run the same publication check before activating, so a mission whose quiz
   was archived can't be (re)activated. Reuse the Phase-1 helper. Throw
   `MissionNotReadyForActivationException(failures)` as today.

## Phase 2 — Fail fast at session creation (session-operations-service, optional)

Pick **one** placement:
- **(preferred) Domain invariant:** in `MissionRuntimeSnapshot.Create`, reject a trivia substage
  that has zero question snapshots (new exception, e.g. `TriviaSubstageRequiresQuestionsException`).
  Turns the silent empty-set into an explicit failure at snapshot construction. Check existing
  domain tests that build snapshots — update fixtures that intentionally build empty trivia.
- **(alternative) Facade guard:** in `CreateSessionFacade`, after building the runtime DTO, throw
  if any trivia substage resolved empty. Less pure but localized.

This guard is what catches the readiness-check↔fetch race; Phase 1 alone leaves that small window.

---

## Test plan

mission-design:
- **Unit** — `GetMissionReadinessQueryHandler`: referenced quiz `Archived`/`Draft` ⇒ `IsReady == false`
  with a publication failure; all-published ⇒ unchanged green.
- **Unit** — `ActivateMissionCommandHandler`: archived referenced quiz ⇒ throws; published ⇒ activates.
- **Integration** — `GET /api/missions/{id}/readiness` reflects an archived quiz (seed published →
  activate → archive → assert `isReady=false`).

session-operations:
- **Integration** — `POST /api/sessions` rejected via the existing gate when readiness `IsReady=false`
  (extend the existing readiness-gate test in `CreateSessionEndpointTests`).
- **Phase 2** — snapshot/facade rejects a runtime plan whose trivia substage has zero questions.

Gates (this worktree): `make -C backend build` for both services + the three suites must stay green.

## Risks / notes

- One extra batched DB read per readiness/activation call — negligible, single query.
- **Behavior change:** missions that were "ready" while pointing at an archived quiz now report
  not-ready. Intended, but flag in the PR; could surface in any existing readiness fixtures.
- Confirm `Substage.TriviaQuizId` enumeration from the `Mission` aggregate matches how
  `MissionActivationPolicy` already walks `mission.Stages → substages` (it does).
- Two services change together → land as one PR (or coordinate), since session-operations
  integration tests stub `MissionReadinessSource` and won't catch a mission-design regression.

## Suggested sequencing

Phase 1 first (closes the create path), verify, then Phase 2 (defense-in-depth), verify. Phase 3
(archive-time enforcement) → separate Linear ticket against mission-design.
