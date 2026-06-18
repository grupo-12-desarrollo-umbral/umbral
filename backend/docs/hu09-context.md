# HU-09 Context - Gestion de misiones rebuild

> Paste this section into any agent session that needs context for HU-09.
> Last updated: 2026-06-16 | Branch: `feature/hu-09-mission-management-rebuild`

## State

- DES-14 (HU-09): **Done**, labels: `Feature`, `Validate criteria`, `ready-for-agent`, `svc:mission-design-service`, `missing-mission-sprint`, `canon-realign`, `needs-rebuild`
- Same-service landed context: DES-17 (HU-11) **Done**, DES-20 (HU-14A) **Done**, DES-21 (HU-14B) **Done**, DES-18 (HU-12) **Done**, DES-19 (HU-13) **Done**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`, `canon-realign`; related to DES-14 but missing `svc:mission-design-service` in Linear, so use local PRD file authority
- Branch: `feature/hu-09-mission-management-rebuild` (branch from `develop`; no same-service predecessor is currently In Progress)

## Required design patterns

- `Composite`
  - Why: `Mission` owns the hierarchical runtime-plan authoring structure, and `MissionNode` must model the `Stage`, `Substage`, and `Clue` tree coherently while treasure-hunt `Target`s stay attached to their owning `Substage`.
  - Phase owner: X.1 Domain, carried through X.2 Application and X.4 API contract shape.
  - Concrete obligation: rebuild the mission model as a `Mission` wrapper over ordered `MissionNode` composites: `Stage` -> `Substage` -> optional `Clue`; do not flatten the hierarchy into unrelated records or scatter traversal/readiness logic through handlers.

> Resolution note: `backend/docs/trivia_sprint_required_patterns_matrix.md` omits HU-09 because mission authoring was excluded from that trivia sprint matrix. For this DES-14 rebuild, the mandatory `Composite` obligation comes from `backend/docs/adr/0004-required-domain-patterns.md`, `backend/services/mission-design-service/CONTEXT.md`, `backend/docs/ddd_solution_model.md`, and the 2026-06-16 realignment overlay.

## What predecessors have already landed

`mission-design-service` has landed mission and trivia code on `develop`, but DES-14 is tagged `needs-rebuild`; existing mission-management code is a stale baseline to reconcile or replace, not a canonical foundation to extend blindly. The strongest documented reuse sources are HU-11, HU-14A, HU-12, and HU-13 context files. HU-09 and HU-14B have no dedicated earlier context file; `backend/services/mission-design-service/README.md` is intentionally sparse.

**Domain layer**
- Existing `Mission` authoring baseline was implemented before the mission-runtime rewrite and must be rebuilt around `Mission` as a wrapper, not a runtime session.
- `MissionDesign` owns `Mission`, `MissionNode`, `Target`, `Clue`, `TriviaQuiz`, `TriviaQuizSelection`, `Difficulty`, `MaximumTime`, and `MissionActivation`.
- `TriviaQuiz` authoring, question/option management, validation, publication/archive, and duplication/retirement have landed in later HUs; `TriviaQuiz` is reusable authoring content selected into trivia substages, not a session source.
- Current canon requires each `Substage` to have exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`.
- Treasure-hunt progress is `Target` based, not clue based. `Clue` is optional player guidance and at most one clue can guide a target.

**Application layer**
- Mission create/update/deactivate/catalog/detail flows exist from the old DES-14 implementation, but rebuild phases must inspect and delete/reconcile stale command shapes that lack `SubstagePlayMode`, `Target`, and runtime-plan readiness validation.
- Authorization and validation behaviours are already part of the service pipeline.
- Trivia-side use cases and read models exist from HU-11/HU-14A/HU-14B/HU-12/HU-13 and may be reused for trivia-substage selection checks only through contracts owned by `MissionDesign`.

**Infrastructure / API**
- EF Core persistence, repositories, and `/api/missions` endpoints exist from the old baseline.
- Trivia persistence/API surfaces exist for quiz authoring and publication.
- DES-14 rebuild likely requires a replacement migration/model shape for `MissionNode`, `Target`, clue association, and readiness/activation fields; do not preserve stale schema just for compatibility if it contradicts canon.

**Frontend**
- Mission-management UI exists from the original DES-14 slice and trivia administration UI exists from later slices.
- The frontend must be brought back into alignment with the rebuilt mission runtime-plan contract: stage/substage authoring, play mode selection, target authoring, optional clue guidance, and activation readiness feedback.

**Coverage:** no predecessor context file records a stable aggregate percentage for `mission-design-service`; verify real service coverage during phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Rebuild posture | Treat DES-14 as a rebuild of mission management, not an additive patch over stale mission-session assumptions. |
| Mission wrapper | Model `Mission` as the source-content wrapper for future `LiveSession` creation, not as a runtime session and not as a session lifecycle object. |
| `MissionNode` Composite | Rebuild the hierarchy as ordered `Stage`, `Substage`, and `Clue` nodes under one `Mission` aggregate. |
| Substage play mode | Require every `Substage` to declare exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`; there is no session-level `SessionMode`. |
| Target-based treasure hunt | Model treasure-hunt progress through `Target` objectives under treasure-hunt substages, not through clues. |
| Optional clue guidance | Keep `Clue` as optional guidance, with max one clue associated to a target; clue release/visibility does not advance the substage. |
| Trivia substages | A trivia `Substage` references one whole published `TriviaQuiz` through a `TriviaQuizSelection`; `TriviaQuiz` is not a `SessionSource`. |
| Readiness/activation | `MissionActivationPolicy` validates the full runtime plan before a mission can be activated/source-ready: stages, substages, play modes, treasure targets/winner score, and trivia selections. |
| Backend contract | Rebuild `/api/missions` request/response shapes around mission wrapper metadata plus stage/substage/target/clue/trivia-selection authoring and readiness feedback. |
| Frontend flow | Rebuild mission-management UI so administrators can create, edit, inspect, deactivate, and readiness-check the mission runtime plan. |

## Touched surfaces

- `backend/services/mission-design-service`
- `frontend/` mission management and runtime-plan authoring UI
- `backend/frontend` API contract boundary: mission create/update/detail/deactivate, node/target/clue/trivia-selection payloads, and activation/readiness response shape
- Future backend integration boundary: `SessionOperations` consumes mission readiness facts and later immutable `MissionRuntimeSnapshot` data; it must not mutate mission authoring content

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| c561867 | X.1 Domain | Mission/MissionNode Composite, play modes, targets, clues, activation policy |
| 9eef83a | X.2 Application | Mission management application layer (commands/queries/handlers/validators/DTOs) |
| 2954fe3 | X.2 follow-up | Rename `TriviaQuestionSelection` → `TriviaQuizSelection` |
| see git log | X.3 Infrastructure | EF owned-type config, `AddMissionCompositePersistence` migration, repository round-trip, ADR-0005 gate green |
| — | X.4 Api | **Pending** — rebuilt `/api/missions` contract not yet committed |

## Known quirks / gotchas

- DES-14 is already **Done** in Linear but marked `needs-rebuild`; driver prompts must move it back through implementation deliberately rather than assuming Done means aligned.
- The local PRD still contains stale lines saying detailed `Target` modeling is out of scope. The 2026-06-16 realignment overlay supersedes that for DES-14.
- Reject any implementation plan that introduces `SessionMode`, treats `TriviaQuiz` as a direct session source, or models treasure-hunt progress as clue-based.
- A `Clue` is still a `MissionNode`, but it is guidance, not an objective. The objective is `Target`.
- Readiness/activation is not a runtime state transition. It is a `MissionDesign` source-readiness decision over the authored runtime plan.
- HU-10A/DES-15 remains the follow-on hierarchy authoring ticket, but DES-14 rebuild must establish enough correct model shape that DES-15 extends canon instead of repairing stale assumptions again.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Derived from `ddd_solution_model.md` §MissionDesign, `services/mission-design-service/CONTEXT.md`,
> `bd_umbral_entity_spec.md` §Mission/Target/Clue, and `canon-realignment-after-mission-runtime-rewrite.md`.
> X.1/X.2 are already committed — those blocks describe the as-built canon shape (the
> reference X.3/X.4 must match). Open a canonical doc only to fill a gap a block leaves open.

### Phase X.1 — Domain  *(committed — reference only)*
**Derive** (`bd_umbral_entity_spec.md` §Mission, realignment overlay):
- `Mission` aggregate — source-content wrapper (`Difficulty`, `MaximumTime`, `MissionActivation`), **not** a runtime session; owns ordered `MissionNode`s.
- `MissionNode` Composite — `Stage` → `Substage` → optional `Clue` (`MissionNodeType` enum). Child rules enforced via `InvalidMissionNodeChildException`.
- `Substage` — exactly one `SubstagePlayMode` (`TreasureHunt`|`Trivia`); enforced by `SubstageRequiresPlayModeException` / `SubstagePlayModeMismatchException`. No `SessionMode`.
- `Target` — QR objective under a TreasureHunt substage; progress is target-based. Max one `Clue` per target (`TargetMayReferenceAtMostOneClueException`), same substage (`ClueMustBelongToSameSubstageException`).
- `MissionActivationPolicy` (domain service) — validates the runtime plan; `MissionNotReadyForActivationException` / `MissionAlreadyActiveException`.

**Files:** `Domain/Entities/{Mission,MissionNode,Stage,Substage,Target,Clue}.cs`, `Domain/Enums/{SubstagePlayMode,MissionNodeType,MissionActivation,ClueVisibilityPolicy}.cs`, `Domain/Services/MissionActivationPolicy.cs`, `Domain/Events/Mission*.cs` + `Target*.cs` + `ClueAssociatedWithTargetEvent.cs`, `Domain/Exceptions/*`.
**Pattern:** `Composite` (structural — not flattened records).
**Gate:** unit test per new domain type; Composite structurally present; one play mode per substage; target-based progress; readiness validated; no `SessionMode`; `TriviaQuiz` not a `SessionSource`.

### Phase X.2 — Application  *(committed — reference only)*
**Derive** (`ddd_solution_model.md` §MissionDesign application services):
- Mission commands: Create/Update/Deactivate; structure: Add/Update/Remove `MissionNode`, `AssignSubstagePlayMode`, Add/Update/Remove `Target`, Associate/Unassociate `Clue`, Set/Update `TriviaQuizSelection`; `ActivateMission`.
- Queries: `GetMissionCatalog`, `GetMissionDetail`, `GetMissionReadiness`, `GetDifficultyCatalog`.
- Composite traversal centralized in `MissionStructureEditor` / `MissionCommandHandlerBase` — **not** flattened into handlers. `TriviaQuizSelectionGuard` validates published-quiz selection.

**Files:** `Application/Missions/{Commands,Handlers,Queries,DTOs}/*`, `Application/Missions/Common/{MissionStructureEditor,MissionCommandHandlerBase,MissionDtoMapper,TriviaQuizSelectionGuard}.cs`, `Application/Common/Interfaces/{IMissionRepository,IMissionReadModelRepository}.cs`.
**Pattern:** `Composite` preserved; Administrator-only via existing `AuthorizationBehaviour`.
**Gate:** handler + validator tests (valid + rejection branches); no `SessionMode` in commands/DTOs; no `TriviaQuiz` as `SessionSource`.

### Phase X.3 — Infrastructure  *(PENDING — the real remaining work)*
**Derive:** persist the Composite as EF **owned types** under `Mission`: `MissionNode` (Stage/Substage/Clue + sequence order), `Target` (qr/validation/active/score), clue association (max-one, same-substage), `SubstagePlayMode`, `TriviaQuizSelection`. No stale mission-session schema.
**Files:** edit `Infrastructure/Persistence/Configurations/MissionConfiguration.cs` (owned-type config — mirror `TriviaQuizConfiguration.cs`); `Repositories/{MissionRepository,MissionReadModelRepository}.cs` (read-model reconstructs the full tree); **new** migration under `Infrastructure/Persistence/Migrations/` — none exists for the rebuilt model yet.
- Check the snapshot by **grep `MissionNode`/`Target` in `ApplicationDbContextModelSnapshot.cs`** — do not full-read it.
**Pattern:** none.
**Gate:** `ef migrations add` succeeds and represents the Composite; repository integration test proves round-trip of Stage/Substage/Target/Clue/TriviaQuizSelection; no `SessionMode` in schema/read models.

### Phase X.4 — Api  *(PENDING)*
**Derive:** rebuild `/api/missions` — create/list/detail/update/deactivate around `Mission` metadata; nested authoring payloads for Stage/Substage/PlayMode/Target/optional Clue/TriviaQuizSelection; readiness endpoint/field reporting runtime-plan validation failures. Administrator-only mutations.
**Files:** edit `Api/Endpoints/MissionsEndpoints.cs` (mirror `TriviasEndpoints.cs`); map new domain exceptions in `Api/Services/ProblemDetailsExceptionHandler.cs`.
**Pattern:** `Composite` visible in the detail contract.
**Gate:** endpoint tests for create/update/deactivate/detail/readiness + one invalid-plan rejection; ≥ repo coverage gate (ADR-0005); no `SessionMode`, no `TriviaQuiz` as `SessionSource` in any payload.
