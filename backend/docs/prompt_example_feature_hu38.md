# Prompt Example - HU-38 Aplicación de penalizaciones justificadas (Feature Slice)

Concrete prompt sequence for driving HU-38 (DES-53) through a full feature slice on `feature/hu-38-justified-penalties`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-38:** this is **not** greenfield. HU-38 is the operator-facing **justified-penalty** layer on top of the `ScoreEntry` ledger that DES-99 (HU-37 + HU-39) already landed on `develop`. It adds a `Penalty` child of `ScoreEntry`, an eligibility `Strategy` (`IPenaltyPolicy` — reusing the landed `IScorePolicy` for impact), an authorization `Proxy` restricting the action to the session's **assigned** operator, and a `POST …/penalties` endpoint. The penalty impacts score **only** through an append-only `ScoreEntry` deduction — never a mutable total.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- `Penalty` as a child of `ScoreEntry` (one `Penalty` ↔ one `ScoreEntry`), with a mandatory `PenaltyReason`, `appliedAt`, and `appliedByUserId`
- the deduction is an **append-only `ScoreEntry`** of `EntryType.Penalty` (non-negative magnitude; total subtracts it) — no mutable total
- `IPenaltyPolicy` eligibility as a `Strategy`; the landed `IScorePolicy` **reused** for impact (not recreated)
- `Proxy` restricting the action to the session's **assigned** operator (Administrator unrestricted), no ad-hoc role/owner `if`
- assigned-operator fact obtained by **consuming `session-operator-assigned` into a local projection** the Proxy reads — not an HTTP client
- `PenaltyApplied` (+ the ledger's `ScoreEntryRegistered`) published **post-commit**; the apply flow does not depend on RabbitMQ
- no recreation of the landed ledger / enums / `IScorePolicy` / DbContext / Api host

---

## Required design patterns

- `Strategy`
  - Why: a justified penalty is a score-policy outcome — `PenaltyPolicy` decides eligibility/justification, `ScorePolicy` decides the deduction magnitude; scoring variation must not become handler branching.
  - Phase owner: X.1 Domain (`IPenaltyPolicy` + `sealed DefaultPenaltyPolicy`; reuse landed `IScorePolicy`), consumed at runtime in X.2 Application.
  - Gate obligation: eligibility + impact live in strategies injected into the handler; **no** eligibility/impact `if`/`switch`. Single-impl, no selector until a second variant lands.
- `Proxy`
  - Why: only the session's assigned operator may penalize its teams; guard at the application boundary.
  - Phase owner: X.2 Application (`IScoringSessionAccessResolver` + `ScoringSessionAuthorizationProxy`) + X.4 Api (endpoint `[Authorize]` policy).
  - Gate obligation: Administrator unrestricted; Operator only when the actor owns the session (`AssignedOperatorUserId == actor.UserId`) else `ForbiddenAccessException`; no ad-hoc role/owner `if` in handler, controller, or DI. Ownership read from the **local session-assignment projection**.

> Resolution note: `required_patterns_matrix.md` mandates **both** `Strategy` and `Proxy` for the HU-38 row (`ScoringMonitoring` §). This is a mandated `Proxy`, not the informational applies-where tag (HU-04/05/36B).

---

## Pre-resolved orient (as of 2026-07-15)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-99 (HU-37 + HU-39) is **Done** and merged to `develop`: it landed the append-only `ScoreEntry` ledger (`Grant` factory only), `ScoreValue`/`ResolutionTime`, `ScoreEntryType`/`ScoreSourceType` (**both already carry `Penalty`**), `IScorePolicy`/`SnapshotScorePolicy` (**Strategy already present**), the `Ranking` projection, `ScoringMonitoringDbContext` + the `AddScoringLedgerAndRanking` migration, the per-type-exchange MassTransit registration, and the Api composition root (`Program.cs`, `RankingController`, `AuthorizationPolicies` with `AdministratorOrOperator` + `ParticipantOrOperator`, `ProblemDetailsExceptionHandler`, SignalR). **The service is populated, not greenfield.** DES-51/DES-54 are cancelled (folded into DES-99). Cross-service blockers DES-26 (HU-19 assignment) and DES-76 (HU-21A `LiveSession.AssignedOperatorUserId`) are both **Done**.

Full landed detail + mirror anchors + create/edit classification per phase are in `@backend/docs/hu38-context.md`.

### What HU-38 adds on top (per DES-53 + DES-85)

| Concern | New work |
|---|---|
| Justified penalty | Operator applies a `Penalty` to a team in a session they supervise, with mandatory `PenaltyReason`, recorded `appliedByUserId` + `appliedAt`. |
| Ledger deduction | Impacts score only through an append-only `ScoreEntry` of `EntryType.Penalty` (non-negative magnitude; total subtracts). |
| Eligibility Strategy | `IPenaltyPolicy` (new); `IScorePolicy` **reused** for the deduction magnitude. |
| Authorization Proxy | Restricts the action to the session's assigned operator (Administrator unrestricted). |
| Assignment projection | Consume `session-operator-assigned` → local read-model the Proxy reads. |
| Domain event | `PenaltyApplied` (+ inherited `ScoreEntryRegistered`) published post-commit; apply flow independent of RabbitMQ. |
| Backend contract | `POST /api/sessions/{liveSessionId}/penalties`, body `{ teamId, reason }`. |
| Frontend flow | Operator web UI to apply a justified penalty and see the score reflect the deduction. |

### Branch state and prerequisite

`feature/hu-38-justified-penalties`, base `develop`. DES-99's ledger is merged — the "land HU-37 first" gate is satisfied. **Before starting:** the assigned-operator integration event (`session-operator-assigned`) is **not yet published by session-operations** (only a domain event exists). Until it is (a cross-service item gated by GH #164), the projection is empty and the Proxy denies all Operators (Administrator passes). See Rationale + `hu38-context.md` Known quirks.

### Linear state (as of 2026-07-15)

- DES-53 (HU-38): **Todo**, labels: `svc:scoring-monitoring-service`, `svc:session-operations-service` (Proxy-only), `Feature`, `ready-for-agent`.
- DES-85 (PRD): **Backlog**, `ready-for-agent`, `svc:scoring-monitoring-service`.
- Build-on predecessor: DES-99 (HU-37 + HU-39) **Done**. Blockers DES-26/DES-76 **Done**. No same-service In Progress.

> Linear live state may have changed. Verify DES-53 labels via the Linear MCP if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`.

---

## 1. Orient - read service state, PRD, and landed ledger

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source or Linear state may have changed since 2026-07-15.

```text
Read the following and summarise what has been decided and implemented so far:
- @backend/services/scoring-monitoring-service/README.md and CONTEXT.md
- @backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md
- @backend/docs/hu37-39-context.md - the landed ledger + ranking build-on surface
- @backend/docs/hu38-context.md - the pre-resolved HU-38 context

Then inspect the landed scoring source enough to confirm the build-on surface:
- @backend/services/scoring-monitoring-service/src/Domain/Entities/ScoreEntry.cs
- @backend/services/scoring-monitoring-service/src/Domain/Services/IScorePolicy.cs
- @backend/services/scoring-monitoring-service/src/Application/Scores/Commands/RecordScoreEntry/
- @backend/services/scoring-monitoring-service/src/Api/Controllers/RankingController.cs

Then use the Linear MCP to fetch only the current live state of DES-53 and DES-85.

Output: which types HU-38 reuses vs. adds; confirmation that Penalty is a ScoreEntry child,
IScorePolicy/enums already exist, and the assigned-operator fact must be consumed (not fetched).
Do not start planning or implementing yet.
```

---

## 2. Label DES-53 as ready-for-agent

> DES-53 already carries `ready-for-agent` as of 2026-07-15. Use this step to confirm it remains present.

```text
Use the Linear MCP to confirm DES-53 still carries ready-for-agent.
If it is missing, add it.
Output the updated DES-53 ticket state and labels (note the double svc: label is Proxy-only).
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-53 carries svc:scoring-monitoring-service
and ready-for-agent, and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- Penalty as a ScoreEntry child (one-to-one), mandatory PenaltyReason + appliedAt + appliedByUserId
- deduction is an append-only ScoreEntry of EntryType.Penalty (non-negative magnitude), no mutable total
- IPenaltyPolicy eligibility Strategy; landed IScorePolicy reused for impact
- Proxy restricting to the assigned operator (Administrator unrestricted), no ad-hoc role/owner if
- assigned-operator fact consumed into a local projection, not an HTTP client
- PenaltyApplied (+ ScoreEntryRegistered) published post-commit; apply flow independent of RabbitMQ
- no recreation of the landed ledger / enums / IScorePolicy / DbContext / Api host

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning.
```

In the remaining examples below, `HU-38` and `DES-53` are the resolved values; `DES-85` is the shared PRD for `scoring-monitoring-service` (local file above).

---

## 4. Start the slice

```text
Prepare the justified-penalty slice on branch feature/hu-38-justified-penalties (base develop).
This slice affects backend scoring-monitoring-service and frontend.

The pre-resolved orient above lists what DES-99 already landed and what HU-38 adds. Do not
re-read the PRD for scoping unless you need a precise implementation detail.

HU-38 is a layer ON TOP of the landed ledger: reuse ScoreEntry/ScoreValue/IScorePolicy and the
existing enums; add only the Penalty child, IPenaltyPolicy, the ApplyPenalty command, the
authorization Proxy, the assignment projection, and the endpoint. Do NOT recreate the ledger,
ranking, DbContext, or Api host.

Move DES-53 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-38 in scoring-monitoring-service, per the
**X.1 derivation block in @backend/docs/hu38-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open). Types tagged (from HU-37) are landed build-on
surface — mirror/extend, do not recreate.

Gate:
- Domain build passes; a unit test per new/changed domain type (Penalty, PenaltyReason,
  DefaultPenaltyPolicy, PenaltyApplied, each new exception, and ScoreEntry.Penalty)
- ScoreEntry.Penalty raises ScoreEntryRegistered AND PenaltyApplied as an append-only deduction
  (EntryType.Penalty, non-negative magnitude, no mutator)
- Penalty requires non-blank PenaltyReason + appliedAt + appliedByUserId
- DefaultPenaltyPolicy eligibility covered (accept + reject); a penalty impacts score only via a
  ScoreEntry Penalty entry — no mutable total
- Strategy: IPenaltyPolicy is interface + sealed impl in Domain/Services/; IScorePolicy and the
  ScoreEntryType/ScoreSourceType enums are reused, not recreated

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
- clean build; handler tests cover valid apply, non-owning operator -> ForbiddenAccessException,
  Administrator unrestricted, ineligible penalty -> PenaltyNotEligibleException, missing/blank reason
- validator tests (reason required, ids present)
- access enforced through the Proxy resolver reading the local assignment projection — no ad-hoc
  role/owner if in the handler; policies injected/selected at runtime (no branching)
- the assignment consumer performs an idempotent projection upsert
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
full-reading it, as the block instructs).

Gate:
- build passes
- dotnet ef migrations add AddPenaltyAndSessionAssignmentProjection succeeds and represents Penalty
  (one-to-one under ScoreEntry) plus the session_operator_assignments projection
- repository integration test round-trips an append-only ScoreEntry Penalty + its Penalty child;
  the assignment consumer upserts the projection idempotently
- PenaltyApplied (+ ScoreEntryRegistered) published AFTER commit and a broker outage does not fail
  the apply path; no mutable score total persisted

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
- endpoint POST /api/sessions/{liveSessionId}/penalties returns success for the ASSIGNED operator
  applying a justified penalty; 403 RFC 7807 for a non-owning operator; 400 for a missing/blank reason
- the penalty is reflected as a ScoreEntry Penalty deduction
- access enforced through a Proxy-style guard: endpoint [Authorize(Policy = AdministratorOrOperator)]
  plus the X.2 access resolver — no ad-hoc role if
- service reaches the ADR-0005 coverage gate

(Note: assigned-operator SUCCESS is only observable once session-operations publishes
session-operator-assigned; until then verify Administrator success + the 403/400 branches and flag
the producer gap. See hu38-context.md Known quirks.)

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
- POST /api/sessions/{liveSessionId}/penalties as an Administrator with a valid reason -> success + ScoreEntry deduction
- same with a blank reason -> 400
- same as a non-owning Operator -> 403 (once the assignment projection is populated)

Output:
- container status
- smoke command results
- the apply-penalty request/response shape the frontend must consume
- whether session-operations yet publishes session-operator-assigned (cross-service flag)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the frontend plan concreteness rule
(below), modelled on the exemplar closest to this slice's shape
(@frontend/plans/hu-03-frontend-role-permission-assignment.md for a small 1-few-endpoint surface;
@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md for a large/multi-endpoint or
partially-blocked surface) — save it in @frontend/plans/ for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-38 justified penalties (operator web surface).

Scope:
- add a penalty data client + request/response types matching the verified POST
  /api/sessions/{liveSessionId}/penalties contract ({ teamId, reason } -> applied-penalty result)
- an operator action, on a supervised session's team, to apply a justified penalty with a REQUIRED
  reason; block submit on an empty reason
- surface the resulting score deduction (the team total reflects the penalty)
- surface the 403 (not your session) and 400 (missing reason) responses as clear operator feedback
- no ranking/audit/history UI (later HUs)

Gate:
- frontend typecheck/build passes
- the operator flow exercises apply-penalty against the real contract: success, 400 blank reason, 403 non-owner
- reason is enforced as required in the UI, not only server-side
- no UI copy implies a mutable score total edited directly (the deduction is a ledger fact)

Do not modify backend code in this step.
```

This Step's plan MUST follow the **frontend plan concreteness rule**:

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types,
   real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only
   for the fully-knowable near-term increments (the client + the apply-penalty form). Keep later,
   large, or blocked increments at contract + gate altitude. Never write code for an increment
   blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan
   names (exported vs. private helpers, exact signatures, the const/env it reads) and write only what
   the source supports. If a detail is not verifiable, state the assumption under Open Questions.
3. **Required sections** (a plan missing one is a defect): Context · Verified Backend Contract
   (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping ·
   Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no
   "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): apply justified penalties - HU-38

Ref: HU-38
Ref: DES-53
Ref: DES-85
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed.

```text
Use @frontend/AGENTS.md and the plan at @frontend/plans/<the HU-38 plan file written in Step 9>.
Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question (name it). Do not re-generate the
plan. Do not modify backend code.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-53 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- Penalty as a ScoreEntry child; mandatory PenaltyReason + appliedAt + appliedByUserId
- deduction is an append-only ScoreEntry of EntryType.Penalty; no mutable total
- IPenaltyPolicy eligibility Strategy; IScorePolicy reused for impact
- Proxy restricting to the assigned operator; no ad-hoc role/owner if
- assigned-operator fact consumed into a local projection, not an HTTP client
- PenaltyApplied (+ ScoreEntryRegistered) published post-commit; apply flow independent of RabbitMQ

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- backend API contract changes (the apply-penalty shape)
- frontend plan/file produced
- tests and gates run
- CROSS-SERVICE FLAG: whether session-operations yet publishes session-operator-assigned; if not,
  the Proxy denies all Operators until it does (GH #164)

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-38-justified-penalties \
  --title "feat(scoring-monitoring): apply justified penalties (HU-38)" \
  --body "Adds operator-applied justified penalties on scoring-monitoring-service on top of the DES-99 ledger: a Penalty child of ScoreEntry with a mandatory PenaltyReason/appliedAt/appliedByUserId, an append-only EntryType.Penalty deduction, IPenaltyPolicy eligibility (Strategy) reusing IScorePolicy for impact, an assigned-operator Proxy reading a consumed session-operator-assigned projection, and POST /api/sessions/{liveSessionId}/penalties. Cross-service dependency: session-operations must publish session-operator-assigned (GH #164) before the Proxy admits Operators."
```

---

## Rationale

- **Not greenfield — a layer on the landed ledger.** The earlier HU-38 draft was written on 2026-07-12 assuming a greenfield service and that HU-37 had not merged. DES-99 (HU-37 + HU-39) is now Done: the ledger, `IScorePolicy`, the enums (`Penalty` cases included), the `ScoringMonitoringDbContext`, and the Api host all exist. HU-38 therefore **reuses** those and adds only the penalty layer. Recreating any of them is a defect the derivation blocks explicitly call out.

- **The deduction is a non-negative magnitude tagged `Penalty`.** `ScoreValue.Create` rejects negatives, so the penalty is not a negative score — it is a `ScoreEntry` of `EntryType.Penalty` whose magnitude the derived total subtracts. This keeps the ledger append-only and the total explainable (PRD Out-of-Scope: no mutable total).

- **Cross-service authorization — consume, don't call (design decision, with an open dependency).** The assigned-operator fact lives in session-operations. HU-38 consumes `session-operator-assigned` into a local projection the `Proxy` reads, rather than an HTTP access client — the repo has **no** cross-service HTTP-auth-client convention, and the consume path fits the service's consume-facts boundary and existing `IConsumer` stack. **Open dependency:** session-operations currently emits `LiveSessionOperatorAssignedEvent` only as a domain event; the `session-operator-assigned` integration event / publisher **does not exist yet** and is gated by GH #164. Until it lands, the projection is empty and the Proxy admits only Administrators. This must be flagged at Stop 2 as a cross-service contract item — it is not fixed from this service.

- **Messaging never gates the core flow.** Both the inbound assignment consume and the outbound `PenaltyApplied`/`ScoreEntryRegistered` publish are MassTransit-native and gated by GH #164, but the apply + persist path must complete independently of RabbitMQ (ticket AC): a broker outage never fails or rolls back the ledger write.

- **Why the patterns differ from the ledger slice.** HU-37/HU-39 (DES-99) carried `Strategy` only (scoring/ranking policies) and inherited the standard gateway guard — no `Proxy`. HU-38 adds a **mandated `Proxy`** because a penalty is a privileged, session-scoped mutation restricted to the assigned operator, and keeps `Strategy` for eligibility (`IPenaltyPolicy`) + impact (reused `IScorePolicy`).
