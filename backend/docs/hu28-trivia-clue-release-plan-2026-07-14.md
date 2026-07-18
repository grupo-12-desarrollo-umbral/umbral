# HU-28 — Releasing hidden clues on trivia substages: implementation plan — 2026-07-14

**Branch**: `feature/hu-28-operative-clues` · **Base commit**: `b55357b` · **Status**: plan only, nothing implemented

Supersedes the "Suspected gap" section of
`hu28-operator-clue-release-seed-handoff-2026-07-14.md`, which is now **confirmed** by code
reading (still not reproduced live — the seeds do not create the shape).

---

## The problem in one paragraph

A `HiddenUntilOperatorRelease` clue authored on a **trivia** substage is dead data. It is
snapshotted into the live session correctly and then can never reach a participant:
`ProjectReleasableTargets()` won't list it (treasure-hunt gate, and it projects from
`TargetSnapshots`), `ResolveReleasableTarget()` won't resolve it (same gate), and
`CollectSubstageVisibleClues()` won't project it (filters on `IsVisibleWhenSubstageStarts`).
No UI path exists, and none can be built, because **release resolution is keyed on a target**
and a trivia substage has no targets.

## Why this is a realignment, not a feature

The canon already models release as **clue-keyed**. The target-keying is an implementation
narrowing that drifted from the spec.

- `docs/bd_umbral_entity_spec.md:682` — `clueId`: "Released clue reference".
- `docs/bd_umbral_entity_spec.md:683` — `targetId`: "**Target whose optional clue** became visible"
  (i.e. `targetId` is the *contextual* field; the clue is the subject).
- `docs/bd_umbral_entity_spec.md:692` — "one `ClueReleaseRecord` references exactly **one clue snapshot**".
- `docs/bd_umbral_entity_spec.md:696` — "the same **clue** should not be released twice to the same
  `Team` in the same `LiveSession`".
- `services/session-operations-service/CONTEXT.md:88` — "The traceable record that **one `Clue`**
  became visible to a specific `Team` in one `LiveSession`".

Note the divergence at `LiveSessionConfiguration.cs:570`: its comment cites **spec L696** as the
authority for a unique index on `(LiveSessionId, TeamId, TargetId)`. L696 says *clue*, not
*target*. The index enforces a narrower rule than the spec states.

Per `canon-realignment-workflow.md`, the authority chain is **canon docs > tracker AC > existing
code**. Canon is unambiguous here, so the plan follows canon.

## What is already in place (do not rebuild)

The groundwork exists and is unused:

| Asset | Location | State |
|---|---|---|
| Substage-scoped clue snapshots | `ClueSnapshot` (keyed by `SubstageSnapshotId`) | Populated |
| Every authored clue, **any policy**, snapshotted | `CreateSessionCommandHandler.cs:97` — self-described "clue superset" | Populated |
| Stable per-clue identity | `ClueSnapshot.ClueSnapshotId` | Populated |
| Nullable clue key on the release record | `ClueReleaseRecord.ClueId` | **Always `null`** — reserved, never written |
| Persisted `clue_id` column | `LiveSessionConfiguration.cs:555` | Exists, always null |
| Clue id on the domain event | `ClueReleasedEvent.ClueId` | Exists, always null |
| Board projection for target-less clues | `VisibleClue.CreateForSubstage` | Used by the visible-when-starts path |

So no new persistence concept is needed. The work is to **start using `ClueId`** as a release
key and to teach the trivia projection about releases.

## Scope

**In scope** — an operator can release a `HiddenUntilOperatorRelease` clue on an active trivia
substage to one team or all teams, and it appears on those teams' boards.

**Non-goals**

- Per-team filtering of the picker (the known "still lists an already-released target" behaviour).
  Pre-existing HU-26 behaviour, correct for multi-team sessions. Out of scope; see
  the docstring fix in Phase 5.
- Any change to treasure-hunt release behaviour, ordering, or payloads.
- Operative clues (`AddOperativeClue`) — already work on trivia, untouched.
- Backfilling `ClueId` for existing treasure-hunt release rows (impossible: `TargetSnapshot`
  embeds clue text and carries no clue-node identity — `LiveSession.cs:1162`).

## Design decisions

> **Resolved 2026-07-14** — all four decisions settled at the recommended option:
> **D-1** exactly-one-of subject · **D-2** `ClueReleaseSubject` value object ·
> **D-3** `TargetName: null` + `SequenceOrder` (frontend renders `Pista {n}`) ·
> **D-4** add `ClueSnapshotId` to the substage board factory. Phase 1 is unblocked.

### D-1 — Key a release on an exactly-one-of subject (recommended) — ✅ chosen

`ClueReleaseRecord.TargetId` becomes `Guid?`. A record carries **exactly one** of:

- `TargetId` — a treasure-hunt target's embedded clue (unchanged path, `ClueId` stays null), or
- `ClueId` — a substage-scoped `ClueSnapshot.ClueSnapshotId` (new trivia path, `TargetId` null).

This preserves treasure hunt **by construction**: its rows, its resolution path, its uniqueness
index, and its board projection are all untouched. The trivia branch is strictly additive.

*Alternative rejected* — give `TargetSnapshot`'s embedded clue its own `ClueSnapshotId` and key
everything on `ClueId`. Cleaner as one uniform key, but it rewrites the treasure-hunt path and
cannot backfill existing sessions, whose `MissionRuntimeSnapshot` is immutable by design
(`CONTEXT.md:76`). Rejected as a violation of the "maintain treasure-hunt behaviour" constraint.

### D-2 — Model the subject as a value object (recommended) — ✅ chosen

Introduce `ClueReleaseSubject` (Domain/ValueObjects) with `ForTarget(Guid)` /
`ForSubstageClue(Guid)` factories, owning the exactly-one-of invariant. `LiveSession` keeps two
public release methods (`ReleaseClueToTeam`, `ReleaseClueToAllTeams`), each taking the subject.

*Alternative* — four explicit methods (`ReleaseTargetClueToTeam`, `ReleaseSubstageClueToTeam`,
and their all-teams twins). No new concept, but it grows an already-large aggregate's public
surface and pushes the branch into the Application layer. The VO better serves the Open/Closed
guidance in `.agents/backend-agent.md`.

**Decide before Phase 1.** The rest of the plan assumes the VO.

### D-3 — Picker label: send data, not prose (recommended) — ✅ chosen

`ClueSnapshot` carries **no title** — `CreateSessionCommandHandler.cs:102` snapshots only
`Text`, `VisibilityPolicy`, `SequenceOrder`. The authoring `Clue` has a `Title` (it is a
`MissionNode`), but it is dropped at snapshot time.

The treasure-hunt picker labels each row with `target.Name`. A trivia clue has no equivalent.
Rather than invent a label in the domain, send `TargetName: null` + `SequenceOrder` and let the
frontend render `Pista {n}`. Keeps UI Spanish out of the Domain layer, and `ClueText` — which
the picker already displays — carries the real meaning.

*Alternative* — add `Title` to `ClueSnapshot`. Better labels, but it changes
`MissionRuntimeSnapshot`, needs a migration, and yields blank titles for every pre-existing
session (immutable snapshots). Defer; not needed for this fix.

### D-4 — Give substage clues identity on the board (recommended) — ✅ chosen

`VisibleClue.CreateForSubstage(clueText)` produces an item with `TargetSnapshotId = null` and
`OperativeClueId = null` — no stable id at all. Once trivia clues can be released per-team, the
board needs to distinguish them. Add `ClueSnapshotId` to the substage factory.

**Contract impact**: this widens the participant board payload. `mobile/src/lib/realtime/
team-board-types.ts` is being edited by a concurrent session — coordinate before touching.

---

## The trap: `CountVisibleSubstageInitialClues` must NOT become release-aware

`ProjectOperatorSessionPanel` computes `substageInitialClueCount` **once, outside the per-team
loop** (`LiveSession.cs:836`) and adds it to every team's `ReleasedClueCount`
(`LiveSession.cs:855`). That is only sound because substage-initial clues are currently
team-independent.

It stays sound after this change **only if** `CountVisibleSubstageInitialClues` keeps filtering
strictly on `IsVisibleWhenSubstageStarts`. The two counts are then disjoint sets:

- released hidden clues → already counted by `team.ReleasedClueCount`, which
  `AppendManualClueRelease` increments via `team.IncrementReleasedClueCount()`;
- visible-when-starts clues → counted by `substageInitialClueCount`.

If `CountVisibleSubstageInitialClues` is "helpfully" pointed at the new release-aware collector,
**every released trivia clue is counted twice**. Its comment at `LiveSession.cs:869` currently
claims it "Mirrors the source-of-truth `CollectSubstageVisibleClues`" — that mirror breaks
deliberately in Phase 1 and the comment must be rewritten to say why.

`LiveSession.cs`, `OperatorTeamProgress.cs`, `OperatorSessionPanelDto.cs` and
`OperatorSessionPanelDtoFactory.cs` all carry **uncommitted concurrent work from another
session** (see the handoff, "Working tree"). Rebase/coordinate before starting Phase 1, and
cite domain methods by name — line numbers in this doc will drift.

---

## Phases

Follows the layer convention in `.agents/backend-agent.md` (X.1 Domain → X.4 Api). One phase,
one layer. `make -C backend build SVC=session-operations-service` after each.

### Phase 1 — Domain

| File | Change |
|---|---|
| `Domain/ValueObjects/ClueReleaseSubject.cs` | **New.** `ForTarget` / `ForSubstageClue`; owns exactly-one-of. |
| `Domain/Exceptions/ClueReleaseSubjectInvalidException.cs` | **New.** `ErrorCategory.Validation`. |
| `Domain/ValueObjects/ClueSnapshot.cs` | Add `HiddenUntilOperatorReleasePolicy` const + `IsHiddenUntilOperatorRelease`. |
| `Domain/ValueObjects/ReleasableClue.cs` | **New**, replaces `ReleasableTarget`: `TargetId Guid?`, `ClueId Guid?`, `TargetName string?`, `SequenceOrder`, `ClueText`. |
| `Domain/ValueObjects/ReleasableTarget.cs` | **Delete** once callers move. |
| `Domain/ValueObjects/VisibleClue.cs` | `CreateForSubstage(clueSnapshotId, clueText)` — D-4. |
| `Domain/Entities/ClueReleaseRecord.cs` | `TargetId` → `Guid?`; `CreateManual` takes `ClueReleaseSubject`. |
| `Domain/Events/ClueReleasedEvent.cs` | `TargetId` → `Guid?`. (No consumer reads it — `BroadcastClueReleasedNotificationHandler` re-projects the whole board — so this is inert.) |
| `Domain/Entities/LiveSession.cs` | The five edits below. |

`LiveSession` edits:

1. `ProjectReleasableTargets()` → `ProjectReleasableClues()`. Treasure-hunt branch: today's logic,
   emitting `ReleasableClue` with `TargetName` set. **New trivia branch**: `ClueSnapshots` for the
   active substage where `IsHiddenUntilOperatorRelease` and `Text` non-blank, ordered by
   `SequenceOrder`, emitting `ClueId` + `TargetName: null`.
2. `ResolveReleasableTarget(targetId)` → keep; add sibling `ResolveReleasableSubstageClue(clueId)`
   (asserts active substage is Trivia, clue belongs to it, policy is hidden, text non-blank).
   Dispatch on the subject.
3. `ReleaseClueToTeam` / `ReleaseClueToAllTeams` — take `ClueReleaseSubject`.
4. `EnsureClueNotAlreadyReleased(teamId, subject)` — match on whichever key the subject carries.
5. `CollectSubstageVisibleClues(substageSnapshotId, **teamId**)` — **the actual bug fix.** Project a
   clue when `IsVisibleWhenSubstageStarts` **or** a release record exists for
   `(teamId, clueSnapshotId)`. Mirror `CollectTargetVisibleClues`'s two-group order: always-visible
   pinned first by `SequenceOrder`, then released newest-first, `SequenceOrder` breaking ties.
   Update the caller at `LiveSession.cs:1053` to pass `teamId`.
6. `CountVisibleSubstageInitialClues()` — **leave the filter alone**; rewrite the comment per the
   trap above.

**Gate**: `make -C backend build SVC=session-operations-service`. Domain has zero external deps.

### Phase 2 — Application

| File | Change |
|---|---|
| `Application/Dtos/Sessions/ReleasableTargetsDto.cs` | → `ReleasableCluesDto`. `Targets` → `Clues`; `ReleasableClueDto(Guid? TargetId, Guid? ClueId, string? TargetName, int SequenceOrder, string ClueText)`. |
| `Application/Sessions/Common/ReleasableTargetsDtoFactory.cs` | → `ReleasableCluesDtoFactory`; project `ProjectReleasableClues()`. |
| `Application/Sessions/Queries/GetReleasableTargets/` | → `GetReleasableClues/` (query + handler). ADR-0011 vertical slice. |
| `Application/Sessions/Commands/ReleaseClue/ReleaseClueCommand.cs` | `(Guid LiveSessionId, Guid? TargetId, Guid? ClueId, Guid? TeamId)`. |
| `.../ReleaseClueCommandValidator.cs` | Replace `TargetId.NotEmpty()` with an exactly-one-of rule. |
| `Application/Sessions/Common/ClueReleaseFacade.cs` | Build the `ClueReleaseSubject`; pass through. `ReleaseClueResultDto` echoes the subject. |

Naming note: the folder rename is required by ADR-0011 (`structure-guard` runs inside `build`).

**Gate**: `make -C backend build`, then `make -C backend structure-guard SVC=session-operations-service`.

### Phase 3 — Infrastructure

`Persistence/Configurations/LiveSessionConfiguration.cs`:

1. `target_id` → nullable (drop `.IsRequired()`).
2. Replace the single unique index with **two filtered** ones — Postgres treats `NULL`s as
   distinct, so a plain composite index over a nullable column enforces nothing:
   - `(live_session_id, team_id, target_id)` unique `WHERE target_id IS NOT NULL`
   - `(live_session_id, team_id, clue_id)` unique `WHERE clue_id IS NOT NULL`
   Use `.HasFilter(...)`. Fix the comment to cite spec L696 accurately (it says *clue*).
3. Add a `CHECK` constraint mirroring the exactly-one-of invariant at the DB boundary.

Migration: `make -C backend ef SVC=session-operations-service ARGS="migrations add ReleaseClueByClueId"`.
Forward-safe on existing data — every current row has `target_id` set and `clue_id` null, which
satisfies both the new filtered index and the check.

**Gate**: `make -C backend build`; migration applies cleanly against a fresh DB.

### Phase 4 — Api

`Api/Controllers/SessionsController.cs`:

- `ReleaseClueRequest(Guid TargetId, Guid? TeamId)` → `(Guid? TargetId, Guid? ClueId, Guid? TeamId)`.
- Update the `clues/releasable` doc comment at `:326` — it says "treasure-hunt substage's
  still-releasable targets".
- Response type → `ReleasableCluesDto`.

Malformed subjects (both ids, neither id) surface as `400` via the validator and ADR-0018
ProblemDetails mapping — no controller try/catch.

**Gate**: `make -C backend build`.

### Phase 5 — Frontend (same PR — API contract change)

Root `AGENTS.md`: *"API contracts are the only shared surface. If a contract changes, flag it
explicitly — both sides must be updated together."* This one changes. Do not split the PR.

| File | Change |
|---|---|
| `frontend/app/lib/definitions.ts:487-511` | `ReleasableTargetDto` → `ReleasableClueDto` (nullable `targetId`, new `clueId`, nullable `targetName`); `targets` → `clues`. |
| `frontend/app/dashboard/OperatorClueReleasePanel.tsx` | Send whichever id the picked row carries; render `Pista {sequenceOrder}` when `targetName` is null (D-3). Keep the existing `409 clue-already-released-to-team` handling. |

Also in this phase, the two doc corrections carried over from the handoff's "Remaining work":

- `ProjectReleasableClues()` docstring — drop the "every listed target is one a subsequent
  release would accept" overclaim (disproved live; re-release returns 409).
- `frontend/docs/hu-28-manual-test.md:109` — "alongside the seeded target clues" (plural) is now
  wrong; only clue 1 shows until clue 2 is released.

---

## Test plan

Coverage gate is **at least 95% aggregate branch coverage** across all test projects
(`docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md`). `session-operations-service`
currently sits at 95.1% branch — the new branches must carry tests or the gate drops.

**Domain** (`tests/UnitTests/Domain/Entities/`)

- `LiveSessionReleasableTargetsTests.cs` → rename to `...ReleasableCluesTests.cs`. Add: trivia
  substage lists hidden clues by `SequenceOrder`; visible-when-starts clues are **not** listed;
  treasure-hunt cases unchanged.
- `LiveSessionClueReleaseTests.cs` — add: release a trivia clue to one team / all teams; the
  board shows it for the released team and **not** for others; second release → 409;
  `ProjectParticipantTeamBoard_PinsAlwaysVisibleCluesAboveReleasedClues` must still pass
  **unmodified** (the treasure-hunt regression guard).
- **New**: `ClueReleaseSubject` exactly-one-of; both-ids and neither-id throw.
- **New**: the double-count guard — release a trivia clue, assert
  `ProjectOperatorSessionPanel` counts it **once**. This is the trap above; it needs an explicit test.

**Application** (`tests/Application.UnitTests/Sessions/`)

- `Commands/ReleaseClue/ReleaseClueCommandValidatorTests.cs` — exactly-one-of.
- `Facades/ClueReleaseFacadeTests.cs` — subject construction for both shapes.

**Integration** (`tests/IntegrationTests/Api/`)

- `ReleasableTargetsEndpointTests.cs` → `ReleasableCluesEndpointTests.cs`; add a trivia case.
- `ReleaseClueEndpointTests.cs` — release by `clueId`; `400` on a malformed subject.
- `SignalRClueReleasedDeliveryTests.cs` — a trivia release reaches the board over SignalR.

**Gate**: `make -C backend test SVC=session-operations-service`, then
`make -C backend gate SVC=session-operations-service`.

## Manual verification

The existing fixtures cannot exercise this — neither seed authors a hidden clue on a trivia
substage (that is *why* the gap survived). Add one:
`frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts` authors the HU-171 trivia
substage; give it a second clue with `HiddenUntilOperatorRelease`, keeping clue 1 visible.

Per the handoff, **do not flip the existing clue to hidden** — `hu-171-manual-test.md` and
`hu-28-manual-test.md` both depend on a visible clue being on the board. Mirror the edit into
the byte-identical SQL in `frontend/docs/hu-171-manual-test.md`.

Then: seed → Start → picker is populated **immediately** (trivia is substage 1 — no 60s wait,
unlike the treasure-hunt path) → release → board shows it.

Stack gotchas carried from the handoff, still true: app services have no healthchecks, so `Up`
is not readiness (check `GET /api/sessions` returns 401, not 502); a live session snapshots its
mission immutably at creation, so **always re-seed** after a code change; sandboxed Bash can fail
with `apply-seccomp ... Permission denied` and still report **exit 0**.

## Risks

| Risk | Mitigation |
|---|---|
| Double-counting released trivia clues on the operator panel | Explicit domain test; do not touch `CountVisibleSubstageInitialClues`'s filter |
| Concurrent session's uncommitted work in `LiveSession.cs` + operator-panel files | Coordinate/rebase before Phase 1; commit by explicit path, never `git commit -a` |
| Nullable `target_id` silently voids the uniqueness guard | Filtered indexes + DB `CHECK`; integration test for the duplicate 409 |
| Frontend/backend contract skew | Single PR, Phases 2-5 together |
| Mobile board payload change (D-4) | Coordinate — `team-board-types.ts` has concurrent edits |
| Coverage gate regression from new branches | Test plan above; `make gate` before PR |

## Sequencing

Phases 1→4 are strictly ordered (each compiles against the previous). Phase 5 can be done in
parallel with 3-4 once the DTO shape in Phase 2 is settled. **Resolve D-2 and D-4 before starting.**

Estimated surface: ~9 backend source files (2 new VOs, 1 new exception, 1 deleted VO, 1
migration), 2 frontend files, ~8 test files.

## Suggested skills

- **`cqrs-mediatr-aspnetcore`** — Phase 2, the slice rename and validator.
- **`ef-core-postgresql`** — Phase 3, filtered unique indexes and the nullable-column migration.
- **`aspnet-backend-testing`** — the test plan, especially the double-count guard.
- **`/code-review`** — before the PR; scope it explicitly, the tree has other sessions' work.
