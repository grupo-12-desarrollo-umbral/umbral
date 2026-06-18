# Prompt Example - HU-09 Gestion de misiones rebuild (Feature Slice)

Concrete prompt sequence for driving the DES-14 rebuild of HU-09 through a full feature slice on `feature/hu-09-mission-management-rebuild`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-14 rebuild:** the old HU-09 mission-management baseline predates the mission-runtime rewrite. This slice is not an additive patch. It rebuilds mission management around `Mission` as a source-content wrapper and around a runtime-ready mission plan: ordered `Stage`s, `Substage`s with exactly one `SubstagePlayMode`, target-based treasure-hunt objectives, optional clues, and trivia substages that select published trivia questions. Runtime session creation remains outside this slice.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- `Mission` as wrapper, not runtime session
- `MissionNode` Composite: `Stage`, `Substage`, `Clue`
- each `Substage` has exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`
- treasure-hunt progress is `Target` based, not clue based
- `Clue` is optional guidance, max one per target
- readiness/activation validates the runtime plan
- no `SessionMode`
- no `TriviaQuiz` as `SessionSource`

---

## Required design patterns

- `Composite`
  - Why: `Mission` owns the hierarchical runtime-plan authoring structure, and `MissionNode` must model the `Stage`, `Substage`, and `Clue` tree coherently while treasure-hunt `Target`s stay attached to their owning `Substage`.
  - Phase owner: X.1 Domain, carried through X.2 Application and X.4 API contract shape.
  - Gate obligation: the rebuilt mission model must be a real Composite over ordered `MissionNode`s (`Stage` -> `Substage` -> optional `Clue`), with traversal/readiness rules centralized in domain policy rather than flattened records or handler conditionals.

> Resolution note: `backend/docs/trivia_sprint_required_patterns_matrix.md` omits HU-09 because mission authoring was excluded from that trivia sprint matrix. For this DES-14 rebuild, the mandatory `Composite` obligation comes from `backend/docs/adr/0004-required-domain-patterns.md`, `backend/services/mission-design-service/CONTEXT.md`, `backend/docs/ddd_solution_model.md`, and the 2026-06-16 realignment overlay.

---

## Pre-resolved orient (as of 2026-06-16)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-14 (HU-09) itself is **Done** but marked `needs-rebuild` and `canon-realign`. Treat the existing mission-management implementation as stale baseline code to inspect, delete, or reconcile, not as authoritative scope. Later same-service trivia slices are also Done: DES-17 (HU-11), DES-20 (HU-14A), DES-21 (HU-14B), DES-18 (HU-12), and DES-19 (HU-13). There are no same-service In Progress predecessors, so the branch base is `develop`.

**Domain layer**

- Old mission baseline exists, but it must be rebuilt around `Mission` as wrapper/source content, not runtime session.
- `MissionDesign` owns `Mission`, `MissionNode`, `Target`, `Clue`, `TriviaQuiz`, `TriviaQuizSelection`, `Difficulty`, `MaximumTime`, and `MissionActivation`.
- `MissionNode` must be a Composite over `Stage`, `Substage`, and `Clue`.
- Each `Substage` has exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`; there is no `SessionMode`.
- Treasure-hunt progression is `Target` based, not clue based; `Clue` is optional guidance and max one clue can guide a target.
- Trivia substages select questions from a published `TriviaQuiz`; `TriviaQuiz` is not a `SessionSource`.

**Application layer**

- Existing mission create/update/deactivate/catalog/detail flows may exist, but command/query shapes must be reconciled with the rebuilt runtime-plan model.
- `AuthorizationBehaviour` and `ValidationBehaviour` already exist in the service pipeline.
- Trivia-side published quiz behavior exists from later HUs and can be used to validate `TriviaQuizSelection`; do not let it become direct session creation scope.

**Infrastructure / API**

- EF Core persistence, repositories, and `/api/missions` endpoints exist from old HU-09.
- The rebuild likely changes mission persistence shape for `MissionNode`, `Target`, clue association, play mode, and readiness/activation state.
- API contract must expose mission wrapper metadata plus runtime-plan authoring and readiness feedback.

**Frontend**

- Mission-management UI exists from old HU-09 and must be realigned to the new mission runtime-plan contract.
- Trivia administration UI exists and may support published quiz selection for trivia substages.

**Coverage:** no stable aggregate percentage is captured in predecessor docs for `mission-design-service`; verify real service coverage during phase X.4.

### What HU-09 rebuild adds on top (per DES-14, DES-62, and the realignment overlay)

| Concern | New work |
|---|---|
| Rebuild posture | Rebuild mission management instead of patching stale mission-session assumptions. |
| Mission wrapper | `Mission` is source content for future `LiveSession` creation; it is not a runtime session. |
| `MissionNode` Composite | Ordered `Stage`, `Substage`, and optional `Clue` nodes under one aggregate. |
| Play mode | Every `Substage` declares exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`; no `SessionMode`. |
| Treasure hunt | `Target` is the QR-validated objective; progress is target-based. |
| Clues | Optional guidance only, max one clue per target; clue visibility/release does not advance progress. |
| Trivia substages | Reference one whole published `TriviaQuiz` through a `TriviaQuizSelection`; no `TriviaQuiz` as `SessionSource`. |
| Readiness/activation | Validate the full runtime plan before source readiness/activation: stages, substages, play modes, targets/winner score, trivia selections. |
| Backend contract | Rebuilt `/api/missions` payloads for mission metadata, nodes, targets, clues, trivia selections, deactivation, and readiness. |
| Frontend flow | Admin UI for authoring and inspecting the rebuilt mission runtime plan. |

### Branch state and prerequisite

`feature/hu-09-mission-management-rebuild` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** inspect the current mission-design source and identify stale mission-session assumptions. Reconcile or delete conflicting code; do not create parallel mission types.

### Linear state (as of 2026-06-16)

- DES-14 (HU-09): **Done**, labels: `Feature`, `Validate criteria`, `ready-for-agent`, `svc:mission-design-service`, `missing-mission-sprint`, `canon-realign`, `needs-rebuild`
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`, `canon-realign`
- Same-service Done issues: DES-17, DES-20, DES-21, DES-18, DES-19
- Same-service In Progress issues: none

> Linear live state may have changed. Use the Linear MCP to verify DES-14 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

> Note: the local PRD file is authoritative for original HU-09/HU-14 scope, but the realignment overlay supersedes stale PRD lines that made detailed `Target` modeling out of scope or implied trivia as a session source.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-06-16.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md - current service status
- @backend/services/mission-design-service/CONTEXT.md - bounded-context language and pattern expectations
- @backend/docs/prd/DES-62-mission-design-service-baseline.md - original PRD for HU-09 to HU-14
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment overlay; it supersedes stale DES-62 assumptions for DES-14
- @backend/docs/hu09-context.md - the pre-resolved HU-09 rebuild context

Then inspect the existing mission-design source only enough to identify stale or reusable baseline code:
- @backend/services/mission-design-service/src/Domain/
- @backend/services/mission-design-service/src/Application/Missions/
- @backend/services/mission-design-service/src/Infrastructure/Persistence/
- @backend/services/mission-design-service/src/Api/Endpoints/MissionsEndpoints.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-14 (HU-09 - Gestion de misiones) - status and labels
- DES-62 (PRD - MissionDesign service) - status and labels

Output:
- which current Mission concepts are stale and must be rebuilt
- the canonical rebuilt scope: Mission wrapper, MissionNode Composite, Stage/Substage/Clue, SubstagePlayMode, Target, optional Clue, readiness/activation
- confirmation that there is no SessionMode and no TriviaQuiz as SessionSource in this slice
- current Linear status and labels for DES-14

Do not start planning or implementing yet.
```

---

## 2. Label DES-14 as ready-for-agent

> DES-14 already carries `ready-for-agent` as of 2026-06-16. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-14 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-14 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-14 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear; read local files if you need implementation decisions.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- Mission as wrapper, not runtime session
- MissionNode Composite: Stage, Substage, Clue
- each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia
- treasure-hunt progress is Target based, not clue based
- Clue is optional guidance, max one per target
- readiness/activation validates the runtime plan
- no SessionMode
- no TriviaQuiz as SessionSource

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-09` and `DES-14` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above and is overlaid by the canon realignment document.

---

## 4. Start the slice

```text
Prepare the mission-management rebuild slice on branch feature/hu-09-mission-management-rebuild.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what existing code may have
landed and what the DES-14 rebuild adds. Do not re-read the PRD for scoping unless
you need to resolve a precise implementation detail.

Before implementation, inspect whether current Mission source code contradicts the realigned canon.
Treat DES-14 as a rebuild: delete, replace, or reconcile stale model/code paths instead of layering
new types beside old assumptions.

Move DES-14 to In Progress if the team process requires reopening rebuild work, and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-09 rebuild in mission-design-service.
Use DES-62 plus @backend/docs/canon-realignment-after-mission-runtime-rewrite.md and canonical docs.

Before writing anything, inspect existing Mission domain code and remove/reconcile stale mission-session assumptions.
Do not create parallel Mission or MissionNode types.

Scope:
- rebuild Mission as a source-content wrapper aggregate, not a runtime session
- implement the mandated Composite:
  Mission owns ordered MissionNode elements for Stage, Substage, and Clue
- model Stage as top-level ordered node with one or more Substage children for readiness
- model Substage as child of Stage with exactly one SubstagePlayMode: TreasureHunt or Trivia
- model Target as the QR-validated treasure-hunt objective owned by a TreasureHunt Substage
- enforce treasure-hunt progress as Target based, not clue based
- model Clue as optional guidance under a Substage; a Target may reference max one Clue from the same Substage
- ensure clue visibility/release never advances a target or substage
- model trivia Substage selection as TriviaQuizSelection from one published TriviaQuiz
- ensure TriviaQuiz is reusable authoring content, not SessionSource
- ensure there is no SessionMode in MissionDesign
- add or complete domain events needed by rebuilt mission authoring:
  MissionCreated, MissionDetailsUpdated, MissionStructureChanged, MissionNodeAdded,
  MissionNodeUpdated, MissionNodeRemoved, TargetAddedToSubstage, TargetUpdated,
  TargetRemovedFromSubstage, ClueAssociatedWithTarget, MissionActivated, MissionDeactivated
- implement MissionActivationPolicy so readiness/activation validates the runtime plan:
  every stage has substages, every substage has exactly one play mode, TreasureHunt substages
  have active targets and winner score, Trivia substages have valid published question selections
- keep activation/readiness distinct from runtime LiveSession lifecycle

Gate:
- Domain build passes
- New domain invariants are expressed as unit tests on Mission, MissionNode, Target, and MissionActivationPolicy
- Gate: Composite is structurally present as Mission -> MissionNode(Stage/Substage/Clue), not flattened records or handler traversal
- Gate: each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia
- Gate: treasure-hunt progression is Target based; Clue is optional guidance and max one per target
- Gate: readiness/activation validates the runtime plan
- Gate: no SessionMode exists in MissionDesign
- Gate: TriviaQuiz is not modeled as SessionSource

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 - domain layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-09 rebuild in mission-design-service.
Use DES-62 plus @backend/docs/canon-realignment-after-mission-runtime-rewrite.md and canonical docs.

Before writing anything, inspect current Application/Missions code and remove/reconcile commands,
queries, validators, or DTOs that expose stale mission-session assumptions.

Scope:
- repository interfaces for the rebuilt Mission aggregate and read models:
  IMissionRepository and IMissionReadModelRepository if missing or stale
- commands + handlers + validators:
  CreateMission, UpdateMission, DeactivateMission
- structure commands + handlers + validators needed for the rebuilt runtime plan:
  add/update/remove MissionNode, assign SubstagePlayMode, add/update/remove Target,
  associate/unassociate optional Clue to Target, set/update TriviaQuizSelection
- activation/readiness command/query:
  validate runtime plan and activate/source-ready Mission only through MissionActivationPolicy
- queries + handlers:
  GetMissionCatalog and GetMissionDetail with Stage/Substage/Target/Clue/TriviaQuizSelection shape
- authorization boundary:
  mission mutations and activation are Administrator-only through existing authorization plumbing
- validation:
  required mission authored fields, hierarchy rules, exactly one SubstagePlayMode per Substage,
  Target ownership, max one Clue per Target, published TriviaQuiz selection, not-found handling
- reject or avoid any command/DTO shape that introduces SessionMode or TriviaQuiz as SessionSource

Gate:
- clean build passes
- handler and validator unit tests cover valid path plus rejection/error branches
- Gate: application use cases preserve the Composite and do not flatten traversal logic into handlers
- Gate: activation/readiness handlers validate the runtime plan through domain policy
- Gate: no SessionMode appears in commands, DTOs, handlers, or validators
- Gate: no command treats TriviaQuiz as SessionSource
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 - application layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-09 rebuild in mission-design-service.
Use DES-62 plus @backend/docs/canon-realignment-after-mission-runtime-rewrite.md and canonical docs.

Before writing anything, inspect current persistence scaffold:
- ApplicationDbContext
- MissionConfiguration
- MissionNode/Target configurations if present
- existing migrations and model snapshot

Scope:
- replace or migrate stale Mission persistence to the rebuilt aggregate shape
- persist Mission wrapper metadata, activation/readiness state, deactivation/archive timestamp
- persist MissionNode Composite data: Stage, Substage, Clue, parent/child relationship, sequence order
- persist exactly one SubstagePlayMode for every Substage
- persist Target under TreasureHunt Substage, including validation type/expected value/active flag
- persist optional Clue association with max one Clue per Target and same-Substage constraint
- persist TriviaQuizSelection reference for Trivia Substage without treating TriviaQuiz as SessionSource
- update read-model repository queries so Mission detail returns the runtime-plan shape needed by API/frontend
- create EF migration if the current snapshot does not match rebuilt canon; do not preserve stale schema to avoid migration work
- integration tests for persistence and read model reconstruction across Stage/Substage/Target/Clue/Trivia selection

Gate:
- build passes
- EF migration/snapshot accurately represents Mission wrapper, MissionNode Composite, Target, optional Clue association, SubstagePlayMode, and TriviaQuizSelection
- repository integration tests prove round-trip persistence for the rebuilt runtime plan
- Gate: database/read models contain no SessionMode concept
- Gate: no persistence model treats TriviaQuiz as SessionSource

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 - infrastructure layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-09 rebuild in mission-design-service.
Use DES-62 plus @backend/docs/canon-realignment-after-mission-runtime-rewrite.md and canonical docs.

Before writing anything, inspect the current MissionsEndpoints contract and identify stale response/request shapes.
Replace stale contract surfaces rather than adding parallel endpoints for the same behavior.

Scope:
- rebuild /api/missions create, list, detail, update, deactivate endpoints around Mission wrapper metadata
- expose runtime-plan authoring endpoints or nested payloads for:
  Stage, Substage, SubstagePlayMode, Target, optional Clue, and TriviaQuizSelection
- expose activation/readiness endpoint or response field that reports runtime-plan validation failures
- endpoint policy: Administrator-only for mission mutations and activation/readiness mutation
- response shape: Mission detail includes ordered stages, ordered substages, exact play mode, targets,
  optional clue association, trivia selection, activation/readiness state
- reject stale API shapes that contain SessionMode or direct TriviaQuiz SessionSource
- map domain/application validation errors to appropriate ProblemDetails responses
- maintain existing mission catalog/detail semantics for frontend consumers, updated to rebuilt shape

Gate:
- endpoint tests pass for create/update/deactivate/detail/readiness and at least one invalid runtime-plan rejection
- Gate: API detail proves Mission as wrapper, MissionNode Composite, exactly one SubstagePlayMode, Target-based treasure hunt, optional Clue max one per target
- Gate: no API request/response contains SessionMode
- Gate: no API request/response treats TriviaQuiz as SessionSource
- service coverage reaches the repo gate target

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 - api layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build mission-design-service api-gateway
docker compose up -d mission-design-service api-gateway

Run curl smoke checks through the gateway for:
- mission catalog
- mission detail for a rebuilt mission
- readiness/activation validation response

Output:
- container status
- smoke command results
- any API contract changes that the frontend phase must consume
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in `@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-09 mission-management rebuild.

Scope:
- update mission-management data client/types to match the rebuilt backend contract
- mission catalog/detail remain administrator-facing but show rebuilt readiness/activation state
- mission editor supports Mission wrapper metadata and ordered runtime-plan authoring:
  Stage, Substage, SubstagePlayMode, Target, optional Clue, and TriviaQuizSelection
- Substage editor forces exactly one play mode: TreasureHunt or Trivia
- TreasureHunt editor manages Target objectives and optional clue guidance; do not present clues as progress objectives
- Trivia editor selects published TriviaQuiz questions for a Trivia Substage; do not create a trivia session from a quiz
- readiness UI displays runtime-plan validation failures from backend
- remove or rewrite stale UI copy/types that mention SessionMode or TriviaQuiz as SessionSource

Gate:
- frontend typecheck/build passes
- mission-management flow exercises create/edit/detail/deactivate/readiness against the rebuilt API contract
- Gate: UI exposes Mission as wrapper, MissionNode Composite, exactly one SubstagePlayMode, Target-based treasure hunt, optional Clue max one per target
- Gate: no UI type/copy introduces SessionMode
- Gate: no UI type/copy treats TriviaQuiz as SessionSource

Do not modify backend code in this step.
```

Commit:

```text
feat(frontend): rebuild mission management runtime plan - HU-09

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-14 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- Mission as wrapper, not runtime session
- MissionNode Composite: Stage, Substage, Clue
- each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia
- treasure-hunt progress is Target based, not clue based
- Clue is optional guidance, max one per target
- readiness/activation validates the runtime plan
- no SessionMode
- no TriviaQuiz as SessionSource

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract changes
- frontend plan/file produced
- tests and gates run
- any unresolved ambiguity for DES-15 follow-up

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-09-mission-management-rebuild \
  --title "feat(mission-design): rebuild HU-09 mission management" \
  --body "Rebuilds DES-14/HU-09 mission management around the realigned mission runtime model: Mission wrapper, MissionNode Composite, SubstagePlayMode, Target-based treasure hunt, optional Clue guidance, and runtime-plan readiness/activation."
```

---

## Rationale

The old HU-09 prompt treated mission management as a basic CRUD baseline and deferred detailed `Target` modeling. That is no longer safe after the 2026-06-16 mission-runtime rewrite. DES-14 is tagged `needs-rebuild` because downstream session creation now depends on `Mission` as the only source for `LiveSession`, with an immutable runtime snapshot derived from the authored mission plan. If HU-09 keeps stale assumptions, later DES-15, DES-22, DES-24, and the rebuilt session tickets will inherit the wrong source model.

The required pattern also differs from the trivia slices. HU-11 through HU-14 primarily use `Template Method` for quiz validation flows. DES-14 rebuild is about hierarchical mission authoring, so the controlling obligation is `Composite`: `Mission` owns a `MissionNode` tree for `Stage`, `Substage`, and `Clue`, while treasure-hunt `Target`s remain target objectives under the owning treasure-hunt `Substage`. Readiness/activation then validates that complete runtime plan without introducing runtime session state, `SessionMode`, or `TriviaQuiz` as a direct session source.
