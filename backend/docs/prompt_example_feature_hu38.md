# Prompt Example - HU-38 Aplicación de penalizaciones justificadas (Feature Slice)

Concrete prompt sequence for driving DES-53 / HU-38 through a full feature slice on `feature/hu-38-justified-penalties` in `scoring-monitoring-service`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-38:** this is the **first** implementation code in a greenfield `scoring-monitoring-service` (only `.gitkeep` scaffolding today). HU-38 adds the operator-facing **justified-penalty** layer: an `ApplyPenalty` command that writes an append-only `ScoreEntry` deduction plus its `Penalty` child, gated to the session's assigned operator, with eligibility/impact expressed as `Strategy` policies. It is **not** the full scoring ledger — `ScoreEntry` is HU-37's foundation (DES-51), which lands first; HU-38 builds on it. See the prerequisite note before starting.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- a penalty requires a mandatory `PenaltyReason` and records `appliedByUserId` + `appliedAt`
- a penalty impacts score **only** through an append-only `ScoreEntry` deduction — no separate mutable total
- eligibility (`PenaltyPolicy`) and impact (`ScorePolicy`) are `Strategy` policies, not handler branching
- the operation is restricted to the session's **assigned operator** via a `Proxy` guard (Administrator unrestricted) — no ad-hoc role `if`
- on success, `PenaltyApplied` is published to RabbitMQ, and the apply flow does **not** depend on RabbitMQ
- HU-38 builds on HU-37's landed `ScoreEntry` ledger (it is not recreated here)

---

## Required design patterns

- `Strategy`
  - Why: justified penalties are a score-policy outcome — `PenaltyPolicy` decides eligibility/justification, `ScorePolicy` decides the deduction impact; scoring variation must not become handler-level branching.
  - Phase owner: X.1 Domain (`IPenaltyPolicy`/`IScorePolicy` + `sealed` concrete strategies in `Domain/Services/`), selected at runtime in X.2.
  - Gate obligation: policies realized as interface + `sealed` impl in `Domain/Services/` (single-impl, no selector until a second variant lands); eligibility/impact logic lives in the strategies, never in `if`/`switch` inside the handler.
- `Proxy`
  - Why: only the operator assigned to a session may penalize its teams; access is guarded at the application boundary.
  - Phase owner: X.2 Application (resolver/decorator) + X.4 Api (endpoint policy).
  - Gate obligation: a `ScoringSessionAuthorizationProxy` over an access-resolver interface (Administrator unrestricted; Operator only when `AssignedOperatorUserId == actor.UserId`, else `ForbiddenAccessException`) — no ad-hoc role/owner `if` in handler, controller, or DI; endpoint carries `[Authorize(Policy=...)]`.

> Resolution note: both patterns are **mandated** by `backend/docs/required_patterns_matrix.md` for HU-38 (Strategy, Proxy), per `backend/docs/adr/0004-required-domain-patterns.md` (Strategy for scoring policies; Proxy for role/policy access guards) and placed by `backend/docs/adr/0012-design-pattern-placement-convention.md` (Strategy → `Domain/Services/`; Proxy → the Application slice / `<Area>/Common/Authorization/`).

---

## Pre-resolved orient (as of 2026-07-12)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

`scoring-monitoring-service` is **greenfield**: scaffolded tree, only `.gitkeep` files, zero `.cs`, no `ScoreEntry`/`Penalty`/policy types. The Application layer is pre-seeded with `Scores/`, `Metrics/`, `Alerts/`, `Common/` folders. No same-service predecessor is Done or In Progress, so the branch base is `develop`. Mirror targets come from sibling services (session-operations, mission-design, identity-access), not from this service.

- **Domain:** `ScoringMonitoring` owns `ScoreEntry` and `Penalty`; `Ranking`/`AuditHistory`/monitoring are derived models (out of scope for HU-38).
- **Application/Infrastructure/API:** none for scoring yet. Sibling anchors: `SessionAdministrationAuthorizationProxy` + `ISessionAdministrationAccessResolver` (Proxy), `IQuestionActivationStrategy`/`SequentialQuestionActivationStrategy` (Strategy convention), `mission-design` `CreateMission` slice (vertical-slice layout).
- **Cross-service (session-operations, unmerged):** HU-19 (operator↔session assignment) and HU-21A (session state / `LiveSession.AssignedOperatorUserId`) supply the operator-identity facts the Proxy consumes.

**Coverage:** greenfield — measured fresh at X.4 against the ADR-0005 gate.

### What HU-38 adds on top (per DES-53 and DES-85)

| Concern | New work |
|---|---|
| Justified penalty | Operator applies a `Penalty` to a team in a supervised session, with mandatory `PenaltyReason`, recorded actor + `appliedAt`. |
| Ledger deduction | Penalty impacts score only via an append-only `ScoreEntry` deduction — no hidden mutable total. |
| Eligibility Strategy | `PenaltyPolicy` validates eligibility/justification as an interchangeable strategy. |
| Impact Strategy | `ScorePolicy` computes the deduction magnitude as an interchangeable strategy. |
| Authorization Proxy | Application-boundary guard restricting the op to the assigned operator (Administrator unrestricted). |
| Event | On commit, publish `PenaltyApplied` (+ `ScoreEntryRecorded`) to RabbitMQ for secondary recalc/audit; apply flow independent of RabbitMQ. |
| Backend contract | Operator-guarded apply-penalty endpoint returning the applied result. |
| Frontend flow | Operator UI to apply a justified penalty and see the score reflect it. |

### Branch state and prerequisite

`feature/hu-38-justified-penalties` should be branched from `develop`. No same-service predecessor is In Progress, so there is no feature-branch dependency to inherit.

**Prerequisite — HU-37 `ScoreEntry` ledger (DES-51, `Todo`).** Canon models `Penalty` as a child entity of `ScoreEntry`, and the PRD names HU-37 the foundation, so **HU-38 lands after HU-37**: build HU-38 as the penalty layer on HU-37's existing ledger — consume `ScoreEntry`/`ScoreValue`, add the `Penalty` child + policies + recording behavior (per-phase blocks tag build-on types _(from HU-37)_). Do not start X.1 until DES-51/HU-37 has merged.

### Linear state (as of 2026-07-12)

- DES-53 (HU-38): **Backlog**, labels: `svc:scoring-monitoring-service`, `Feature`, `ready-for-agent`
- DES-85 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:scoring-monitoring-service`
- Same-service Done issues: none. Same-service In Progress: none. DES-51 (HU-37): `Todo`.
- Cross-service blockers: DES-26 (HU-19), DES-76 (HU-21A) — session-operations, unmerged.

> Linear live state may have changed. Use the Linear MCP to verify DES-53 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`.

---

## 1. Orient - read service state and PRD

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-12.

```text
Read the following files and summarise what has been decided and scaffolded so far:
- @backend/services/scoring-monitoring-service/README.md - current service status
- @backend/services/scoring-monitoring-service/CONTEXT.md - bounded-context language and pattern expectations
- @backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md - PRD for HU-37 to HU-40
- @backend/docs/hu38-context.md - the pre-resolved HU-38 context

Then inspect the scaffolded scoring source enough to confirm it is greenfield:
- @backend/services/scoring-monitoring-service/src/

Then use the Linear MCP to fetch only the current live state of:
- DES-53 (HU-38) - status and labels
- DES-51 (HU-37) - status (the ScoreEntry ledger prerequisite)

Output:
- confirmation the service is greenfield (no ScoreEntry/Penalty code)
- the canonical HU-38 scope: Penalty child of ScoreEntry, mandatory PenaltyReason, ledger deduction, PenaltyPolicy/ScorePolicy Strategy, assigned-operator Proxy, PenaltyApplied event
- confirmation DES-51/HU-37's ScoreEntry ledger has landed (HU-38 builds on it)
- current Linear status and labels for DES-53

Do not start planning or implementing yet.
```

---

## 2. Label DES-53 as ready-for-agent

> DES-53 already carries `ready-for-agent` as of 2026-07-12. Use this step to confirm the label remains present before execution.

```text
Use the Linear MCP to confirm DES-53 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-53 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-53 carries both svc:scoring-monitoring-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md.
Do not re-fetch PRD scope from Linear; read the local file if you need implementation decisions.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- a penalty requires a mandatory PenaltyReason and records appliedByUserId + appliedAt
- a penalty impacts score only through an append-only ScoreEntry deduction
- PenaltyPolicy and ScorePolicy are Strategy policies, not handler branching
- the operation is restricted to the session's assigned operator via a Proxy guard
- on success, PenaltyApplied is published to RabbitMQ and the apply flow does not depend on it
- HU-38 builds on HU-37's landed ScoreEntry ledger (not recreated here)

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-38` and `DES-53` are the resolved values for this slice. `DES-85` is the shared PRD reference for `scoring-monitoring-service`; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the justified-penalty slice on branch feature/hu-38-justified-penalties.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend scoring-monitoring-service and frontend.

The pre-resolved orient at the top of this document lists that the service is greenfield
and what HU-38 adds. Do not re-read the PRD for scoping unless you need a precise
implementation detail.

Before implementation, confirm DES-51/HU-37 has landed the ScoreEntry ledger — HU-38
builds the penalty layer on top of it and must not rebuild the ledger.

Move DES-53 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-38 in scoring-monitoring-service, per the
**X.1 derivation block in @backend/docs/hu38-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit test per new/changed domain type (ScoreEntry penalty-recording behavior raising PenaltyApplied/ScoreEntryRecorded as an append-only deduction, Penalty reason/appliedAt invariants, PenaltyReason, DefaultPenaltyPolicy, DefaultScorePolicy, each exception; HU-37 already covers base ScoreEntry/ScoreValue)
- Strategy realized as IPenaltyPolicy/IScorePolicy interface + sealed concrete impl in Domain/Services/ — no eligibility/impact branching elsewhere
- a penalty impacts score only through a ScoreEntry deduction; no separate mutable total
- Penalty requires a non-blank PenaltyReason and records appliedByUserId + appliedAt

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.1 - domain layer (HU-38)

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-38 in scoring-monitoring-service, per the
**X.2 derivation block in @backend/docs/hu38-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; handler tests cover valid apply, non-owning operator (ForbiddenAccessException), ineligible penalty rejection, and missing/blank reason
- validator tests cover reason required and ids present
- access enforced through the ScoringSessionAuthorizationProxy resolver — no ad-hoc role/owner if in the handler
- PenaltyPolicy/ScorePolicy Strategy selected at runtime (injected), not branched
- PenaltyApplied raised for post-commit publish; no infrastructure leak

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.2 - application layer (HU-38)

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-38 in scoring-monitoring-service, per the
**X.3 derivation block in @backend/docs/hu38-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it).

Gate:
- build passes
- EF migration/snapshot represents ScoreEntry + its Penalty child accurately
- repository integration test round-trips an append-only ScoreEntry + Penalty
- PenaltyApplied published to RabbitMQ after commit; a broker outage does not fail or roll back the apply path
- no mutable score total persisted

Do not touch Api or frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.3 - infrastructure layer (HU-38)

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-38 in scoring-monitoring-service, per the
**X.4 derivation block in @backend/docs/hu38-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint returns success for the assigned operator applying a justified penalty
- 403 RFC 7807 for a non-owning operator; 400 for a missing/blank reason
- access enforced through the Proxy (endpoint [Authorize(Policy=...)] + access resolver) — no ad-hoc role if
- the penalty is reflected as a ScoreEntry deduction
- service reaches the repo coverage gate (ADR-0005)

Do not touch frontend.
```

Commit:

```text
feat(scoring-monitoring): phase X.4 - api layer (HU-38)

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build scoring-monitoring-service api-gateway
docker compose up -d scoring-monitoring-service api-gateway

Run curl smoke checks through the gateway for:
- apply a justified penalty as the assigned operator (expect success, penalty reflected as a ScoreEntry deduction)
- apply as a non-owning operator (expect 403 RFC 7807)
- apply with a blank reason (expect 400)

Output:
- container status
- smoke command results
- the apply-penalty request/response contract the frontend phase must consume
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the frontend plan concreteness rule (below), modelled on the exemplar closest to this slice's shape (@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1–few-endpoint surface; @frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-38 justified penalties.

Scope:
- add the apply-penalty data client/types matching the verified backend contract
- an operator-facing action to apply a justified penalty to a team in a supervised session
- require a non-blank reason before submit; surface eligibility/authorization errors (403/400) from the backend
- reflect the resulting score change (deduction) in the operator view
- do not expose penalty application to non-operators or for non-owned sessions

Gate:
- frontend typecheck/build passes
- the flow exercises applying a justified penalty against the real API contract, including the reason-required and non-owner rejection paths
- no UI treats the penalty as a mutable total outside the ledger

Frontend plan concreteness rule (embed verbatim in the plan):
1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the fully-knowable near-term increments (typically the foundation + first authoring increment). Keep later, large, or blocked increments at contract + gate altitude: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) · data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping · Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Do not modify backend code in this step.
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed.

```text
Use @frontend/AGENTS.md and the Step 9 plan at @frontend/plans/<hu-38 plan file>.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it).
Do not re-generate the plan. Do not modify backend code.
```

Commit (frontend):

```text
feat(frontend): justified penalty application - HU-38

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-53 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- a penalty requires a mandatory PenaltyReason and records appliedByUserId + appliedAt
- a penalty impacts score only through an append-only ScoreEntry deduction
- PenaltyPolicy and ScorePolicy are Strategy policies, not handler branching
- the operation is restricted to the session's assigned operator via a Proxy guard
- on success, PenaltyApplied is published to RabbitMQ and the apply flow does not depend on it

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract (apply-penalty request/response)
- frontend plan/file produced
- tests and gates run
- any newly surfaced ambiguity (the HU-37 sequencing, the HTTP session-assignment access client, and the endpoint path are already decided — flag only new ones)

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-38-justified-penalties \
  --title "feat(scoring-monitoring): apply justified penalties (HU-38)" \
  --body "Implements DES-53/HU-38: operator-applied justified penalties in scoring-monitoring-service. A mandatory-reason Penalty is recorded as an append-only ScoreEntry deduction, gated to the session's assigned operator via a Proxy, with PenaltyPolicy eligibility and ScorePolicy impact as Strategy policies, publishing PenaltyApplied to RabbitMQ without the apply flow depending on it."
```

---

## Rationale

HU-38's controlling patterns differ from a plain CRUD slice because the PRD frames a penalty as a **scoring policy outcome**, not a row insert: eligibility (`PenaltyPolicy`) and score impact (`ScorePolicy`) are `Strategy` abstractions so mode-specific scoring rules never collapse into handler conditionals, and application-boundary authorization is a `Proxy` because only the operator assigned to a session may penalize its teams. The penalty is deliberately modeled as a `Penalty` child of an append-only `ScoreEntry` deduction so the team total stays explainable from the ledger — the PRD explicitly rejects a mutable session total.

Two decisions were resolved rather than left open: (1) **HU-37 lands first** — HU-38 is the penalty layer on HU-37's `ScoreEntry` ledger (consumes `ScoreEntry`/`ScoreValue`, adds the `Penalty` child + policies + recording behavior); X.1 must not start until DES-51 has merged. (2) The **cross-service session-assignment fact** (owned by `session-operations-service`) is reached through an `ISessionAssignmentAccessClient` implemented as a synchronous HTTP access client, mirroring `IAuthenticatedActorProfileAccessClient`; the endpoint is `POST /api/sessions/{liveSessionId}/penalties`.
