# HU-10A Context - Estructura jerarquica de misiones

> Paste this section into any agent session that needs context for HU-10A.
> Last updated: 2026-06-19 | Branch: `feature/hu-10a-mission-hierarchy-structure`

## State

- DES-15 (HU-10A): **In Progress**, labels: `canon-realign`, `ready-for-agent`, `svc:mission-design-service`, `Feature`
- **Resolved mode:** canon-realign (comment-only reword) — `canon-realign` without `needs-rebuild`. The canon comment (`⚠️ Deuda de canon`) identified that the hierarchy needed `SubstagePlayMode` + `Target` and `Clue` as optional. HU-09's rebuild (DES-14, Done 2026-06-19) already implemented all of these; HU-10A's AC has been tightened against canon and the backend is complete. The remaining work is the **frontend hierarchy authoring UI**.
- Supersession filtering: DES-23 (HU-16, Done) is in the realignment map's superseded column (superseded by DES-75) — dropped from the predecessor set. DES-75 is Todo, not a predecessor.
- Predecessor DES ids: DES-14 (HU-09, Done — build-on), DES-17 (HU-11, Done — unrelated), DES-18 (HU-12, Done — unrelated), DES-19 (HU-13, Done — unrelated), DES-20 (HU-14A, Done — unrelated), DES-21 (HU-14B, Done — unrelated)
- PRD DES id: DES-62 (local file: `backend/docs/prd/DES-62-mission-design-service-baseline.md`)
- Branch: `feature/hu-10a-mission-hierarchy-structure` (branch from `develop`; no same-service predecessor is currently In Progress)

## Required design patterns

- `Composite`
  - Why: `Mission` owns the hierarchical authoring structure, and `MissionNode` must model the `Stage`, `Substage`, and `Clue` tree coherently while treasure-hunt `Target`s stay attached to their owning `Substage`. The domain (not handlers) enforces which children each node admits.
  - Phase owner: X.1 Domain, carried through X.2 Application and X.4 API contract shape.
  - Concrete obligation: the mission model must be a real Composite over ordered `MissionNode`s (`Stage` -> `Substage` -> optional `Clue`), with containment rules, play-mode enforcement, and traversal/readiness logic centralized in domain entities and policy rather than flattened records or handler conditionals.

> Resolution note: `backend/docs/trivia_sprint_required_patterns_matrix.md` omits HU-10A because mission authoring was excluded from that trivia sprint matrix (line 5: "TreasureHunt/mission HUs are out of scope for this sprint and are omitted"). The mandatory `Composite` obligation comes from `backend/docs/adr/0004-required-domain-patterns.md` ("`Composite` for `Mission` and hierarchical `MissionNode` modeling"), `backend/services/mission-design-service/CONTEXT.md` §Required Patterns, `backend/docs/ddd_solution_model.md` §MissionDesign pattern mapping (line 427), and the realignment overlay. HU-09's rebuild already implemented this pattern structurally; HU-10A verifies it and builds the frontend that consumes it.

## What predecessors have already landed

`mission-design-service` has landed mission and trivia code on `develop`. DES-14 (HU-09) was a `needs-rebuild` ticket that rebuilt the entire mission model around `Mission` as a source-content wrapper with a `MissionNode` Composite, `SubstagePlayMode`, `Target`-based treasure hunt, optional `Clue` guidance, and `MissionActivationPolicy` readiness validation. The rebuild went through all four backend phases (Domain, Application, Infrastructure, API) and is fully committed and tested. The HU-09 frontend plan (`frontend/plans/hu-09-frontend-mission-management.md`) explicitly covers only the 5 base CRUD endpoints and defers hierarchy authoring to HU-10A.

### Build-on predecessor: DES-14 (HU-09) — full detail

**Domain layer** (committed, c561867)
- `Mission` aggregate root — source-content wrapper (`Difficulty`, `MaximumTime`, `MissionActivation`), not a runtime session; owns ordered `Stage`s via `AddStage`/`AddSubstage`/`AddClue`/`RemoveStage`.
- `MissionNode` abstract Composite — `CanContain`, `AddChild`/`RemoveChild`, `Children`, `Descendants()`. `Stage` admits only `Substage`; `Substage` admits only `Clue`; `Clue` is leaf (`CanContain => false`). Violations throw `InvalidMissionNodeChildException(parent, child)` with message `"A {child} node cannot be placed directly under a {parent} node."`
- `Substage` — exactly one `SubstagePlayMode` (`TreasureHunt`|`Trivia`), set via factory (`CreateTreasureHunt`/`CreateTrivia`), immutable per instance. `EnsurePlayMode(expected)` guards `AddTarget`/`UpdateTarget`/`RemoveTarget`/`SetWinnerScore` (require `TreasureHunt`) and `SelectTriviaQuiz` (requires `Trivia`). Mismatch throws `SubstagePlayModeMismatchException(expected, actual)`.
- `Target` — QR objective under a TreasureHunt substage (not a MissionNode); ordered by `SequenceOrder`; `AssociateClue(clueId)` throws `TargetMayReferenceAtMostOneClueException` if a different clue is already set (idempotent for same clue).
- `Clue` — leaf MissionNode; `Text`, `Visibility` (`ClueVisibilityPolicy`, default `HiddenUntilOperatorRelease`). Optional guidance, not an objective.
- `Substage.AssociateClueWithTarget` — checks same-substage (`ClueMustBelongToSameSubstageException`) and max-one-per-target (`TargetMayReferenceAtMostOneClueException`).
- `MissionActivationPolicy` (static domain service) — `EvaluateReadiness(mission)` returns failures: mission must have >=1 stage; every stage >=1 substage; every TreasureHunt substage >=1 active target + `WinnerScore`; every Trivia substage a non-null `TriviaQuizId`. `Mission.Activate()` runs the policy and throws `MissionNotReadyForActivationException(failures)` on failure.

**Application layer** (committed, 9eef83a + 2954fe3)
- Commands: `CreateMission`, `UpdateMission`, `DeactivateMission`, `ActivateMission`, `AddMissionNode`, `UpdateMissionNode`, `RemoveMissionNode`, `AssignSubstagePlayMode`, `AddTarget`, `UpdateTarget`, `RemoveTarget`, `AssociateClueWithTarget`, `UnassociateClueFromTarget`, `SetTriviaQuizSelection`, `UpdateTriviaQuizSelection`.
- Queries: `GetMissionCatalog`, `GetMissionDetail`, `GetMissionReadiness`, `GetDifficultyCatalog`.
- `MissionStructureEditor` (internal static) — structure mutation façade: `RenameNode`, `RemoveNode` (rejects clue removal if associated to a target), `AssignPlayMode` (rebuilds substage, copies clues only — targets/winner-score/trivia-selection are lost on mode switch by design), `UnassociateClueFromTarget`, `RefreshReadiness`.
- `TriviaQuizSelectionGuard` — validates published quiz selection for trivia substages.
- All commands `[Authorize(Roles = Roles.Administrator)]` via `AuthorizationBehaviour`.

**Infrastructure / API** (committed)
- `MissionConfiguration` — EF owned-type config: `Mission` -> `OwnsMany<Stage>` -> `OwnsMany<Substage>` -> `OwnsMany<Target>` + `OwnsMany<Clue>`. Migration `20260618215638_AddMissionCompositePersistence`.
- `MissionRepository` — `AsSplitQuery()` for deep owned tree loading.
- `/api/missions` full endpoint set: CRUD, activate, readiness, nodes (add/update/remove), play-mode assign, targets (add/update/remove), clue-association (post/delete), trivia-quiz-selection (post/put). All `Administrator`-only mutations.
- Response shapes include the full Composite tree: `MissionResponse` -> `MissionStageResponse` -> `MissionSubstageResponse` (PlayMode, WinnerScore, TriviaQuizSelection, Targets[], Clues[]) -> `MissionTargetResponse` (ClueId) / `MissionClueResponse` (Text, VisibilityPolicy).

**Frontend** (partial — CRUD only)
- `frontend/plans/hu-09-frontend-mission-management.md` covers only 5 base CRUD endpoints (create/list/detail/update/deactivate). Explicitly defers hierarchy authoring to HU-10A: "Mission hierarchy (stages, substages, clues). HU-10A/10B owns that."
- No frontend wiring exists for: nodes, play-mode, targets, clue-association, trivia-quiz-selection, readiness, activate.

**Coverage:** 95.46% (per hu09-context.md X.4 commit).

### Unrelated predecessors (landed, untouched by this HU)

- DES-17 (HU-11, Done) — trivia quiz creation/editing. Different aggregate (`TriviaQuiz`).
- DES-18 (HU-12, Done) — trivia publication/archival. Different aggregate.
- DES-19 (HU-13, Done) — trivia duplication/retirement. Different aggregate.
- DES-20 (HU-14A, Done) — trivia question management. Different aggregate.
- DES-21 (HU-14B, Done) — trivia question validation. Different aggregate.

## What this HU adds

| Concern | New work |
|---|---|
| Backend verification | Verify that HU-09's rebuild satisfies all of HU-10A's AC items — containment rules, play-mode enforcement, target operations, clue association, readiness. No new backend domain types. |
| Exception mapping (already done — verify) | `MissionAlreadyDeactivatedException` is **already mapped to 409 Conflict** in `ProblemDetailsExceptionHandler` by HU-09 (shared arm with `MissionAlreadyActiveException`), with a passing test (`ProblemDetailsExceptionHandlerTests.TryHandleAsync_MissionAlreadyDeactivatedException_Returns409`). The original "falls through to 500 / map to 400" premise was **stale** — corrected by the HU-10A driver (X.2, 2026-06-19). No code change needed; verify only. 409 is the correct state-conflict status and stays consistent with its sibling exception. |
| Frontend hierarchy authoring | Build the mission structure authoring UI: stage/substage/clue tree editor, play-mode selection, target authoring, clue association, trivia-quiz selection, readiness display, and mission activation. |
| Frontend types | Extend `definitions.ts` with hierarchy response types (stages, substages, targets, clues, trivia selection, readiness) matching the existing backend contract. |
| Frontend API client | Extend `app/lib/missions.ts` with functions for all structure endpoints (nodes, play-mode, targets, clue-association, trivia-quiz-selection, readiness, activate). |
| Frontend server actions | Extend `app/actions/missions.ts` with server actions for structure mutations. |
| Frontend UI components | Mission structure editor panel consuming the existing `/api/missions` contract, showing explicit rejection messages from the backend. |

## Touched surfaces

- `backend/services/mission-design-service` — verification only; no code change (the planned exception-mapping fix rested on a stale premise — already 409 Conflict). All other backend layers are already complete from HU-09
- `frontend/` — main work: mission hierarchy authoring UI, types, API client, server actions
- `backend/frontend` API contract boundary: no contract changes — the existing `/api/missions` contract from HU-09 is the surface the frontend consumes
- Future backend integration boundary: `SessionOperations` consumes mission readiness facts; unchanged by this HU

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| _(none yet)_ | | |

## Known quirks / gotchas

- HU-09's rebuild already implemented the **entire backend** for HU-10A's AC. The backend phases (X.1-X.4) are **verification and minor refinement**, not new implementation. Do not rebuild domain types that already exist and pass tests.
- The `ready-for-agent` label was missing from DES-15 and was added during generation. The ticket was already In Progress (moved 2026-06-18).
- The canon comment on DES-15 mentions `TriviaQuestionSelection`, but the canon authority is `TriviaQuizSelection` (renamed in HU-09's X.2 follow-up, commit 2954fe3). The ticket body uses the correct term ("quiz publicado por identidad").
- `MissionStructureEditor.AssignPlayMode` rebuilds the substage on mode switch and only copies **clues** — targets, winner score, and trivia-quiz selection are lost. This is by design (play-mode is immutable per `Substage` instance) but the frontend should warn the user before a mode switch.
- ~~`MissionAlreadyDeactivatedException` is not mapped — falls through to 500; HU-10A should map to 400.~~ **Corrected (HU-10A driver, X.2, 2026-06-19):** this premise was stale. HU-09 already maps `MissionAlreadyDeactivatedException` to **409 Conflict** (shared arm with `MissionAlreadyActiveException` in `ProblemDetailsExceptionHandler.cs`), covered by a passing test. It does NOT fall through to 500. 409 is the correct status (state conflict) and is consistent with the sibling exception; forcing 400 would break the test and split identical semantics. No code change made.
- `MissionStructureEditor.RefreshReadiness` works by re-calling `mission.UpdateDetails` with unchanged values purely for the `RefreshActivationState` side effect. This is a code smell but functional.
- Clue rename via `MissionStructureEditor.RenameNode` recreates the `Clue` entity to update text/visibility, which does not raise `MissionNodeUpdatedEvent`. This is a known gap but not an AC item.
- HU-10B (DES-16, "Validaciones estructurales de misión") was archived and folded into HU-10A per the realignment map. HU-10A's AC now covers both hierarchy structure and structural validations.
- The PRD (DES-62) says "Modelado detallado de `Target` y flujos QR" is out of scope, but the realignment overlay supersedes that — `Target` modeling is in scope and already implemented by HU-09.
- `TriviaQuizId` is stored without a DB foreign key; referential integrity is enforced only at the application layer (`TriviaQuizSelectionGuard`).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `bd_umbral_entity_spec.md` §Mission/§MissionNode/§Target/§Clue (lines 27-144),
> `ddd_solution_model.md` §MissionDesign (lines 71-84, 199-218, 289-311, 368-373, 414-431),
> `services/mission-design-service/CONTEXT.md` §Mission Authoring / §Required Patterns / §Boundary Rules,
> `grilling-session-mission-restructure.md` §Mission Hierarchy / §Mission Readiness (lines 22-135),
> and `canon-realignment-after-mission-runtime-rewrite.md` §Canon delta / Mission hierarchy (lines 20-30).
>
> **Critical:** HU-09's rebuild (DES-14) already implemented the full backend Composite model,
> application layer, infrastructure, and API. The backend phases below are **verification and
> minor refinement** — do not create new domain types or rebuild existing ones. The main new
> work is the frontend (Step 9 of the prompt file).

### Phase X.1 — Domain *(verification — existing HU-09 implementation satisfies AC)*
**Derive** (`bd_umbral_entity_spec.md` §Mission (line 27), §MissionNode (line 59), §Target (line 104), §Clue (line 135); `grilling-session-mission-restructure.md` §Mission Hierarchy (line 22), §Mission Readiness (line 114); `canon-realignment-after-mission-runtime-rewrite.md` §Canon delta (line 27); `CONTEXT.md` §Required Patterns / Composite (line 85)):
- No new domain types — HU-09's rebuild already implemented the full Composite model per canon.
- **Verify** the following AC items are satisfied by existing code + tests:
  - `MissionNode` Composite: `Stage` → `Substage` → `Clue` hierarchy with `CanContain` enforcement (`Stage` admits only `Substage`; `Substage` admits only `Clue`; `Clue` is leaf) — `MissionNodeTests.cs`
  - Every rejection throws `InvalidMissionNodeChildException(parent, child)` with message including both parent and child node types — `MissionNodeTests.cs`
  - Each `Substage` has exactly one `SubstagePlayMode` (`TreasureHunt`|`Trivia`), immutable per instance — `SubstageTests.cs`
  - TreasureHunt: `Target` operations (`AddTarget`/`UpdateTarget`/`RemoveTarget`/`SetWinnerScore`) rejected if substage is not `TreasureHunt` — `SubstageTests.cs`
  - Trivia: `SelectTriviaQuiz` rejected if substage is not `Trivia`; `Target` operations don't apply — `SubstageTests.cs`
  - `Clue` optional, at most one per `Target` of same substage (`AssociateClueWithTarget` checks same-substage + max-one) — `MissionStructureTests.cs`
  - Each `Stage` must contain >=1 `Substage` for runtime readiness (`MissionActivationPolicy.EvaluateReadiness`) — `MissionActivationPolicyRuntimePlanTests.cs`

**Target files** (no new files — verify existing):
- verify `Domain/Entities/{Mission,MissionNode,Stage,Substage,Target,Clue}.cs` — canon-aligned, no changes needed
- verify `Domain/Services/MissionActivationPolicy.cs` — readiness rules match AC
- verify `Domain/Exceptions/InvalidMissionNodeChildException.cs` — message includes parent + child types

**Pattern this phase owns:** `Composite` (structurally present in existing HU-09 code — verify, do not rebuild)
**Gate:** run existing domain unit tests (`MissionNodeTests`, `MissionStructureTests`, `SubstageTests`, `MissionActivationPolicyTests`, `MissionActivationPolicyRuntimePlanTests`); confirm all AC items pass; no new domain types created; Composite structurally present as `Mission` -> `MissionNode(Stage/Substage/Clue)`, not flattened records

### Phase X.2 — Application *(verification + minor refinement)*
**Derive** (`ddd_solution_model.md` §MissionDesign application services (lines 199-218), events (lines 289-311), repositories (lines 368-373); `CONTEXT.md` §Boundary Rules (line 75)):
- No new commands/handlers/validators — HU-09's rebuild already implemented all structure commands.
- **Verify** the following AC items are satisfied by existing code + tests:
  - `AddMissionNodeCommand` handles Stage/Substage/Clue creation with correct parent routing — `MissionStructureCommandHandlerTests.cs`
  - `AssignSubstagePlayModeCommand` rebuilds substage with new mode — `MissionMutationCommandHandlerTests.cs`
  - `AddTargetCommand`/`UpdateTargetCommand`/`RemoveTargetCommand` enforce TreasureHunt mode — `MissionStructureCommandHandlerTests.cs`
  - `AssociateClueWithTargetCommand`/`UnassociateClueFromTargetCommand` enforce same-substage + max-one — `MissionStructureCommandHandlerTests.cs`
  - `SetTriviaQuizSelectionCommand` validates published quiz via `TriviaQuizSelectionGuard` — `MissionStructureCommandHandlerTests.cs`
  - `ActivateMissionCommand` runs `MissionActivationPolicy` and returns readiness failures — `MissionStructureCommandHandlerTests.cs`
- **~~Refinement~~ — NO CODE CHANGE (corrected by HU-10A driver, 2026-06-19):** the block originally said to map `MissionAlreadyDeactivatedException` to 400 "currently falls through to 500". That premise is **stale**: HU-09 already maps it to **409 Conflict** in `Api/Services/ProblemDetailsExceptionHandler.cs` (shared arm with `MissionAlreadyActiveException`), with a passing test (`ProblemDetailsExceptionHandlerTests.TryHandleAsync_MissionAlreadyDeactivatedException_Returns409`). 409 is the correct state-conflict status and stays consistent with the sibling exception. X.2 is therefore **verification-only** — verify the existing 409 mapping + test, do not change it.

**Target files** (verify only — no edits):
- verify `Api/Services/ProblemDetailsExceptionHandler.cs` — `MissionAlreadyDeactivatedException` already mapped to 409 (shared arm with `MissionAlreadyActiveException`); no edit
- verify `Application/Missions/{Commands,Handlers,Validators}/*` — canon-aligned, no changes needed
- verify `Application/Missions/Common/{MissionStructureEditor,MissionCommandHandlerBase,MissionDtoMapper,TriviaQuizSelectionGuard}.cs` — canon-aligned

**Pattern this phase owns:** `Composite` preserved in handlers (verify — no flattened traversal logic in handlers)
**Gate:** run existing application unit tests (`MissionStructureCommandHandlerTests`, `MissionMutationCommandHandlerTests`, validator tests); confirm all AC items pass; Composite preserved in handlers (no flattened traversal logic — structure mutation stays in `MissionStructureEditor`); exception mapping verified by the existing `MissionAlreadyDeactivatedException` -> 409 test; no `SessionMode` in commands/DTOs

### Phase X.3 — Infrastructure *(verification — existing HU-09 implementation satisfies AC)*
**Derive** (`bd_umbral_entity_spec.md` §Mission/§MissionNode/§Target/§Clue persistence; `ddd_solution_model.md` §MissionDesign repositories (lines 368-373)):
- No new migration — HU-09's `20260618215638_AddMissionCompositePersistence` migration already persists the full Composite hierarchy.
- **Verify** the following:
  - `MissionConfiguration` maps `Mission` -> `OwnsMany<Stage>` -> `OwnsMany<Substage>` -> `OwnsMany<Target>` + `OwnsMany<Clue>` with correct indexes
  - `MissionRepository` loads the deep owned tree via `AsSplitQuery()`
  - Migration snapshot includes `MissionStages`, `MissionSubstages`, `MissionClues`, `MissionTargets` tables
  - Integration test `MissionEndpointsTests.MissionAuthoringEndpoints_BuildTreeExposeReadinessAndActivateReadyMission` proves repository round-trip

**Target files** (no new files — verify existing):
- verify `Infrastructure/Persistence/Configurations/MissionConfiguration.cs` — owned-type config matches Composite
- verify `Infrastructure/Persistence/Migrations/20260618215638_AddMissionCompositePersistence.cs` — schema matches domain
- verify `Infrastructure/Repositories/MissionRepository.cs` — split-query loading
- Check the snapshot by **grep `MissionNode`/`Target` in `ApplicationDbContextModelSnapshot.cs`** — do not full-read it

**Pattern this phase owns:** none
**Gate:** run existing integration tests (`MissionEndpointsTests`, `MissionInfrastructureIntegrationTests`); confirm repository round-trip of Stage/Substage/Target/Clue/TriviaQuizSelection; no new migration needed; no `SessionMode` in schema/read models

### Phase X.4 — Api *(verification — existing HU-09 implementation satisfies AC)*
**Derive** (`ddd_solution_model.md` §MissionDesign pattern mapping (lines 425-431); `CONTEXT.md` §Required Patterns / Composite (line 85)):
- No new endpoints — HU-09's rebuild already exposed all structure endpoints under `/api/missions`.
- **Verify** the following AC items are satisfied by existing code + tests:
  - `POST /api/missions/{missionId}/nodes` — add Stage/Substage/Clue with correct parent routing
  - `PUT /api/missions/{missionId}/nodes/{nodeId}` — rename node / update clue
  - `DELETE /api/missions/{missionId}/nodes/{nodeId}` — remove node (rejects clue associated to target)
  - `PUT .../play-mode` — assign play mode
  - `POST/PUT/DELETE .../targets` — target CRUD (TreasureHunt only)
  - `POST/DELETE .../clue-association` — associate/unassociate clue
  - `POST/PUT .../trivia-quiz-selection` — set/update trivia quiz (Trivia only)
  - `GET .../readiness` — readiness failures
  - `POST .../activate` — activate mission (runs readiness policy)
  - `MissionResponse` includes full Composite tree (stages -> substages -> targets/clues)
  - All mutations `Administrator`-only via `[Authorize]` + `AuthorizationBehaviour`
  - Exception mapping: `MissionAlreadyDeactivatedException` returns 409 Conflict (already mapped by HU-09; verified in X.2, no code change)

**Target files** (no new files — verify existing):
- verify `Api/Endpoints/MissionsEndpoints.cs` — full endpoint set matches AC
- verify `Api/Services/ProblemDetailsExceptionHandler.cs` — `MissionAlreadyDeactivatedException` -> 409 Conflict (already mapped by HU-09, shared arm with `MissionAlreadyActiveException`; verify only, no code change)

**Pattern this phase owns:** `Composite` visible in the detail contract (verify — `MissionResponse` -> `MissionStageResponse` -> `MissionSubstageResponse` -> `MissionTargetResponse` / `MissionClueResponse`)
**Gate:** run existing endpoint tests (`MissionEndpointsTests`); confirm all AC items pass; Composite visible in the detail contract (`MissionResponse` -> `MissionStageResponse` -> `MissionSubstageResponse` -> `MissionTargetResponse` / `MissionClueResponse`, not flattened); `MissionAlreadyDeactivatedException` returns 409 Conflict; service coverage >= repo gate (ADR-0005); no `SessionMode` in any payload
