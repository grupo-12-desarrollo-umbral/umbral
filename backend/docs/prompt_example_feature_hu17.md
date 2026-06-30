# Prompt Example - HU-17 Creacion de sesion desde una unica fuente (Feature Slice)

Concrete prompt sequence for driving DES-24 (HU-17) through a full slice on `feature/hu-17-single-source-session-creation`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-24 slice:** this is **not** a fresh rebuild. HU-15 (DES-22, Done) already made `Mission` the only `SessionSource`, added the immutable `MissionRuntimeSnapshot`, the single `CreateSessionFacade`, `SessionCreationPolicy`, and the mission-only `POST /api/sessions` contract. HU-17 **locks the single-source invariant with explicit tests across all four layers and finishes the residual two-source teardown HU-15 left behind** (dead `SessionMode` enum + orphaned exceptions). It adds no new aggregate, no new endpoint, and no new migration. Treat every phase as verification + targeted deletion, not re-implementation.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- a `LiveSession` is created from exactly **one** `Mission`; `Mission` is the only `SessionSource`
- a `TriviaQuiz` cannot create a `LiveSession` directly (no quiz-as-source, no alternate creation route)
- trivia questions reach runtime only via a trivia `Substage` whose published quiz was copied into the `MissionRuntimeSnapshot` at creation (HU-15)
- no external source can be mixed into the mission snapshot (the snapshot is built solely from the one source mission)
- no session-level `SessionMode` exists in the service after this slice
- HU-15's mission-only creation path (`SessionSource`, `MissionRuntimeSnapshot`, `CreateSessionFacade`, `SessionCreationPolicy`, `POST /api/sessions`) is **verified and locked, not rebuilt**

---

## Required design patterns

- **None mandated.** `required_patterns_matrix.md:45,100` lists HU-17 as `—`: the single-source invariant is "enforced inside the `HU-15`/`HU-16` Facade + `SessionCreationPolicy`, not its own pattern." Do **not** invent a pattern. The invariant rides on HU-15's already-built `CreateSessionFacade` (`Facade`) + `SessionCreationPolicy`; HU-17 adds no new pattern gate.

> Applies-where note (no new gate): `POST /api/sessions` is a protected mutation, but HU-17 is not matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). It inherits the standard `Administrator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-06-30)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-24 (HU-17) is **Todo**, a `needs-rebuild` row in the realignment map (phase #3 "Session creation": "Single-source rule = mission-wrapper (drop 'trivia session from quiz')"). Its blocker **DES-22 (HU-15) is Done (2026-06-21)** and shipped the entire mission-only creation path: `SessionSource` reshaped to Mission-only (`SessionSourceType` now holds only `Mission = 1`), the immutable `MissionRuntimeSnapshot` + snapshot VOs, mission-based `LiveSession.Create(...)` (sets `Scheduled`, `ValidateSource` rejects non-mission sources at `LiveSession.cs:620-626`), the single `CreateSessionFacade`, `SessionCreationPolicy.EnsureMissionEligible`, `IMissionRuntimeSource` + HTTP adapter, the `AddMissionRuntimeSnapshot` migration (dropped `source_trivia_quiz_id` + trivia-snapshot tables), and the mission-only `POST /api/sessions` contract.

HU-17 is **session-operations-only** (single `svc:` label) — there is **no** cross-service mission-design prerequisite; HU-15 already landed `GET /api/missions/{id}/runtime-plan`.

Superseded and excluded from the predecessor set: HU-16 (DES-23->DES-75), HU-21A (DES-28->DES-76), HU-22 (DES-30->DES-77), HU-33A (DES-44->DES-78) — all Canceled. HU-18/19/20 build on creation, not the reverse. No same-service In Progress predecessors, so the branch base is `develop`.

### What HU-17 adds on top (per DES-24, the realignment map, and PRD DES-70)

| Concern | New work |
|---|---|
| Verification posture | Lock HU-15's single-source result as an explicitly tested invariant; finish the residual teardown. Do not rebuild creation. |
| Single-source invariant (domain) | Unit tests: `SessionSource.Create` -> `Mission` only; `SessionSourceType` exposes only `Mission`; `LiveSession.Create`/`ValidateSource` reject a non-mission source. |
| Residual two-source teardown | Delete dead debris (zero non-migration refs): `SessionMode` enum, `SessionSourceDoesNotMatchModeException`, the `TriviaSessionSnapshot*` exception pair (entity already removed by HU-15). |
| No source mixing (application) | Test that `CreateSessionFacade` is the single creation entry point and builds the snapshot solely from the one requested mission; command carries no non-mission source field. |
| Persistence cleanliness | Assert (via test) no `source_trivia_quiz_id` / quiz-snapshot tables / `SessionMode` column persist. No new migration. |
| API contract | Test `POST /api/sessions` is mission-only and there is no alternate creation route. Reshape nothing. |
| Frontend | Verification/cleanup: UI offers a `Mission` as the only source; remove residual quiz-as-source / `SessionMode` copy or types. No contract change. |

### Branch state and prerequisite

`feature/hu-17-single-source-session-creation` should be branched from `develop`. All dependencies are Done; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) the dead two-source debris is still orphaned and that the `TriviaQuestionSnapshot*` exceptions are **live** (reused by `MissionRuntimeSnapshot` trivia content) — delete only the dead pair.

### Linear state (as of 2026-06-30)

- DES-24 (HU-17): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-22 (HU-15): **Done** (the foundation this slice locks)
- Same-service Canceled (superseded): DES-23, DES-28, DES-30, DES-44

> Linear live state may have changed. Use the Linear MCP to verify DES-24 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-06-30.

```text
Read the following and summarise what is already implemented vs. what HU-17 must lock:
- @backend/docs/hu17-context.md - the pre-resolved HU-17 context (primary)
- @backend/docs/hu15-context.md - what HU-15 already shipped (the foundation HU-17 verifies)
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment overlay (single-source canon delta)

Then grep the existing session-operations source to confirm:
- SessionSourceType holds only Mission; SessionSource.Create yields Mission; LiveSession.ValidateSource rejects non-mission
- the dead two-source debris (SessionMode, SessionSourceDoesNotMatchModeException, TriviaSessionSnapshot* exceptions) has zero non-migration references
- the live TriviaQuestionSnapshot* exceptions are reused by MissionRuntimeSnapshot (do NOT delete those)
- the current ApplicationDbContextModelSnapshot.cs has no source_trivia_quiz_id / SessionMode / quiz-snapshot tables

Then use the Linear MCP to fetch the current live state and labels of DES-24.

Output: what is already correct (and must NOT be rebuilt), the exact dead-debris files to delete, and the per-layer invariants to lock.
Do not start planning or implementing yet.
```

---

## 2. Label DES-24 as ready-for-agent

```text
Use the Linear MCP to confirm DES-24 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-24 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-24 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- a LiveSession is created from exactly one Mission; Mission is the only SessionSource
- a TriviaQuiz cannot create a LiveSession directly (no quiz-as-source, no alternate route)
- trivia reaches runtime only via a trivia Substage copied into the MissionRuntimeSnapshot (HU-15)
- no external source can be mixed into the mission snapshot
- no session-level SessionMode exists after this slice
- HU-15's creation path is verified and locked, NOT rebuilt

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining steps, `HU-17` and `DES-24` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by the realignment document).

---

## 4. Start the slice

```text
Prepare the single-source session-creation lock slice on branch feature/hu-17-single-source-session-creation.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service and (verification only) frontend.

The pre-resolved orient at the top of this document lists what HU-15 already shipped and what
HU-17 locks. Do not re-read the PRD for scoping unless you need a precise implementation detail.

This is a verification + teardown slice: lock the single-source invariant with tests and delete
the dead two-source debris. Do NOT re-implement the mission-only SessionSource, the
MissionRuntimeSnapshot, the CreateSessionFacade, SessionCreationPolicy, or the
POST /api/sessions contract - they are Done.

Move DES-24 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-17 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu17-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit test locks the single-source invariant (SessionSource.Create -> Mission only; LiveSession.Create rejects a non-mission source; SessionSourceType exposes only Mission)
- dead two-source debris deleted: SessionMode enum, SessionSourceDoesNotMatchModeException, TriviaSessionSnapshotMustContainQuestionsException, TriviaSessionSnapshotRequiredException
- no SessionMode type remains in the service; the live TriviaQuestionSnapshot* exceptions are untouched

Do not touch other backend layers or frontend. Do not re-implement any HU-15 type.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-17)

Ref: HU-17
Ref: DES-24
Ref: DES-70
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-17 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu17-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; facade/handler test proves the snapshot is built only from the requested mission (sourceMissionId derives from command.MissionId, no external source mixed) and the command carries no non-mission source field
- creation has a single entry point (the mission CreateSessionFacade); no CreateTriviaSession* / IPublishedTriviaQuizSource path remains (verify absent)

Do not touch Infrastructure, Api, or frontend. Do not rewrite the CreateSessionFacade.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-17)

Ref: HU-17
Ref: DES-24
Ref: DES-70
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-17 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu17-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it, as the block instructs).

Gate:
- Infrastructure build passes
- NO new migration - assert (ef migrations add dry-check or model-snapshot grep) the model already excludes source_trivia_quiz_id / SessionMode / quiz-snapshot tables
- repository integration test round-trips a session with a mission-only SessionSource; no foreign-source column or table persists

Do not touch Api or frontend. Do not add an empty migration.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-17)

Ref: HU-17
Ref: DES-24
Ref: DES-70
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-17 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu17-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration test: active+ready mission -> 201 with a single mission source in the contract; no quiz/second-source field; no alternate creation route; plus the ineligible-mission rejection (reuse HU-15's 409/422 path)
- service coverage reaches the repo gate target (ADR-0005)

Do not touch frontend. Do not reshape the POST /api/sessions contract.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-17)

Ref: HU-17
Ref: DES-24
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run curl smoke checks through the gateway for:
- POST /api/sessions with an active, runtime-ready mission -> expect 201 Created (single mission source)
- POST /api/sessions with an inactive / not-ready mission -> expect 409/422
- confirm there is no quiz-as-source creation route and the request body carries no quiz/second-source field

Output:
- container status
- smoke command results
- confirmation the single-source contract is unchanged from HU-15 (no contract drift for the frontend)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-17 single-source session creation.

The backend contract is UNCHANGED from HU-15: POST /api/sessions takes a mission-only
CreateSessionRequest (MissionId, Title, MaximumTimeMinutes, ScheduledAt), Administrator-only,
201 Created, 409/422 when the mission is not eligible. This is a small verification/cleanup
surface — choose the hu-03 exemplar.

Scope:
- confirm the session-creation form already selects a Mission as the only source; if not, align it to the mission-only CreateSessionRequest
- remove or rewrite any residual UI copy/types that mention creating a session from a trivia quiz, SessionMode, or a TriviaQuiz source
- ensure no UI affordance offers a quiz-as-source / second-source session-creation path
- keep surfacing backend eligibility/readiness rejection (mission inactive / not ready -> 409/422)

Gate:
- frontend typecheck/build passes
- session-creation flow exercises create-from-mission against the (unchanged) API contract, incl. the not-eligible rejection path
- Gate: UI selects a Mission as the only source; no UI type/copy treats TriviaQuiz as a SessionSource
- Gate: no UI type/copy introduces or retains SessionMode

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): single-source session creation - HU-17

Ref: HU-17
Ref: DES-24
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of
> truth and supersedes the Step 9 seed scope — including Step 9's single seed commit:
> commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-17-frontend-single-source-session-creation.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the
phase lands), then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not invent the
blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-24 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- a LiveSession is created from exactly one Mission; Mission is the only SessionSource
- a TriviaQuiz cannot create a LiveSession directly (no quiz-as-source, no alternate route)
- trivia reaches runtime only via a trivia Substage copied into the MissionRuntimeSnapshot (HU-15)
- no external source can be mixed into the mission snapshot
- no session-level SessionMode exists after this slice
- HU-15's creation path is verified and locked, not rebuilt

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- the dead two-source debris removed
- the per-layer invariant tests added
- tests and gates run (incl. ADR-0005 coverage)
- confirmation that DES-75/76/77/78 can now proceed (single-source invariant locked)

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-17-single-source-session-creation \
  --title "feat(session-operations): lock single-source session creation invariant (HU-17)" \
  --body "Locks DES-24/HU-17: a LiveSession is created from exactly one Mission, a TriviaQuiz can never be a SessionSource, and no external source can be mixed into the immutable MissionRuntimeSnapshot. Adds per-layer invariant tests over HU-15's mission-only creation path and removes the residual two-source debris (SessionMode enum, SessionSourceDoesNotMatchModeException, the orphaned TriviaSessionSnapshot* exceptions). No new aggregate, endpoint, or migration — HU-15's creation path is verified and hardened, not rebuilt."
```

---

## Rationale

HU-17's acceptance criteria (create from exactly one mission; no quiz-as-source; trivia only via a copied substage quiz; no mixing of external sources) are the **single-source invariant**. After the 2026-06-16 mission-runtime rewrite, HU-15 (DES-22) rebuilt session creation around `Mission` as the only `SessionSource` and tore out the quiz-as-source model — so most of HU-17's acceptance is already an emergent property of HU-15's reshape. HU-17 exists to make that property **explicit, tested, and structurally permanent**: it converts an emergent guarantee into locked invariants and deletes the residual two-source debris (`SessionMode`, mode<->source matching exception, the orphaned `TriviaSessionSnapshot*` pair) that would otherwise let a future ticket resurrect a session-level mode concept.

There is **no mandated pattern**: `required_patterns_matrix.md:100` states the single-source invariant is enforced inside HU-15's `Facade` + `SessionCreationPolicy`, not its own pattern. Forcing a pattern here would be the inverse of the HU-01/02/03 defect — inventing scope the matrix explicitly disclaims. This slice is deliberately verification-dominant; it is still a gated deliverable because the realignment map blocks DES-75 (trivia substage), DES-76 (state machine), DES-77 (timer), and DES-78 (trivia orchestration) on DES-24 — the invariant must be locked before any of them can safely rebuild on the session model.
