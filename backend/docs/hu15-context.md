# HU-15 Context - Creacion de sesion de mision desde mision activa (rebuild)

> Paste this section into any agent session that needs context for HU-15.
> Last updated: 2026-06-21 | Branch: `feature/hu-15-session-creation-from-mission`

## State

- DES-22 (HU-15): **Todo**, labels: `canon-realign`, `needs-rebuild`, `ready-for-agent`, `svc:session-operations-service`, `svc:mission-design-service`, `Feature`
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present). DES-22 is a foundation `needs-rebuild` row in the realignment map (`canon-realignment-after-mission-runtime-rewrite.md:52,86`), phase #2 "Session creation" (`:98`). It is **not** in the superseded column — it is the rebuild ticket. Body AC is stale; real scope is canon + the realignment map.
- **Owning service: `session-operations-service`.** The dual `svc:` label resolves from canon: session-operations-service owns HU-15 (it creates the `LiveSession` aggregate root + the immutable `MissionRuntimeSnapshot`); mission-design-service is the **read/validation seam** the snapshot copies from. Evidence: PRD DES-70 is "primera implementacion de session-operations-service (HU-15 a HU-36)" — HU-15 is its first HU (PRD lines 17, 62-63, 162-163, 185-187, 213-214); the realignment map files DES-22 under "Session creation".
- Predecessor DES ids: DES-14 (HU-09 rebuild, **Done** 2026-06-19), DES-15 (HU-10A, **Done** 2026-06-21) — cross-service source HUs HU-15 snapshots from; HU-07A/07B landed the `LiveSession` aggregate this HU's mission-based `Create` builds on.
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear).
- Branch: `feature/hu-15-session-creation-from-mission`, base `develop` (all dependencies Done/merged; no same-service predecessor In Progress).
- Superseded handling applied: HU-16 (DES-23→DES-75), HU-21A (DES-28→DES-76), HU-22 (DES-30→DES-77), HU-33A (DES-44→DES-78) dropped from the predecessor set. The two stale superseded HU-16 docs (`hu16-context.md`, `prompt_example_feature_hu16.md`) were deleted.

## Required design patterns

- `Facade`
  - Why: session creation is a multi-subsystem orchestration — readiness gate, mission-runtime fetch, snapshot build, aggregate construction, persistence — that must live behind a single Application-layer entry point, not an ad-hoc handler.
  - Phase owner: **X.2 Application**.
  - Concrete obligation: a single Application-layer `Facade` for mission-based session creation that orchestrates `readiness gate (IMissionReadinessSource) → fetch mission runtime content (IMissionRuntimeSource) → build MissionRuntimeSnapshot → LiveSession.Create → persist (ILiveSessionRepository)`. No scattered orchestration in the command handler; the handler stays a pass-through to the facade.

> Resolution note: HU-15 is **not** a row in `trivia_sprint_required_patterns_matrix.md`. The matrix maps **HU-16** ("Session creation from a quiz") to `Facade` (`:70`), and the realignment **supersedes HU-16** (DES-75 turns trivia selection into a `Substage`), moving session creation to HU-15/HU-17. The `Facade` mandate therefore transfers HU-16 → HU-15. This is canon-backed, not gap-filling: **ADR-0004 line 3** ("`Facade` for session orchestration and event publication in `SessionOperations`") and **PRD DES-70 lines 195-197** ("Complex orchestration belongs in an Application-layer `Facade`, especially for session creation"). The existing `CreateTriviaSessionFacade` is the direct predecessor this rebuild continues.

> Applies-where note (no new gate): the `POST /api/sessions` endpoint exposes a protected mutation, but HU-15 is **not** matrix-tagged for `Proxy` and is not in the applies-where set (HU-04/05/36B). The endpoint inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, **no** new `Proxy` gate. Likewise the `LiveSession` lifecycle `State` pattern is **HU-21A's** scope (DES-76); HU-15 only sets the initial state `Scheduled` at creation — **no** new `State` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-14 (HU-09) + DES-15 (HU-10A) — mission-design-service, both Done.** Rebuilt `Mission` as a source-content wrapper over a `MissionNode` Composite (`Stage` → `Substage` → optional `Clue`), `SubstagePlayMode` (`TreasureHunt`|`Trivia`) per substage, `Target`-based treasure hunt, optional `Clue` guidance, `TriviaQuizSelection` for trivia substages, and `MissionActivationPolicy` runtime-plan readiness. This is the **cross-service source** HU-15 freezes into its snapshot — see `hu09-context.md`, `hu10a-context.md`, `hu10a-brief.md`. The full runtime plan (including resolved trivia question content) is the thing HU-15 must read from mission-design and copy.
- **HU-07A/07B — session-operations-service.** Landed the `LiveSession` aggregate root + EF persistence + the session State machine (`Domain/Services/SessionStates/*`, `SessionStateTransitionPolicy`) + participant/team runtime. HU-15's mission-based `Create` builds on this same aggregate — but the existing **source/snapshot** parts (`SessionSource` with a `TriviaQuiz` member, `TriviaSessionSnapshot`, `CreateTrivia` factory, quiz-fetch integration) were built under the **old quiz-as-source model** and are exactly what HU-15 tears out.

**Landed, untouched by this HU:** HU-18/19/20 (team/operator association + reads) build on top of session creation, not the reverse — do not anchor creation on them.

**Superseded / not predecessors (do NOT anchor on):** HU-16 (DES-23→DES-75), HU-21A (DES-28→DES-76), HU-22 (DES-30→DES-77), HU-33A (DES-44→DES-78).

**Coverage:** session-operations-service carries the HU-07A/07B baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Rebuild posture | Rebuild session creation around `Mission` as the **only** `SessionSource`; tear out the quiz-as-source model, not patch beside it. |
| Mission-only source | Reshape `SessionSource` VO to reference exactly one `Mission`; drop the `TriviaQuiz` source member and `SessionSourceType.TriviaQuiz`. |
| Immutable runtime snapshot | Add `MissionRuntimeSnapshot` — an immutable owned model of `LiveSession` that freezes the full mission runtime plan (stages/substages, treasure targets + clues, resolved trivia questions) at creation. |
| Mission-based factory | Add `LiveSession.Create(...)` mission-based factory that owns the snapshot and sets initial state `Scheduled`; keep `LiveSessionCreatedEvent`. |
| No session-level mode | Remove session-level `SessionMode` reliance from creation; play mode is per-`Substage` inside the snapshot, never a session attribute. |
| Creation `Facade` | Rebuild orchestration as a mission-based Application `Facade`: readiness gate → mission-runtime fetch → snapshot build → `LiveSession.Create` → persist. |
| Mission runtime-plan endpoint (Phase 0, mission-design) | Add `GET /api/missions/{id}/runtime-plan` resolving the full plan with inlined trivia questions — the cross-service read seam, covered by the `svc:mission-design-service` label; lands before X.3. |
| Mission runtime read port | Add `IMissionRuntimeSource` read port + HTTP adapter that consumes the Phase 0 endpoint in a single read (distinct from `/readiness` and the old quiz-fetch). |
| Persistence rebuild | EF mapping + migration for the `MissionRuntimeSnapshot` owned graph; drop `source_trivia_quiz_id` and the trivia-snapshot tables. |
| API contract | Keep `POST /api/sessions` (`Administrator`, 201); reshape request to mission-only `CreateSessionRequest`; map `MissionNotEligibleForSessionCreationException` to 409/422. |
| Frontend flow | Session-creation form selects an active, runtime-ready `Mission` (not a quiz); shows readiness/eligibility rejection from backend. |

## Touched surfaces

- `backend/services/session-operations-service` (owner — `LiveSession`, `MissionRuntimeSnapshot`, creation `Facade`, persistence, `/api/sessions`)
- `backend/services/mission-design-service` (read seam only — must expose a mission runtime-plan/detail endpoint that resolves full trivia content; HU-15 consumes, never mutates, mission authoring content)
- `frontend/` session-creation UI (mission selection + eligibility feedback)
- API contract boundary: `POST /api/sessions` mission-only request/response; the cross-service `session-operations → mission-design` runtime-plan read contract

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **DES-22 is `needs-rebuild`** — drive it as a real rebuild. Reject any plan that keeps `TriviaQuiz` as a `SessionSource`, reintroduces session-level `SessionMode`, or creates a session directly from a quiz.
- **Cross-service read seam (resolved 2026-06-21).** HU-15's snapshot must freeze resolved trivia content — play-time scoring/timing read the **snapshot**, not the live quiz (`bd_umbral_entity_spec.md:343,450,617-638`) — and no single mission-design endpoint returns it: `GET /api/missions/{id}` carries only `TriviaQuizSelection { TriviaQuizId }` (a bare reference), and `GET /api/trivias/{id}` is the old per-quiz fetch the existing `PublishedTriviaQuizSource` already calls. **Resolution:** HU-15 adds a **new `GET /api/missions/{id}/runtime-plan` endpoint in mission-design** (Phase 0 prerequisite, covered by the `svc:mission-design-service` label on DES-22) that resolves the full plan with inlined trivia questions; session-operations X.3 consumes it in a **single read**. Phase 0 must land **before** X.3.
- **`Facade` transfer is the #1 review item.** HU-15 is not literally in the patterns matrix — the `Facade` mandate is transferred from superseded HU-16, backed by ADR-0004:3 + PRD:195-197. Confirm at Stop 1.
- **Do not rebuild the `LiveSession` aggregate or its State machine.** Those are HU-07A/07B (state machine) and HU-21A (lifecycle `State` pattern) scope. HU-15 adds the mission-based `Create` path + snapshot only, and sets initial state `Scheduled`.
- The service already uses `Proxy` (`SessionAdministrationAuthorizationProxy`, `DisconnectParticipantAuthorizationProxy`) for some commands — this does **not** make `Proxy` a HU-15 gate. Creation inherits the standard `Administrator` `AuthorizationBehaviour`.
- Confirmed from code: `LiveSession.Create` today takes `SessionMode`+`SessionSource` (and `CreateTrivia` takes a `TriviaSessionSnapshot`) — **no Mission/MissionRuntimeSnapshot param**. `MissionRuntimeSnapshot` does not exist anywhere (grep = 0) — net-new. `IMissionReadinessSource.GetByIdAsync(int, CT) → MissionReadinessDto?` (`IsActive`/`IsReady`/`ActivationState`/`Failures`) is reusable for the active+ready gate; what's missing is the mission **content** to snapshot.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **realignment-rebuild**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors
> point **only** at code classified `keep`. Canon-delta (old → new),
> `canon-realignment-after-mission-runtime-rewrite.md:22-30`: `Mission` is the **only**
> `SessionSource` (`:22`); no session-level `SessionMode` — play mode is per-`Substage`
> (`:24`); `LiveSession` snapshots the full mission runtime plan at creation, immutable (`:25`).
> Old model to tear out (`grilling-session-mission-restructure.md:138-160`): quiz-or-mission
> source, session-level `SessionMode`, direct trivia sessions, clue-based progression.

### Phase 0 — mission-design: runtime-plan read seam *(prerequisite — lands before X.3)*
**Derive** (`bd_umbral_entity_spec.md` §MissionRuntimeSnapshot :326-356 = the shape the snapshot must receive; existing `MissionsEndpoints.cs` detail + `TriviasEndpoints.cs` detail = the data already authored, just not joined):
- New read query + endpoint **`GET /api/missions/{id}/runtime-plan`** returning the **resolved** runtime plan: mission `title` + `maximumTime`; ordered stages → substages (`PlayMode`, `WinnerScore`); per TreasureHunt substage — targets (`name`, `qrCode`, `sequenceOrder`, `isActive`) + optional clue (`text`, `visibilityPolicy`); per Trivia substage — the selected published quiz's **resolved questions** (`prompt`, options + `isCorrect`, `scoreValue`, `timeLimitSeconds`), in strict mission order. Resolves `TriviaQuizSelection.TriviaQuizId` → `TriviaQuiz` questions — the join `GET /api/missions/{id}` omits and that session-operations must **not** assemble via N+1.
- This is the single read `IMissionRuntimeSource` (X.3) consumes; it does not replace `/readiness` (eligibility stays a separate gate).

**Target files** (create | edit — file to mirror):
- create `Application/Missions/Queries/GetMissionRuntimePlan/*` — mirror `Application/Missions/Queries/GetMissionDetail/*`
- create the runtime-plan response DTO — mirror `MissionResponse` (structure) + `TriviasEndpoints.cs` `TriviaQuestionResponse`/`TriviaOptionResponse` (resolved questions)
- edit `Api/Endpoints/MissionsEndpoints.cs` — add `missions.MapGet("/{id:int}/runtime-plan", GetMissionRuntimePlan)`

**Pattern this phase owns:** none.
**Gate:** query/endpoint integration test — a ready mission with both TreasureHunt and Trivia substages returns the full resolved plan incl. trivia questions/options/correct answers/timers/scores in strict mission order; mission-design coverage ≥ repo gate (ADR-0005). No mutation of mission authoring content.

### Phase X.1 — Domain
**Derive** (`bd_umbral_entity_spec.md` §LiveSession :283-323, §MissionRuntimeSnapshot :328-359; `ddd_solution_model.md` §SessionCreationPolicy :435):
- **`MissionRuntimeSnapshot`** — immutable child/owned model of `LiveSession` (`:328`, immutable `:355`). Freezes: `missionRuntimeSnapshotId` (`:336`), `sourceMissionId` (`:338`), `missionTitle` (`:339`), `maximumTime` (`:340`), `stageSnapshots` (ordered stages + substages, `:341`), `targetSnapshots` (TH target content + QR id + clues, `:342`), `triviaQuestionSnapshots` (questions, options, correct answers, timers, scores, `:343`). Invariants: order = strict mission order (`:357`); TH substage ≥1 target + winner score (`:358`); trivia substage ≥1 question with timer + score + options + correct answer (`:359`).
- **`LiveSession.Create(...)`** mission-based factory — `sourceMissionId` (`SessionSource` VO → one Mission, `:284,302`), `titleSnapshot` (`:287`), `maximumTime` (`:295`), `assignedOperatorUserId` optional (`:296`), owns the immutable `MissionRuntimeSnapshot` (`:285,303`); initial **state = `Scheduled`** (`:317`); raises `LiveSessionCreatedEvent` (keep).
- **`SessionSource`** VO reshaped to Mission-only (drop `TriviaQuiz` / `SessionSourceType.TriviaQuiz`).
- **`SessionCreationPolicy`** (keep) — `EnsureMissionEligible(missionId, isActive, isReady)` (`SessionCreationPolicy.cs:16-27`; `ddd_solution_model.md:435`).

**Target files** (create | edit — file to mirror):
- create `Domain/Entities/MissionRuntimeSnapshot.cs` — mirror `Domain/Entities/TriviaSessionSnapshot.cs` (immutable owned snapshot shape) — *but model the mission runtime plan, not quiz questions*
- create `Domain/ValueObjects/{StageSnapshot,SubstageSnapshot,TargetSnapshot}.cs` — mirror `Domain/ValueObjects/TriviaQuestionSnapshot.cs` (immutable VO with guard-clause invariants)
- edit `Domain/ValueObjects/SessionSource.cs` — Mission-only reshape
- edit `Domain/Entities/LiveSession.cs` — add mission-based `Create`, own the snapshot, set `Scheduled`
- keep/mirror `Domain/Services/SessionCreationPolicy.cs`, `Domain/Events/LiveSessionCreatedEvent.cs`, `Domain/Enums/SessionState.cs`

**Pattern this phase owns:** none (`Facade` is X.2).
**Gate:** unit test per new type; snapshot immutable; `Create` sets `Scheduled`; reject non-Mission source; no session-level `SessionMode`; `TriviaQuiz` not a `SessionSource`; snapshot ordering + TH/trivia substage invariants enforced.

**Existing code (keep / delete / decide):**
- delete `Domain/Entities/TriviaSessionSnapshot.cs` — quiz-as-source snapshot
- delete `Domain/Exceptions/SessionSourceTriviaQuizIdRequiredException.cs` — quiz-source requirement
- keep `Domain/Services/SessionCreationPolicy.cs` — canon-aligned eligibility policy (safe mirror)
- keep `Domain/Exceptions/{MissionNotEligibleForSessionCreationException,SessionSourceEntityRequiredException}.cs`, `Domain/Events/LiveSessionCreatedEvent.cs`, `Domain/Enums/SessionState.cs`
- decide `Domain/Entities/LiveSession.cs` (`Create`/`CreateTrivia` factories, `triviaSnapshot` ctor, `ValidateSourceForMode` quiz branch — reuse the aggregate, rewrite the creation path), `Domain/ValueObjects/SessionSource.cs`, `Domain/Enums/SessionSourceType.cs` (`TriviaQuiz` member), `Domain/Enums/SessionMode.cs`, `Domain/ValueObjects/{TriviaQuestionSnapshot,TriviaOptionSnapshot}.cs` (likely **reuse** inside `MissionRuntimeSnapshot` — do not delete blind), `Domain/Exceptions/{SessionSourceDoesNotMatchModeException,TriviaSessionSnapshotMustContainQuestionsException,TriviaSessionSnapshotRequiredException,TriviaQuestionSnapshotRequires*Exception}.cs`

### Phase X.2 — Application — **owns `Facade`**
**Derive** (`ddd_solution_model.md` §SessionOperations application services; PRD DES-70 :195-197):
- Rebuild creation orchestration as a mission-based **`Facade`**: readiness gate via `IMissionReadinessSource` (keep) → fetch mission runtime content via **new read port `IMissionRuntimeSource`** → build `MissionRuntimeSnapshot` → `LiveSession.Create` → persist via `ILiveSessionRepository` (keep).
- New `CreateSessionCommand(MissionId, Title, MaximumTimeMinutes, ScheduledAt)` (drop `SourceTriviaQuizId`), validator, handler (keep pass-through to facade), result DTO (drop quiz fields).

**Target files** (create | edit — file to mirror):
- create `Application/Sessions/Commands/CreateSession/{CreateSessionCommand,CreateSessionCommandValidator,CreateSessionFacade,ICreateSessionFacade}.cs` — mirror `Application/Sessions/Commands/CreateTriviaSession/*` (command/validator/facade/iface shape)
- create `Application/Common/Interfaces/IMissionRuntimeSource.cs` — mirror `Application/Common/Interfaces/IMissionReadinessSource.cs`
- create `Application/Sessions/DTOs/{MissionRuntimeDto,CreateSessionResultDto}.cs` — mirror `Application/Sessions/DTOs/{MissionReadinessDto,CreateTriviaSessionResultDto}.cs`
- edit/replace `Application/Sessions/Handlers/CreateTriviaSessionCommandHandler.cs` → `CreateSessionCommandHandler.cs` (keep pass-through)

**Pattern this phase owns:** `Facade` — single Application-layer orchestration entry point for session creation.
**Gate:** facade unit tests (happy path + reject inactive / not-ready mission) + validator tests; **`Facade` is the single orchestration entry point** (no ad-hoc handler logic); no `SourceTriviaQuizId` / quiz fields in command/DTO; no session-level `SessionMode`.

**Existing code (keep / delete / decide):**
- delete `Application/Common/Interfaces/IPublishedTriviaQuizSource.cs`, `Application/Sessions/DTOs/PublishedTriviaQuizDto.cs` — quiz-fetch contract
- keep `Application/Common/Interfaces/{IMissionReadinessSource,ILiveSessionRepository}.cs`, `Application/Sessions/DTOs/MissionReadinessDto.cs`
- decide whole `Application/Sessions/Commands/CreateTriviaSession/*` + `CreateTriviaSessionResultDto.cs` + `CreateTriviaSessionCommandHandler.cs` (rename/rebuild to mission-based `CreateSession`; `CreateTriviaSessionFacade` is the `Facade` precedent to mirror, then retire)

### Phase X.3 — Infrastructure
**Derive** (`structure.md` §session-operations persistence; canon-delta `:25`):
- New `IMissionRuntimeSource` HTTP adapter calling **`GET /api/missions/{id}/runtime-plan`** (the Phase 0 endpoint) — a single read returning the full runtime plan incl. resolved trivia content.
- EF mapping for the `MissionRuntimeSnapshot` owned graph (replace the trivia-snapshot tables).
- Migration: drop `source_trivia_quiz_id` + `live_session_trivia_snapshots/_questions/_options`, add the mission-runtime-snapshot tables. `LiveSessionRepository` (keep) round-trips the snapshot.

**Target files** (create | edit — file to mirror):
- create `Infrastructure/Integrations/MissionDesign/{MissionRuntimeSource,MissionRuntimeSourceOptions}.cs` — mirror `Infrastructure/Integrations/MissionDesign/{MissionReadinessSource,MissionReadinessSourceOptions}.cs`
- edit `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — owned-graph mapping for `MissionRuntimeSnapshot` (replace trivia-snapshot config)
- create new migration under `Infrastructure/Migrations/` — grep `MissionRuntimeSnapshot`/`source_trivia_quiz_id` in `ApplicationDbContextModelSnapshot.cs` first; do **not** full-read it
- keep `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`

**Pattern this phase owns:** none.
**Gate:** migration applies cleanly and represents the snapshot owned graph; repository integration test round-trips the full `MissionRuntimeSnapshot`; mission-runtime source integration test; no `source_trivia_quiz_id` / trivia-snapshot tables remain.

**Existing code (keep / delete / decide):**
- delete `Infrastructure/Integrations/MissionDesign/{PublishedTriviaQuizSource,PublishedTriviaQuizSourceOptions}.cs`
- keep `Infrastructure/Integrations/MissionDesign/{MissionReadinessSource,MissionReadinessSourceOptions}.cs`, `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`
- decide `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` (reuse the owned-type config skeleton; replace the trivia-snapshot graph)

### Phase X.4 — Api
**Derive** (`SessionsEndpoints.cs:23`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- Keep `POST /api/sessions` route + `Administrator` auth + `201 Created`.
- Reshape request → mission-only `CreateSessionRequest(MissionId, Title, MaximumTimeMinutes, ScheduledAt)` (drop `SourceTriviaQuizId`).
- Map `MissionNotEligibleForSessionCreationException` → 409/422.

**Target files** (create | edit — file to mirror):
- edit `Api/Endpoints/SessionsEndpoints.cs` — request DTO reshape, keep route/auth/status
- edit the service's problem-details/exception handler — map `MissionNotEligibleForSessionCreationException`

**Pattern this phase owns:** none — endpoint inherits the standard `AuthorizationBehaviour` (no new `Proxy` gate).
**Gate:** endpoint integration tests (ready mission → 201; inactive / not-ready mission → rejected 409/422); request contains no `SourceTriviaQuizId`; **ADR-0005 coverage** (service ≥ repo gate).

**Existing code (keep / delete / decide):**
- decide `Api/Endpoints/SessionsEndpoints.cs` (keep route/auth/201; rewrite the request DTO from `CreateTriviaSessionRequest(MissionId, SourceTriviaQuizId, Title, MaximumTimeMinutes, ScheduledAt)` to mission-only)
