# Prompt Example — HU-09 Mission Management (Feature Slice)

Concrete prompt sequence for driving HU-09 through a full feature slice on `feature/hu-09-mission-management`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-09:** this is the first documented `mission-design-service` slice, so there is no same-service predecessor context to extend. The slice establishes the basic `Mission` authoring baseline first, while explicitly keeping source-readiness and mission-structure completion separate for later HUs (`HU-10A` and `HU-10B`). The frontend starts with mission management only; runtime mission-session behavior remains outside this slice.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Pre-resolved orient (as of 2026-05-31)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed (reuse candidates for HU-09)

No prior `mission-design-service` HU slice is currently tracked in Linear as **Done** or **In Progress**, so HU-09 is the first documented feature slice for this bounded context.

**Domain layer**

- `Mission`, `MissionNode`, `MissionActivation`, `Difficulty`, and `MaximumTime` are already defined as normative domain concepts by the local PRD, DDD solution model, entity spec, and service context
- `MissionActivation` is a source-readiness concept inside `MissionDesign`, not a runtime session lifecycle state

**Application layer**

- The canonical mission-side application services begin with `CreateMission`, `UpdateMission`, `DeactivateMission`, `GetMissionCatalog`, and `GetMissionDetail`

**Infrastructure / API**

- No predecessor HU context has established a stable mission authoring contract yet

**Frontend**

- No predecessor mission-authoring frontend slice is documented yet

**Coverage:** no predecessor HU context establishes a baseline percentage for `mission-design-service`; verify the real aggregate line coverage during phase X.4.

### What HU-09 adds on top (per PRD DES-62)

| Concern | New work |
|---|---|
| `Mission` authoring baseline | Create a mission with basic authored data (`title/name`, `description/briefing`, `difficulty`, `maximumTime`) |
| Mission inspection | Mission catalog and mission detail queries so administrators can consult existing missions |
| Mission maintenance | Update existing mission details without recreating the mission |
| Mission deactivation | Soft-deactivate a mission while preserving history |
| Source readiness boundary | A deactivated mission must not be usable as the source of a new mission session |
| Backend contract | Mission create/list/detail/update/deactivate API surface under `/api/missions` |
| Frontend flow | Admin mission-management UI for create, browse, edit, and deactivate actions |

### Branch state and prerequisite

`feature/hu-09-mission-management` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch base dependency to inherit first.

**Branch naming note:** older repo workflow docs still mention `feature/mission-design-service`, but this prompt follows the newer feature-scoped branch pattern from `workflow_for_prompts.md` and the current HU templates.

**Before starting implementation:** inspect the current mission-design service source and confirm whether scaffolded mission types/endpoints already exist. Extend and reconcile them with DES-62 instead of recreating parallel copies.

### Linear state (as of 2026-05-31)

- DES-14 (HU-09): **Backlog**, labels: `Feature`, `ready-for-agent`, `svc:mission-design-service`
- No same-service predecessor HU is currently **Done** or **In Progress**
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`

> Linear live state may have changed. Use the Linear MCP to verify DES-14 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` instead.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the README, mission-design scaffold, or Linear state may have changed since 2026-05-31.

```
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md — current service status
- @backend/services/mission-design-service/CONTEXT.md — bounded-context language and boundary rules
- @backend/docs/prd/DES-62-mission-design-service-baseline.md — the authoritative PRD for HU-09 to HU-14

Then inspect the existing mission-design source only enough to confirm whether a partial
mission scaffold already exists:
- @backend/services/mission-design-service/src/Domain/Entities/Mission.cs
- @backend/services/mission-design-service/src/Application/Missions/
- @backend/services/mission-design-service/src/Api/Endpoints/MissionsEndpoints.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-14 (HU-09 — Gestión de misiones) — status and labels

Output:
- the normative domain concepts HU-09 depends on (Mission, MissionActivation, Difficulty, MaximumTime)
- what HU-09 adds on top per the PRD: create, consult, update, deactivate, and source-readiness denial for inactive missions
- whether the current source already contains a mission scaffold that should be extended rather than recreated
- current Linear status and labels for DES-14

Do not start planning or implementing yet.
```

---

## 2. Label DES-14 as ready-for-agent

> DES-14 already carries `ready-for-agent` as of 2026-05-31. Use this step to confirm the label remains present before execution.

```
Use the Linear MCP to confirm DES-14 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-14 ticket state and labels.
```

---

## 3. Confirm slice readiness

```
Use the Linear MCP to confirm DES-14 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-09` and `DES-14` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above.

---

## 4. Start the slice

```
Prepare the mission-management slice on branch feature/hu-09-mission-management.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service and frontend.

The pre-resolved orient at the top of this document lists what HU-09 adds.
Do not re-read the PRD for scoping.

Before implementation, confirm whether the current branch source already contains
Mission scaffold code. If it does, extend and reconcile it; do not create parallel types.

Move DES-14 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-09 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current Mission aggregate and related mission
value objects/events in the service source. Extend the existing baseline rather than
recreating Mission concepts under new names.

Scope:
- formalize the Mission authoring baseline aggregate behavior for HU-09:
  create mission, update mission details, deactivate mission
- ensure Mission captures the basic authored fields required by HU-09:
  name/title, description/briefing, difficulty, maximum time, activation/readiness state
- add or complete the HU-09 domain events:
  MissionCreated, MissionDetailsUpdated, MissionDeactivated
- add any mission deactivation invariant exception needed by the canonical docs
- ensure a deactivated mission cannot be considered source-ready
- keep mission readiness distinct from runtime session lifecycle

Important ambiguity to resolve explicitly:
- bd_umbral_entity_spec.md says a Mission must have at least one MissionNode,
  but HU-09 only covers basic mission authoring before HU-10A/HU-10B add structure.
  Implement HU-09 so a newly created mission remains a non-ready draft baseline
  rather than inventing structure during this slice.

Gate:
- Domain build passes
- New domain invariants are expressed as unit tests on Mission
- No mission-structure behavior for HU-10A/HU-10B is prematurely implemented here

Do not touch other backend layers or frontend.
```

Commit:

```
feat(mission-design): phase X.1 — domain layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-09 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the existing Application/Missions scaffold and
extend the current command/query names if they already match the PRD.

Scope:
- repository interfaces for the HU-09 mission baseline:
  IMissionRepository and IMissionReadModelRepository if missing
- commands + handlers + validators:
  CreateMission, UpdateMission, DeactivateMission
- queries + handlers:
  GetMissionCatalog, GetMissionDetail
- DTOs/projections for mission summary and mission detail
- authorization boundary:
  mission mutations are Administrator-only
- validation:
  required mission authored fields, difficulty value rules, maximum time rules,
  and not-found handling for update/detail/deactivate paths

Gate:
- clean build passes
- handler and validator unit tests cover valid path plus rejection/error branches
- application layer does not leak infrastructure concerns

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```
feat(mission-design): phase X.2 — application layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-09 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current persistence scaffold:
- ApplicationDbContext
- MissionConfiguration
- existing Init migration and snapshot

Scope:
- persist the HU-09 Mission baseline cleanly in EF Core
- implement Mission repository/read-model repository support if the application
  layer introduced those abstractions
- add or update the migration only for HU-09's mission baseline requirements
- integration coverage for:
  create mission
  update mission details
  deactivate mission
  list/detail queries reflecting current activation/readiness state
- keep the database surface limited to the HU-09 mission baseline;
  do not add MissionNode persistence before HU-10A/HU-10B

Database isolation:
- each integration test must clear shared mission data before its scenario

Gate:
- dotnet build passes on the solution
- migration is coherent with the current snapshot
- repository/integration tests pass for create, update, deactivate, and read paths

Do not touch Api or frontend.
```

Commit:

```
feat(mission-design): phase X.3 — infrastructure layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-09 in mission-design-service.
Use the service PRD (DES-62) and canonical docs.

Before writing anything, inspect the current MissionsEndpoints scaffold and extend it
instead of creating a second endpoint group for the same feature.

Scope:
- expose the HU-09 mission management API surface under /api/missions:
  create mission
  list missions
  get mission detail
  update mission
  deactivate mission
- ensure response DTOs expose the mission authored fields plus activation/readiness state
- proof that a deactivated mission is surfaced as unavailable for new-session source use
- proof that update and deactivate paths handle not-found and validation errors correctly
- keep runtime session creation behavior out of this service; only expose mission-authoring facts

Gate:
- endpoint tests pass for create, list/detail, update, and deactivate paths
- no regression on any existing mission endpoints already present in the scaffold
- service line coverage reaches 93%

Do not touch frontend.
```

Commit:

```
feat(mission-design): phase X.4 — api layer (HU-09)

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

Then run: `/debrief`

---

## 8.5. Docker rebuild and smoke

```
From the monorepo root, rebuild and restart the mission-design-service stack:

docker compose build mission-design-service
docker compose up -d mission-design-service

Then run a minimal smoke against the mission endpoints:
- create a mission
- fetch the mission catalog
- fetch the mission detail
- deactivate the mission
- confirm the deactivated mission is reflected correctly in the API response

Output the exact commands used and the relevant HTTP status codes.
```

---

## 9. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-09 against the verified backend mission API.

Scope:
- administrator mission catalog view
- mission create flow
- mission detail/edit flow
- mission deactivate action with confirmation
- UI state that reflects whether a mission is active or inactive

Gate:
- frontend uses the backend contract verified in phase X.4
- no invented runtime session behavior
- mission-management flows cover create, browse, edit, and deactivate acceptance paths

Commit message:

feat(frontend): mission management — HU-09

Ref: HU-09
Ref: DES-14
Ref: DES-62
```

---

## 10. Close-out

```
Verify HU-09 acceptance criteria explicitly against the implemented slice:
- administrator can create a mission with its basic data
- administrator can consult and edit existing missions
- administrator can deactivate a mission without deleting its usage history
- a deactivated mission cannot be used to create new mission sessions

Then prepare the draft PR:

gh pr create --draft --title "feat: HU-09 mission management" --body-file <PR_BODY_FILE>

Output:
- acceptance-criteria checklist with evidence
- branch name
- PR title
- any unresolved ambiguity that should be reviewed before merge
```

---

## Rationale

- HU-09 is the first documented `mission-design-service` slice, so the prompt sequence cannot assume a same-service predecessor context file the way HU-03/HU-05 did.
- The repo currently mixes two branching stories: older workflow docs still point to `feature/mission-design-service`, while newer prompt workflow docs require feature-scoped branches. This sequence follows the newer feature-scoped pattern and names the branch `feature/hu-09-mission-management`.
- The strongest ambiguity in the canonical docs is the `Mission`-must-have-nodes rule versus HU-09's basic authoring scope. The prompt resolves that tension by treating HU-09 missions as authored drafts that remain non-ready until HU-10A/HU-10B introduce and validate structure, instead of inventing premature node behavior.
- The local mission-design source already contains scaffolded mission code and `/api/missions` endpoints. The driver should reconcile and extend that baseline rather than build a parallel implementation that duplicates names or routes.
