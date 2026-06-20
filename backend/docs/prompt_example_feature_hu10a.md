# Prompt Example - HU-10A Estructura jerarquica de misiones (Feature Slice)

Concrete prompt sequence for driving DES-15 (HU-10A) through a full feature slice on `feature/hu-10a-mission-hierarchy-structure`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-10A:** HU-09's rebuild (DES-14, Done 2026-06-19) already implemented the **entire backend** — the full `MissionNode` Composite model (`Stage`/`Substage`/`Clue`), `SubstagePlayMode` enforcement, `Target`-based treasure hunt, optional `Clue` guidance, `MissionActivationPolicy` readiness, all application commands/handlers/validators, the EF migration, the `/api/missions` endpoint set, and comprehensive tests (95.46% coverage). HU-10A's backend phases are **verification only** (the planned exception-mapping refinement rested on a stale premise — already 409 Conflict, no code change), not new implementation. The **main new work is the frontend** — the hierarchy authoring UI that HU-09's frontend plan explicitly deferred. HU-10B (structural validations, DES-16) was archived and folded into HU-10A per the realignment map.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- `MissionNode` Composite: `Stage`, `Substage`, `Clue` — already implemented by HU-09, verify
- containment rules enforced by domain entities, not handlers — already implemented, verify
- each `Substage` has exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia` — already implemented, verify
- treasure-hunt progress is `Target` based, not clue based — already implemented, verify
- `Clue` is optional guidance, max one per target, same substage — already implemented, verify
- every rejection informs parent and child node types — already implemented, verify
- readiness/activation validates the runtime plan — already implemented, verify
- `MissionAlreadyDeactivatedException` already mapped to 409 Conflict by HU-09 (verify; the "map to 400" premise was stale — corrected 2026-06-19)
- frontend hierarchy authoring UI consumes the existing backend contract
- no `SessionMode`
- no `TriviaQuiz` as `SessionSource`

---

## Required design patterns

- `Composite`
  - Why: `Mission` owns the hierarchical authoring structure, and `MissionNode` must model the `Stage`, `Substage`, and `Clue` tree coherently while treasure-hunt `Target`s stay attached to their owning `Substage`. The domain (not handlers) enforces which children each node admits.
  - Phase owner: X.1 Domain, carried through X.2 Application and X.4 API contract shape.
  - Gate obligation: the mission model must be a real Composite over ordered `MissionNode`s (`Stage` -> `Substage` -> optional `Clue`), with containment rules, play-mode enforcement, and traversal/readiness logic centralized in domain entities and policy rather than flattened records or handler conditionals. HU-09 already implemented this — HU-10A verifies it is structurally present and builds the frontend that consumes it.

> Resolution note: `backend/docs/trivia_sprint_required_patterns_matrix.md` omits HU-10A because mission authoring was excluded from that trivia sprint matrix. The mandatory `Composite` obligation comes from `backend/docs/adr/0004-required-domain-patterns.md`, `backend/services/mission-design-service/CONTEXT.md` §Required Patterns, `backend/docs/ddd_solution_model.md` §MissionDesign pattern mapping, and the realignment overlay.

---

## Pre-resolved orient (as of 2026-06-19)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-14 (HU-09) is **Done** (completed 2026-06-19). It was a `needs-rebuild` ticket that rebuilt the entire mission model around `Mission` as a source-content wrapper with a `MissionNode` Composite (`Stage`/`Substage`/`Clue`), `SubstagePlayMode`, `Target`-based treasure hunt, optional `Clue` guidance, and `MissionActivationPolicy` readiness. The rebuild went through all four backend phases and is fully committed and tested (95.46% coverage). The HU-09 frontend plan covers only the 5 base CRUD endpoints and explicitly defers hierarchy authoring to HU-10A.

Later same-service trivia slices are also Done: DES-17 (HU-11), DES-20 (HU-14A), DES-21 (HU-14B), DES-18 (HU-12), and DES-19 (HU-13) — all trivia quiz aggregate, unrelated to HU-10A. DES-23 (HU-16) is superseded by DES-75 and excluded from the predecessor set.

There are no same-service In Progress predecessors, so the branch base is `develop`.

**Domain layer**

- `Mission` aggregate root — source-content wrapper, not runtime session; owns ordered `Stage`s.
- `MissionNode` Composite — `Stage` → `Substage` → optional `Clue`; containment enforced via `CanContain` + `InvalidMissionNodeChildException(parent, child)`.
- `Substage` — exactly one `SubstagePlayMode` (`TreasureHunt`|`Trivia`), immutable per instance; `EnsurePlayMode` guards all mode-specific operations.
- `Target` — QR objective under TreasureHunt substage (not a MissionNode); `AssociateClue` enforces max-one-per-target.
- `Clue` — leaf MissionNode; optional guidance; `AssociateClueWithTarget` enforces same-substage.
- `MissionActivationPolicy` — validates runtime plan: >=1 stage, >=1 substage per stage, TH substage needs >=1 active target + WinnerScore, Trivia substage needs TriviaQuizId.

**Application layer**

- All structure commands: Add/Update/Remove MissionNode, AssignSubstagePlayMode, Add/Update/Remove Target, Associate/UnassociateClue, Set/UpdateTriviaQuizSelection, ActivateMission.
- `MissionStructureEditor`, `MissionCommandHandlerBase`, `MissionDtoMapper`, `TriviaQuizSelectionGuard`.
- All `Administrator`-only via `AuthorizationBehaviour`.

**Infrastructure / API**

- EF owned-type config: `Mission` → `Stage` → `Substage` → `Target` + `Clue`. Migration `AddMissionCompositePersistence`.
- Full `/api/missions` endpoint set: CRUD, activate, readiness, nodes, play-mode, targets, clue-association, trivia-quiz-selection.
- Response shapes include full Composite tree.

**Frontend**

- HU-09 frontend plan covers only 5 base CRUD endpoints (create/list/detail/update/deactivate).
- No frontend wiring for hierarchy authoring (nodes, play-mode, targets, clue-association, trivia-quiz-selection, readiness, activate).

**Coverage:** 95.46% (per HU-09 X.4 commit).

### What HU-10A adds on top (per DES-15, DES-62, and the realignment overlay)

| Concern | New work |
|---|---|
| Backend verification | Verify HU-09's rebuild satisfies all AC items — no new domain types. |
| Exception mapping (verify) | `MissionAlreadyDeactivatedException` already mapped to 409 Conflict by HU-09 (the "map to 400 / currently 500" premise was stale — corrected 2026-06-19; no code change). |
| Frontend hierarchy authoring | Build mission structure editor UI consuming the existing backend contract. |
| Frontend types | Extend `definitions.ts` with hierarchy response types. |
| Frontend API client | Extend `app/lib/missions.ts` with structure endpoint functions. |
| Frontend server actions | Extend `app/actions/missions.ts` with structure mutation actions. |
| Frontend UI components | Stage/substage/clue tree editor, play-mode selection, target authoring, clue association, trivia-quiz selection, readiness display, activation. |

### Branch state and prerequisite

`feature/hu-10a-mission-hierarchy-structure` should be branched from `develop`. No same-service predecessor is currently **In Progress**, so there is no feature-branch dependency to inherit first.

**Before starting implementation:** run the existing backend test suite and confirm all HU-10A AC items are already satisfied by HU-09's rebuild. The backend phases are verification only (no code change — the planned exception-mapping fix rested on a stale premise), not new implementation.

### Linear state (as of 2026-06-19)

- DES-15 (HU-10A): **In Progress**, labels: `canon-realign`, `ready-for-agent`, `svc:mission-design-service`, `Feature`
- DES-14 (HU-09): **Done**, labels: `canon-realign`, `needs-rebuild`, `ready-for-agent`, `svc:mission-design-service`, `Feature`
- DES-62 (PRD): **Backlog**, labels: `ready-for-agent`, `canon-realign`
- Same-service Done issues: DES-14, DES-17, DES-18, DES-19, DES-20, DES-21
- Same-service In Progress issues: none (DES-15 itself is the current ticket)
- Superseded: DES-23 (superseded by DES-75, excluded from predecessor set)

> Linear live state may have changed. Use the Linear MCP to verify DES-15 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-62-mission-design-service-baseline.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-06-19.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/mission-design-service/README.md - current service status
- @backend/services/mission-design-service/CONTEXT.md - bounded-context language and pattern expectations
- @backend/docs/prd/DES-62-mission-design-service-baseline.md - original PRD for HU-09 to HU-14
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment overlay
- @backend/docs/hu09-context.md - the HU-09 rebuild context (predecessor)
- @backend/docs/hu10a-context.md - the pre-resolved HU-10A context

Then inspect the existing mission-design source to confirm HU-09's rebuild is complete:
- @backend/services/mission-design-service/src/Domain/Entities/
- @backend/services/mission-design-service/src/Application/Missions/
- @backend/services/mission-design-service/src/Api/Endpoints/MissionsEndpoints.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-15 (HU-10A - Estructura jerarquica de misiones) - status and labels
- DES-14 (HU-09 - Gestion de misiones) - status and labels

Output:
- confirmation that HU-09's rebuild already implemented the full backend Composite model
- the list of AC items from DES-15 and whether each is already satisfied by existing code
- the remaining work: frontend hierarchy authoring UI (the exception mapping was a stale premise — already 409 Conflict, no code change)
- current Linear status and labels for DES-15

Do not start planning or implementing yet.
```

---

## 2. Label DES-15 as ready-for-agent

> DES-15 already carries `ready-for-agent` as of 2026-06-19. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-15 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-15 ticket state and labels, including canon-realign.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-15 carries both svc:mission-design-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-62-mission-design-service-baseline.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear; read local files if you need implementation decisions.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- MissionNode Composite: Stage, Substage, Clue - already implemented by HU-09, verify
- containment rules enforced by domain entities, not handlers - already implemented, verify
- each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia - already implemented, verify
- treasure-hunt progress is Target based, not clue based - already implemented, verify
- Clue is optional guidance, max one per target, same substage - already implemented, verify
- every rejection informs parent and child node types - already implemented, verify
- readiness/activation validates the runtime plan - already implemented, verify
- MissionAlreadyDeactivatedException already mapped to 409 Conflict by HU-09 (verify; "map to 400" premise was stale, no code change)
- frontend hierarchy authoring UI consumes the existing backend contract
- no SessionMode
- no TriviaQuiz as SessionSource

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-10A` and `DES-15` are the resolved values for this slice. `DES-62` is the shared PRD reference for `mission-design-service`; its content lives in the local file above and is overlaid by the canon realignment document.

---

## 4. Start the slice

```text
Prepare the mission hierarchy structure slice on branch feature/hu-10a-mission-hierarchy-structure.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend mission-design-service (verification + minor fix) and frontend (main new work).

The pre-resolved orient at the top of this document lists what existing code has
already landed (HU-09's full rebuild) and what HU-10A adds. Do not re-read the PRD for
scoping unless you need to resolve a precise implementation detail.

Before implementation, run the existing backend test suite and confirm all HU-10A AC items
are already satisfied by HU-09's rebuild. The backend phases are verification only (no code
change — the planned exception-mapping fix rested on a stale premise), not new implementation. Do not create new domain types that
already exist and pass tests.

Move DES-15 to In Progress (it already is), and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-10A in mission-design-service, per the
**X.1 derivation block in @backend/docs/hu10a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase is VERIFICATION ONLY — HU-09's rebuild already implemented the full
Composite model. Do not create new domain types. Run existing domain tests and
confirm all AC items pass.

Gate:
- Domain build passes; existing unit tests pass (MissionNodeTests, MissionStructureTests, SubstageTests, MissionActivationPolicyTests, MissionActivationPolicyRuntimePlanTests)
- Composite structurally present as Mission -> MissionNode(Stage/Substage/Clue), not flattened records or handler traversal
- each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia
- treasure-hunt progression is Target based; Clue optional, max one per target
- every rejection throws InvalidMissionNodeChildException with parent and child node types in the message
- readiness/activation validates the runtime plan (stage >=1 substage, TH substage >=1 active target + WinnerScore, Trivia substage has TriviaQuizId)
- no SessionMode exists in MissionDesign; TriviaQuiz is not modeled as SessionSource
- no new domain types created

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(mission-design): phase X.1 - domain layer verification (HU-10A)

Ref: HU-10A
Ref: DES-15
Ref: DES-62
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-10A in mission-design-service, per the
**X.2 derivation block in @backend/docs/hu10a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase is VERIFICATION ONLY (corrected 2026-06-19). HU-09's rebuild already
implemented all structure commands/handlers/validators. Run existing application
tests and confirm all AC items pass. The originally-planned "refinement" (map
MissionAlreadyDeactivatedException to 400, said to fall through to 500) was based
on a STALE premise: HU-09 already maps MissionAlreadyDeactivatedException to 409
Conflict (shared arm with MissionAlreadyActiveException), with a passing test.
409 is the correct state-conflict status and stays consistent with its sibling.
No code change — verify the existing mapping + test.

Gate:
- clean build passes; existing handler + validator unit tests cover valid path plus rejection/error branches
- application use cases preserve the Composite — no flattened traversal logic in handlers
- activation/readiness handlers validate the runtime plan through MissionActivationPolicy
- MissionAlreadyDeactivatedException already mapped to 409 in ProblemDetailsExceptionHandler, verified by existing test (no code change)
- no SessionMode in commands/DTOs/handlers/validators; no command treats TriviaQuiz as SessionSource
- no new commands/handlers created

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(mission-design): phase X.2 - application layer verification (HU-10A)

Ref: HU-10A
Ref: DES-15
Ref: DES-62
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-10A in mission-design-service, per the
**X.3 derivation block in @backend/docs/hu10a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it, as the block instructs).

This phase is VERIFICATION ONLY — HU-09's migration AddMissionCompositePersistence
already persists the full Composite hierarchy. Run existing integration tests and
confirm repository round-trip. No new migration needed.

Gate:
- build passes
- existing EF migration/snapshot accurately represents Mission wrapper, MissionNode Composite, Target, optional Clue association, SubstagePlayMode, and TriviaQuizSelection
- existing repository integration tests prove round-trip persistence of the rebuilt runtime plan
- no new migration needed; no SessionMode in schema/read models

Do not touch Api or frontend.
```

Commit:

```text
feat(mission-design): phase X.3 - infrastructure layer verification (HU-10A)

Ref: HU-10A
Ref: DES-15
Ref: DES-62
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-10A in mission-design-service, per the
**X.4 derivation block in @backend/docs/hu10a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

This phase is VERIFICATION ONLY — HU-09's rebuild already exposed all structure
endpoints under /api/missions. Run existing endpoint tests and confirm all AC
items pass. Verify the existing MissionAlreadyDeactivatedException -> 409 Conflict
mapping (from HU-09; no code change) works at the endpoint level.

Gate:
- existing endpoint tests pass for create/update/deactivate/detail/readiness + full structure authoring flow (nodes, play-mode, targets, clue-association, trivia-quiz-selection, activate)
- MissionAlreadyDeactivatedException returns 409 Conflict (verified at endpoint level; mapped by HU-09)
- API detail proves Mission as wrapper, MissionNode Composite, exactly one SubstagePlayMode, Target-based treasure hunt, optional Clue max one per target
- no API request/response contains SessionMode or treats TriviaQuiz as SessionSource
- service coverage reaches the repo gate target (ADR-0005)
- no new endpoints created

Do not touch frontend.
```

Commit:

```text
feat(mission-design): phase X.4 - api layer verification (HU-10A)

Ref: HU-10A
Ref: DES-15
Ref: DES-62
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build mission-design-service api-gateway
docker compose up -d mission-design-service api-gateway

Run curl smoke checks through the gateway for:
- mission catalog
- mission detail for a mission with hierarchy (stages, substages, targets, clues)
- add a stage / substage / clue via the structure endpoints
- readiness/activation validation response
- deactivate an already-deactivated mission (verify 409 Conflict)

Output:
- container status
- smoke command results
- the API contract shape that the frontend phase must consume (MissionResponse with full Composite tree)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in `@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-10A mission hierarchy authoring.

The backend is fully implemented by HU-09's rebuild. The existing /api/missions contract
exposes all structure endpoints. The HU-09 frontend plan
(@frontend/plans/hu-09-frontend-mission-management.md) covers only the 5 base CRUD
endpoints and explicitly defers hierarchy authoring to HU-10A. This slice builds the
hierarchy authoring UI on top of the existing backend.

Scope:
- extend mission types in app/lib/definitions.ts to include hierarchy response types:
  MissionStageDto, MissionSubstageDto (with PlayMode, WinnerScore, TriviaQuizSelection,
  Targets[], Clues[]), MissionTargetDto (with ClueId), MissionClueDto (with Text,
  VisibilityPolicy), TriviaQuizSelectionDto, MissionReadinessDto (with Failures[])
- extend app/lib/missions.ts with API client functions for all structure endpoints:
  add/update/remove nodes, assign play-mode, add/update/remove targets,
  associate/unassociate clue, set/update trivia-quiz-selection, get readiness, activate
- extend app/actions/missions.ts with server actions for all structure mutations
- build mission structure editor UI in the existing MissionsPanel:
  - stage/substage/clue tree view with add/remove/reorder controls
  - play-mode selector (TreasureHunt or Trivia) per substage — warn on mode switch
    (targets/winner-score/trivia-selection are lost)
  - target authoring for TreasureHunt substages (name, qrCode, sequenceOrder, isActive,
    winnerScore)
  - clue authoring (title, text, visibilityPolicy) and clue-to-target association
  - trivia-quiz selection for Trivia substages (select from published quizzes)
  - readiness display showing validation failures from GET /api/missions/{id}/readiness
  - mission activation button (POST /api/missions/{id}/activate) — disabled until ready
  - explicit rejection messages from backend (e.g. "A {child} node cannot be placed
    directly under a {parent} node.")
  - administrator-only throughout (existing AuthorizationBehaviour + frontend role gating)
- mission detail view shows the full Composite tree with stages, substages, targets,
  clues, play modes, and readiness state

Gate:
- frontend typecheck/build passes
- mission hierarchy authoring flow exercises add/remove nodes, play-mode selection,
  target authoring, clue association, trivia-quiz selection, readiness display, and
  activation against the existing API contract
- Gate: UI exposes Mission as wrapper, MissionNode Composite, exactly one SubstagePlayMode,
  Target-based treasure hunt, optional Clue max one per target
- Gate: UI shows explicit rejection messages when backend rejects invalid structures
- Gate: no UI type/copy introduces SessionMode
- Gate: no UI type/copy treats TriviaQuiz as SessionSource

Do not modify backend code in this step.
```

Commit:

```text
feat(frontend): mission hierarchy authoring UI - HU-10A

Ref: HU-10A
Ref: DES-15
Ref: DES-62
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of
> truth and supersedes the Step 9 seed scope — including Step 9's single seed commit:
> commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the
phase lands), then commit with the exact subject from the plan's Commit Sequence for that phase.

Order: P1 → 2.1 → 2.2 → 2.3 → P3.

STOP at 2.3 — it is blocked on the trivia-quiz picker Open Question in the plan. Do not invent
the blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-15 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- MissionNode Composite: Stage, Substage, Clue - verified in backend tests
- containment rules enforced by domain entities, not handlers - verified
- each Substage has exactly one SubstagePlayMode: TreasureHunt or Trivia - verified
- treasure-hunt progress is Target based, not clue based - verified
- Clue is optional guidance, max one per target, same substage - verified
- every rejection informs parent and child node types - verified
- readiness/activation validates the runtime plan - verified
- MissionAlreadyDeactivatedException already mapped to 409 Conflict by HU-09 - verified (the "map to 400" premise was stale; no code change)
- frontend hierarchy authoring UI consumes the existing backend contract - implemented
- no SessionMode
- no TriviaQuiz as SessionSource

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend verification results (all AC items satisfied by HU-09's rebuild)
- exception mapping: verified MissionAlreadyDeactivatedException -> 409 Conflict (stale "fix to 400" premise; no code change)
- frontend plan/file produced
- tests and gates run
- any unresolved ambiguity

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-10a-mission-hierarchy-structure \
  --title "feat(mission-design): HU-10A mission hierarchy structure + frontend authoring" \
  --body "HU-10A (DES-15): verifies HU-09's rebuild satisfies all hierarchy AC items (including MissionAlreadyDeactivatedException already mapped to 409 Conflict — the planned 500->400 fix was a stale premise, no code change), and builds the frontend hierarchy authoring UI (stage/substage/clue tree, play-mode selection, target authoring, clue association, trivia-quiz selection, readiness display, activation) on top of the existing /api/missions contract."
```

---

## Rationale

HU-10A's pattern differs from its predecessor HU-09 in a fundamental way: HU-09 was a `needs-rebuild` ticket that tore out stale mission-session assumptions and rebuilt the full model (Domain + Application + Infrastructure + API + CRUD frontend). HU-10A is a `canon-realign` (comment-only reword) ticket whose AC was tightened against the canon realignment overlay — but the canon-aligned implementation was already built by HU-09's rebuild.

The original PRD (DES-62) split mission authoring into three tickets: HU-09 (CRUD), HU-10A (hierarchy structure), and HU-10B (structural validations). HU-09's `needs-rebuild` cycle absorbed all three into a single rebuild, implementing the full Composite model, containment rules, play-mode enforcement, target-based treasure hunt, optional clue guidance, readiness validation, all commands/handlers/validators, the EF migration, and the full API endpoint set. HU-10B (DES-16) was archived and folded into HU-10A per the realignment map.

This left HU-10A as primarily a **frontend slice** — the hierarchy authoring UI that HU-09's frontend plan explicitly deferred. The backend phases are pure verification (confirm HU-09's rebuild satisfies HU-10A's AC). The originally-planned "minor refinement" (mapping `MissionAlreadyDeactivatedException` to 400 instead of 500) turned out to rest on a stale premise — HU-09 already maps it to 409 Conflict with a test, so no code change was made (corrected 2026-06-19). The mandated `Composite` pattern is structurally present in the existing domain code and does not need to be rebuilt — it needs to be verified and then consumed by the frontend.

The required `Composite` pattern comes from ADR-0004 and the service's `CONTEXT.md`, not from the trivia sprint patterns matrix (which excludes mission HUs). The pattern is already implemented: `MissionNode` is an abstract base with `CanContain`, `Stage` admits only `Substage`, `Substage` admits only `Clue`, `Clue` is a leaf, and `Target` is side-content owned by `Substage` (not a node type). The frontend's job is to surface this structure for administrator authoring and display the domain's explicit rejection messages when invalid structures are attempted.
