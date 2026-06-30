# HU-17 Context - Creacion de sesion desde una unica fuente (single-source invariant lock)

> Paste this section into any agent session that needs context for HU-17.
> Last updated: 2026-06-30 | Branch: `feature/hu-17-single-source-session-creation`

## State

- DES-24 (HU-17): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present.
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present) — but **verification-dominant**. DES-24 is the realignment map's phase #3 "Session creation" row ("Single-source rule = mission-wrapper (drop 'trivia session from quiz')", `canon-realignment-after-mission-runtime-rewrite.md:99`). It is **not** in the superseded column. The single mission-only source path, the immutable `MissionRuntimeSnapshot`, the `CreateSessionFacade`, and `SessionCreationPolicy` were **already shipped by HU-15 (DES-22, Done 2026-06-21)**. HU-17 does **not** rebuild them — it **locks the single-source invariant with explicit tests across all four layers and finishes the residual two-source teardown HU-15 left behind**.
- **Owning service: `session-operations-service`** (single `svc:` label — unlike HU-15's dual label, there is **no** cross-service mission-design prerequisite; HU-15 already landed `GET /api/missions/{id}/runtime-plan` + `IMissionRuntimeSource`).
- Predecessor DES ids: **DES-22 (HU-15, Done 2026-06-21)** — the build-on foundation. Landed-untouched same-service: DES-25 (HU-18), DES-26 (HU-19), DES-27 (HU-20), DES-11 (HU-07A), DES-12 (HU-07B).
- PRD DES id: **DES-70** -> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear).
- Branch: `feature/hu-17-single-source-session-creation`, base `develop` (all dependencies Done/merged; no same-service predecessor In Progress).
- Superseded handling applied: HU-16 (DES-23->DES-75), HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78) are **Canceled** and dropped from the predecessor set — never anchor on them.

## Required design patterns

- **None mandated.** `required_patterns_matrix.md:45,100` lists HU-17 as `—`: "Single-source invariant; enforced inside the `HU-15`/`HU-16` Facade + `SessionCreationPolicy`, not its own pattern." Do **not** invent a pattern to fill the gap. The invariant rides on HU-15's already-built `CreateSessionFacade` (`Facade`) + `SessionCreationPolicy`; HU-17 adds no new pattern gate.
- **Applies-where note (no new gate):** `POST /api/sessions` is a protected mutation, but HU-17 is **not** matrix-tagged for `Proxy` and is not in the applies-where set (HU-04/05/36B). The endpoint inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 (HU-15) — session-operations-service, Done 2026-06-21.** Rebuilt session creation around `Mission` as the **only** `SessionSource`: reshaped `SessionSource` VO to Mission-only, dropped `SessionSourceType.TriviaQuiz` (the enum now holds only `Mission = 1`), added the immutable `MissionRuntimeSnapshot` + snapshot VOs, the mission-based `LiveSession.Create(...)` (sets `Scheduled`, validates source via `ValidateSource`), the single `CreateSessionFacade`, `SessionCreationPolicy.EnsureMissionEligible`, the `IMissionRuntimeSource` read port + HTTP adapter, the `AddMissionRuntimeSnapshot` migration (dropped `source_trivia_quiz_id` + the trivia-snapshot tables), and the mission-only `POST /api/sessions` contract. **This is the foundation HU-17 verifies and hardens** — see `hu15-context.md`, `hu15-brief.md`. The mission-only happy path already exists; HU-17 does not rebuild it.

**Landed, untouched by this HU:** HU-18 (DES-25, team association), HU-19 (DES-26, operator assignment), HU-20 (DES-27, assigned-session reads), HU-07A/07B (DES-11/12, membership + reconnect) build on/around the session aggregate but do not inform the single-source creation invariant — do not anchor on them.

**Superseded / not predecessors (do NOT anchor on):** HU-16 (DES-23->DES-75), HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78) — all Canceled.

**Coverage:** session-operations-service carries the HU-07A/07B/HU-15 baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Verification posture | HU-15 already made `Mission` the only `SessionSource` and tore out the snapshot/column. HU-17 **locks** that as an explicitly tested invariant and **finishes the residual teardown**; it does not rebuild creation. |
| Single-source invariant (domain) | Add explicit unit tests that `SessionSource.Create` always yields `SourceType == Mission`, `SessionSourceType` exposes only `Mission`, and `LiveSession.Create`/`ValidateSource` reject a non-mission source. |
| Residual two-source teardown | Delete the dead two-source debris HU-15 left orphaned (zero non-migration references): `SessionMode` enum, `SessionSourceDoesNotMatchModeException`, and the `TriviaSessionSnapshot*` exception pair whose entity HU-15 already removed. |
| No source mixing (application) | Verify + test that the `CreateSessionFacade` is the **single** creation entry point and builds the `MissionRuntimeSnapshot` **solely** from the one requested mission's runtime plan — no external source merged; the command carries no non-mission source field. |
| Persistence cleanliness | Verify (assert via test) the persisted `LiveSession`/`MissionRuntimeSnapshot` graph carries no `source_trivia_quiz_id`, no quiz-snapshot tables, no `SessionMode` column. **No new migration** — HU-15's `AddMissionRuntimeSnapshot` already dropped them and the current model snapshot is clean. |
| API contract (single source) | Verify + test that `POST /api/sessions` request is mission-only (no quiz/second-source field) and there is **no alternate session-creation route** (no quiz-creation endpoint). Reshape nothing — HU-15's contract is correct. |
| Frontend flow | Verification/cleanup only: confirm the session-creation UI offers a `Mission` as the only source and remove any residual quiz-as-source / `SessionMode` affordance or type. No API contract change. |

## Touched surfaces

- `backend/services/session-operations-service` (owner — domain invariant tests + dead-debris deletion; application/infra/api verification tests)
- `frontend/` session-creation UI (verification + stale quiz-as-source cleanup; no contract change)
- API contract boundary: **unchanged** — `POST /api/sessions` mission-only `CreateSessionRequest` (already landed by HU-15); HU-17 only proves and locks it

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **Do NOT re-implement HU-15.** The mission-only `SessionSource`, `MissionRuntimeSnapshot`, `CreateSessionFacade`, `SessionCreationPolicy`, runtime-plan endpoint, and mission-only `POST /api/sessions` are already shipped and Done. Reject any plan that re-adds a `MissionRuntimeSnapshot`, a creation facade, or a runtime-plan endpoint — HU-17 verifies and hardens, it does not duplicate.
- **The dead debris is genuinely dead (confirmed by grep, 2026-06-30):** `SessionMode` (only self-referenced by `SessionSourceDoesNotMatchModeException`), `SessionSourceDoesNotMatchModeException` (only self-reference), `TriviaSessionSnapshotMustContainQuestionsException` + `TriviaSessionSnapshotRequiredException` (their `TriviaSessionSnapshot` entity was deleted by HU-15) — all have **zero non-migration references**. Safe to delete.
- **Do NOT delete the `TriviaQuestionSnapshot*` exceptions.** `TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException` / `TriviaQuestionSnapshotRequiresCorrectOptionException` belong to the **live** `TriviaQuestionSnapshot`/`TriviaOptionSnapshot` VOs that HU-15 **reuses inside** `MissionRuntimeSnapshot` (trivia substage content). Only the `TriviaSessionSnapshot*` (old quiz-as-session snapshot) pair is dead.
- **No new migration.** Removing the dead `SessionMode` enum is a pure code deletion — `SessionMode` is not persisted on `LiveSession` (no entity property; the current `ApplicationDbContextModelSnapshot.cs` has no `SessionMode`/`source_trivia_quiz_id`). Do not add an empty migration; if `ef migrations add` produces a non-empty diff, the model was not actually clean — investigate before committing.
- **Namespace is `umbral_backend.*`** across all session-ops layers (not a per-service root namespace) — match existing files.
- **The single-source guard already exists** at `Domain/Entities/LiveSession.cs:620-626` (`ValidateSource` throws `SessionSourceEntityRequiredException` when `source.SourceType != SessionSourceType.Mission`). HU-17 adds the **test** that locks it; do not duplicate the guard.
- **DES-75/76/77/78 are blocked on DES-24.** This invariant must be explicitly locked before the trivia-substage realign / state machine / timer / trivia orchestration rebuilds, so a session-level mode concept cannot be resurrected. That is why a verification-dominant slice is still a gated deliverable.

## Per-phase derivation - authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **realignment-rebuild (verification-dominant)**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors point **only** at code classified `keep`.
> Canon-delta (`canon-realignment-after-mission-runtime-rewrite.md:22-25`): `Mission` is the **only** `SessionSource`;
> `TriviaQuiz` is **not** a `SessionSource`; there is **no** session-level `SessionMode`; the `MissionRuntimeSnapshot` is immutable.
> Invariant canon: `bd_umbral_entity_spec.md:189` ("a `TriviaQuiz` is not a `SessionSource`"), `:284` (`sourceMissionId`/`SessionSource`), `:302` ("one `LiveSession` originates from exactly one active `Mission`"), `:303` (one immutable snapshot); PRD DES-70:176,180-184 ("`SessionSource` pointing to one mission"; "HU-15/16/17 form one family: mission-only creation, immutable snapshot, rejection of direct `TriviaQuiz` sources"); `required_patterns_matrix.md:100`.

### Phase X.1 - Domain
**Derive** (`bd_umbral_entity_spec.md:189,284,302-303`; `canon-realignment-after-mission-runtime-rewrite.md:22-25`):
- Lock the single-source invariant as a test over the as-built domain: `SessionSourceType` exposes only `Mission`; `SessionSource.Create(Guid)` always produces `SourceType == Mission` and rejects `Guid.Empty` (`SessionSourceEntityRequiredException`); `LiveSession.Create(...)` -> `ValidateSource` rejects any non-mission source. (Guard already at `LiveSession.cs:620-626`; HU-17 adds the test, not the guard.)
- Finish the residual two-source teardown: a session-level `SessionMode` and a `TriviaQuiz` source are not representable in the type system after this phase.

**Target files** (create | edit | delete - file to mirror):
- delete `Domain/Enums/SessionMode.cs` - session-level mode retired (`canon-realignment-after-mission-runtime-rewrite.md:24`)
- delete `Domain/Exceptions/SessionSourceDoesNotMatchModeException.cs` - mode<->source matching is a two-source concept; dead
- delete `Domain/Exceptions/TriviaSessionSnapshotMustContainQuestionsException.cs`, `Domain/Exceptions/TriviaSessionSnapshotRequiredException.cs` - orphaned (entity removed by HU-15)
- create `tests/.../Domain/.../SessionSourceSingleSourceInvariantTests.cs` (or extend the existing `SessionSource`/`LiveSession` domain tests) - mirror an existing domain test in the session-ops test project

**Pattern this phase owns:** none.
**Gate:** Domain build passes; unit test locks the single-source invariant (`SessionSource.Create` -> `Mission` only; `LiveSession.Create` rejects a non-mission source; `SessionSourceType` exposes only `Mission`); dead two-source debris (`SessionMode`, `SessionSourceDoesNotMatchModeException`, `TriviaSessionSnapshot*` pair) deleted; no `SessionMode` type remains in the service; the live `TriviaQuestionSnapshot*` exceptions are untouched.

**Existing code (keep / delete / decide):**
- delete `Domain/Enums/SessionMode.cs`, `Domain/Exceptions/SessionSourceDoesNotMatchModeException.cs`, `Domain/Exceptions/TriviaSessionSnapshotMustContainQuestionsException.cs`, `Domain/Exceptions/TriviaSessionSnapshotRequiredException.cs` - dead two-source debris (zero non-migration refs)
- keep `Domain/ValueObjects/SessionSource.cs`, `Domain/Enums/SessionSourceType.cs`, `Domain/Entities/LiveSession.cs` (`ValidateSource` :620-626), `Domain/Services/SessionCreationPolicy.cs`, `Domain/Exceptions/SessionSourceEntityRequiredException.cs` - canon-aligned; verify + mirror, do not rewrite
- decide none - the `TriviaQuestionSnapshot*` exceptions are **live** (reused VOs); do not touch

### Phase X.2 - Application
**Derive** (`required_patterns_matrix.md:100` - invariant enforced inside the Facade + policy; PRD DES-70:180-184; `ddd_solution_model.md` §SessionCreationPolicy):
- The mission `CreateSessionFacade` is the **single** creation orchestration; it builds the `MissionRuntimeSnapshot` **solely** from `IMissionRuntimeSource.GetByIdAsync(command.MissionId)`, so no external source can be merged (AC #4). The `CreateSessionCommand` carries no non-mission source field (AC #1/#2). The retired `CreateTriviaSession*` / `IPublishedTriviaQuizSource` / `PublishedTriviaQuizDto` are already gone (verify absent).
- HU-17 adds the **test** that locks this; it does not change the facade.

**Target files** (create | edit - file to mirror):
- create/extend a facade/handler test under `tests/.../Application/.../CreateSession*` - mirror the existing `CreateSessionFacade` tests (single-source + no-mixing assertions)
- keep (verify, do not rewrite) `Application/Sessions/Commands/CreateSession/{CreateSessionCommand,CreateSessionCommandValidator,CreateSessionFacade,ICreateSessionFacade,CreateSessionResultDto,CreateSessionCommandHandler}.cs`

**Pattern this phase owns:** none (the `Facade` carrying the invariant is HU-15's, already built; `required_patterns_matrix.md:100`).
**Gate:** Application build passes; facade/handler test proves the snapshot is built **only** from the requested mission (its `sourceMissionId` derives from `command.MissionId`, no external source mixed) and the command carries no non-mission source field; creation has a **single** entry point (the mission `CreateSessionFacade`); no `CreateTriviaSession*` / `IPublishedTriviaQuizSource` path remains.

**Existing code (keep / delete / decide):**
- keep `Application/Sessions/Commands/CreateSession/*` - canon-aligned single-source creation
- delete none (HU-15 already removed `CreateTriviaSession*`, `IPublishedTriviaQuizSource`, `PublishedTriviaQuizDto`) - **verify absent**, do not re-create
- decide none

### Phase X.3 - Infrastructure
**Derive** (`canon-realignment-after-mission-runtime-rewrite.md:25` immutable snapshot; `structure.md` §session-ops persistence):
- The persisted `LiveSession` + owned `MissionRuntimeSnapshot` graph carries no `source_trivia_quiz_id` column, no quiz-snapshot tables, no `SessionMode` column. HU-15's `20260621174437_AddMissionRuntimeSnapshot` migration already dropped them and the current `ApplicationDbContextModelSnapshot.cs` is clean (grep-verified 2026-06-30). **No new migration.**
- HU-17 adds the **test** that locks persistence cleanliness + the mission-only source round-trip.

**Target files** (create | edit - file to mirror):
- create/extend a repository integration test - mirror the existing `LiveSessionRepository` round-trip test: a created session round-trips with a mission-only `SessionSource` and no foreign-source column/table
- keep `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`, `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`, `Infrastructure/Integrations/MissionDesign/{MissionReadinessSource,MissionRuntimeSource}.cs`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; **no new migration** - assert (`ef migrations add` dry-check or model-snapshot grep) the model already excludes `source_trivia_quiz_id`/`SessionMode`/quiz-snapshot tables; repository integration test round-trips a session with a mission-only `SessionSource`; no foreign-source column or table persists.

**Existing code (keep / delete / decide):**
- keep `LiveSessionConfiguration.cs`, `LiveSessionRepository.cs`, `MissionReadinessSource.cs`, `MissionRuntimeSource.cs`
- delete none (HU-15 dropped `PublishedTriviaQuizSource*`) - **verify absent**
- decide none

### Phase X.4 - Api
**Derive** (`Api/Controllers/SessionsController.cs:22-37,182-186`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- `POST /api/sessions` request is the mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)` (no quiz/second-source field), `Administrator`-guarded, `201 Created`. There is **no** alternate session-creation route. HU-17 verifies + covers; it reshapes nothing.

**Target files** (create | edit - file to mirror):
- create/extend an endpoint integration test - mirror the existing `SessionsController` create test: mission -> 201 with a single mission source in the contract; no quiz/second-source field; no alternate creation route
- keep (verify, do not reshape) `Api/Controllers/SessionsController.cs`

**Pattern this phase owns:** none - the endpoint inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); HU-17 is not in the applies-where `Proxy` set -> no new gate.
**Gate:** endpoint integration test (active+ready mission -> 201; the contract carries a single mission source, no quiz/second-source field; no alternate creation route) + the ineligible-mission rejection (reuse HU-15's 409/422 path); **ADR-0005 coverage** (service >= repo gate).

**Existing code (keep / decide):**
- keep `Api/Controllers/SessionsController.cs` (route/auth/201, mission-only `CreateSessionRequest`) - verify, do not reshape
- decide none
