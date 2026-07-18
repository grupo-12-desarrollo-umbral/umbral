# Prompt Example — HU-32 Trazabilidad de evidencias

Concrete prompt sequence for driving `DES-43` / `HU-32` through the backend slice
on `feature/hu-32-evidence-traceability`. Follows
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-32:** every sibling in this family (HU-29/30/31/34) built the **write** half
of the `EvidenceSubmission` umbrella — intake, contextual validation, the two concrete forms, and the
`EvidenceSubmissionRegistered` outbox bridge. All are Done and merged. HU-32 is the **read** half, fed
**asynchronously**: it adds the two outcome facts the umbrella never published
(`EvidenceSubmissionAccepted` / `EvidenceSubmissionRejected` — canon `ddd_solution_model.md:327-328`,
deferred by HU-30 precisely because HU-32 is their consumer), the **first consumer in this service**,
which projects those facts into an evidence-trace read model, and the operator read surface over it.
Do **not** rebuild the base, the facade, either chain, or either concrete form.

**HU-32 adds no operator-mediated review.** ADR-0010 is explicit: both evidence forms are
system-resolved; `reviewedByUserId`/`reviewedAt` stay out. DES-43's body repeats it — "Trazabilidad/
auditoría sí; revisión mediada por operador **no**."

`DES-43` has **no** `backend-only` label, so this slice **does** carry a frontend increment: a small,
read-only operator traceability panel over the one new endpoint (Steps 9 + 9b).

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`.

---

## Required design patterns

- **None mandated.** `required_patterns_matrix.md:45` lists `HU-32` under "— (no mandated pattern)";
  `:125` reads "`HU-32` | — | — | Evidence traceability — audit read; `Proxy` guards operator access."
  Do not invent one to fill the gap.

- `Proxy` — **applies-where, no new gate.** The matrix tags HU-32 at `:125` with the same
  applies-where phrasing it uses for HU-04/05. The operator read inherits the **existing** ADR-0009
  ownership resolver Proxy (`ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync`) plus
  `[Authorize(Roles = "Operator")]` on the query and `[Authorize(Policy = Operator)]` on the endpoint —
  exactly as `GetOperatorTriviaAnsweredMonitorQueryHandler` does. No new pattern gate; no ad-hoc
  role/owner `if` in the handler.

Transport obligation:
- **RabbitMQ — publish *and*, for the first time in this service, consume.** Publish the two new
  outcome facts through the existing MassTransit EF-Core bus outbox (ADR-0017), and **consume**
  registered/accepted/rejected back to feed the trace (AC #5). The matrix transport table (`:59`) lists
  HU-32 under "neither" — it is **stale on this point**, overridden by DES-43 AC #5 and canon
  `ddd_solution_model.md:615` ("`EvidenceSubmissionRegistered` … then consumed by audit/history …
  or projection-support flows"). No second publisher stack; no exchange bootstrap
  (`cfg.ConfigureEndpoints(context)` is already wired).

---

## Pre-resolved orient (as of 2026-07-14)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-43` is a feature build on a **complete, merged** evidence write path:

- **DES-39 / HU-29** — the intake substrate: abstract `EvidenceSubmission` base (already carrying
  `SubmittedAt`, `LiveSessionId`, `TeamId`, `ActiveSubstageId`, `ValidationState` — **AC #1 and AC #2
  are already persisted**), `IEvidenceIntakeFacade`, the generic `EvidenceIntakeValidationChain`,
  `LiveSession.RegisterEvidenceCore`, and the `EvidenceSubmissionRegistered` domain→integration→outbox
  bridge. **Reuse, do not rebuild.**
- **DES-95 / HU-30** — the rejection reason (**AC #4**): `EvidenceSubmission.RejectionReason`
  (`EvidenceRejectionReason?`) + the `Reject(reason)` transition + the contextual `EvidenceValidationChain`
  + the facade's `RegisterPendingAsync` seam + the `rejection_reason` column.
- **DES-42 / HU-31** — the QR form: `TreasureEvidenceSubmission` with `ScannedValue`,
  `TargetSnapshotId?`, and its **own** `ResolutionRejectionReason` (`TargetResolutionRejectionReason?`) —
  a *second, disjoint* reason model that leaves the base `RejectionReason` **null**.
- **DES-46 / HU-34** — the trivia form: `TriviaAnswerSubmission` (`QuestionSequenceOrder`, `IsCorrect`,
  `ScoreValue`), auto-accepted on registration; late/duplicate answers **throw** and are never persisted.
- **DES-29 / HU-21** — the audit precedent + producer template: `SessionEvent`, the
  `SessionStateChanged` publish handler, and the two-handler dispatcher arm to mirror.
- **MassTransit EF-Core transactional bus outbox** — publish is a pre-commit local `OutboxMessage`
  insert riding the business `SaveChanges`, drained asynchronously by `BusOutboxDeliveryService`.
  `cfg.ConfigureEndpoints(context)` is already present, so `bus.AddConsumer<T>()` is enough to create a
  receive endpoint.
- **Consumer exemplar (cross-service):**
  `scoring-monitoring-service/src/Infrastructure/Messaging/Consumers/AnswerRegisteredConsumer.cs` — the
  only consumer in the backend today. Thin adapter: `(ISender, ILogger)` → map → MediatR. Mirror it.

No predecessor of this HU is superseded; DES-43 sits in the realignment map's ✅ living row
(`canon-realignment-after-mission-runtime-rewrite.md:89`). DES-13 (HU-08) and DES-38 (HU-28) are In
Progress but are **not** build-on, so the branch base is `develop`.

### What HU-32 adds on top

| Concern | New work |
|---|---|
| Outcome facts | `EvidenceSubmissionAcceptedEvent` / `EvidenceSubmissionRejectedEvent` (canon `ddd:327-328`), raised on the base's resolution transitions so every path emits exactly one |
| Normalized reason | one reason string on the rejected fact, normalized from the **two disjoint** reason models (base contextual + QR resolution) |
| Origin reference | `OriginReference` on the registered fact (trivia question / QR target) — AC #2's *"nodo de misión o pregunta de trivia"* grain |
| Transport bridge | two `[EntityName]` contracts + two publish handlers + two `OutboxDomainEventDispatcher` arms |
| Trace read model | `EvidenceTraceEntry` projection (own table + `DbSet` + repository), keyed by `EvidenceSubmissionId` |
| First consumer here | three thin `IConsumer<>` adapters → MediatR upsert commands (AC #5) |
| Operator read | `GET /api/sessions/{liveSessionId}/evidence-submissions` (Operator-guarded, optional `?teamId=`) |
| Frontend | small read-only operator traceability panel (one endpoint) |

### Branch state and prerequisite

`feature/hu-32-evidence-traceability` branches from `develop`. All build-on predecessors are
Done/merged; no build-on predecessor is In Progress, so there is no feature-branch dependency to
inherit first.

### Linear state (as of 2026-07-14)

- DES-43 (HU-32): **Todo**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-39, DES-95, DES-42, DES-46, DES-29
- DES-33 (HU-24B — operator panel of events, evidence and ranking) is **blocked by** DES-43: it is a
  downstream consumer of this HU's read surface, not part of this slice

> Linear live state may have changed. Use the Linear MCP to verify DES-43 status and labels if needed,
> but do not re-fetch PRD scope from Linear — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-32 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if
> the service source, README, or Linear state may have changed since 2026-07-14.

```text
Read the following files and summarize what has already landed and what HU-32 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/adr/0010-evidence-qr-only-first-delivery.md
- @backend/docs/hu32-context.md

Then inspect only the current session-operations evidence/transport seams you need to anchor on:
- @backend/services/session-operations-service/src/Domain/Entities/EvidenceSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/TreasureEvidenceSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs
- @backend/services/session-operations-service/src/Application/Sessions/Queries/GetOperatorTriviaAnsweredMonitor/
- @backend/services/session-operations-service/src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs
- @backend/services/scoring-monitoring-service/src/Infrastructure/Messaging/Consumers/AnswerRegisteredConsumer.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-43 (HU-32 - Trazabilidad de evidencias)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-43 status and labels
- confirmation that EvidenceSubmissionRegisteredEvent is raised with a hardcoded Pending, before any
  accept/reject — so the outcome facts (EvidenceSubmissionAccepted/Rejected) are required for AC#3/#4
- the two disjoint rejection-reason models (base EvidenceRejectionReason vs TreasureEvidenceSubmission
  .ResolutionRejectionReason) that must be normalized into one reason on the rejected fact
- confirmation that operator-mediated review is OUT of scope (ADR-0010) — no reviewedByUserId/reviewedAt
- that this service has no production consumer today, so HU-32 introduces the first one

Do not start planning or implementing yet.
```

---

## 2. Label DES-43 as ready-for-agent

```text
Use the Linear MCP to confirm DES-43 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-43 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-43 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm:
- the evidence write path (HU-29 intake, HU-30 contextual validation + rejection reason, HU-31 QR form,
  HU-34 trivia form) is Done and merged — reuse it, do not rebuild it
- AC#1 (submittedAt) and AC#2 (team/session/substage) are already persisted on the EvidenceSubmission
  base; HU-32 surfaces them and adds the finer origin grain (trivia question / QR target)
- AC#3/#4 are NOT reachable from EvidenceSubmissionRegistered alone (it carries a hardcoded Pending),
  so HU-32 adds the canonical EvidenceSubmissionAccepted/EvidenceSubmissionRejected facts
- the rejected fact carries a normalized reason sourced from EITHER the base EvidenceRejectionReason
  (contextual) OR TreasureEvidenceSubmission.ResolutionRejectionReason (QR) — a QR rejection leaves the
  base field null
- AC#5 means the trace is fed by CONSUMING RabbitMQ events (the first consumer in this service), not by
  an in-process notification handler; the bus outbox already keeps the broker off the write path
- operator-mediated review is out of scope (ADR-0010): no reviewedByUserId/reviewedAt, no approve/reject
  endpoint
- the trace covers registered evidence only — trivia late/duplicate answers and HU-29 structural
  intake failures throw and are never persisted

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining examples below, `HU-32` and `DES-43` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-32 slice on branch feature/hu-32-evidence-traceability.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` plus a small read-only frontend panel.

The pre-resolved orient at the top of this document lists what existing code has already
landed and what HU-32 adds. Do not re-read the PRD for scoping unless you need to resolve
a precise implementation detail.

Move DES-43 to In Progress if the team process requires it, and output the exact scope,
branch name, base branch, and touched surfaces. Note explicitly that DES-33 (HU-24B, the operator
panel of events/evidence/ranking) is a downstream consumer blocked by this ticket — its panel is NOT
part of this slice.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-32 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu32-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; unit tests cover every new public domain type
- a contextual Reject(reason) raises exactly one EvidenceSubmissionRejectedEvent carrying the
  normalized base reason — and does NOT backfill EvidenceSubmission.RejectionReason from QR
  (HU-31's invariant at EvidenceSubmission.cs:81-84; the QR path leaves the base field null and
  carries its reason only on the event)
- a QR RejectRegisteredTarget(reason) raises exactly one EvidenceSubmissionRejectedEvent carrying the
  TargetResolutionRejectionReason — NOT null (the two-reason-model trap)
- an accepted QR scan and an accepted trivia answer each raise exactly one EvidenceSubmissionAcceptedEvent
- EvidenceSubmissionRegisteredEvent now carries an OriginReference identifying the trivia question / QR
  target, and still fires on every registered submission
- EvidenceTraceEntry records submittedAt + session/team/substage/origin + state + reason, and its
  MarkAccepted/MarkRejected are idempotent (re-applying the same resolution is a no-op, not a throw);
  it carries a one-line code comment noting the reviewer fields are excluded per ADR-0010 (override
  of the stale hu30-context.md:26,87 note)
- all pre-existing HU-29/30/31/34 domain tests remain green — no observable change to intake,
  validation, or either form's behaviour
- the OwnsMany interceptor probe passes: a ParentEntity : BaseEntity that OwnsMany(ChildEntity) with a
  ChildEntity : BaseEntity raising a domain event on save, asserting the event reaches the dispatcher
  exactly once — converts the Stop-1 "raise on owned-entity entries" EF assumption into a green
  check before X.2/X.3 build on it (mirror DispatchDomainEventsInterceptorBranchTests.cs; ~30 lines)
- no design pattern is introduced: HU-32 has no mandated pattern

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-32)

Ref: HU-32
Ref: DES-43
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-32 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu32-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes
- both publish handlers map their domain event to its [EntityName] contract and publish via
  IPublishEndpoint; a publish failure PROPAGATES (act.Should().ThrowAsync(), rolling the transaction
  back) — no swallow, no timeout, no IIntegrationEventPublisher
- OutboxDomainEventDispatcher routes EvidenceSubmissionAcceptedEvent and EvidenceSubmissionRejectedEvent
  to them, and every existing arm is unchanged
- the two trace-write handlers are idempotent (re-applying the same message is a no-op) and
  order-tolerant (a resolution arriving before its registration still yields a correct row)
- GetOperatorEvidenceTraceQuery carries [Authorize(Roles = "Operator")] and its handler resolves access
  ONLY through ISessionAdministrationAccessResolver — the inherited ADR-0009 resolver Proxy, no ad-hoc
  role/owner if-check (applies-where Proxy: reuse the guard, add no new gate)
- the read returns each registered evidence with submittedAt, team/session/substage/origin, validation
  state, and rejection reason; a QR-rejected item surfaces a NON-NULL reason; optional teamId filters
- no new facade, no second publisher stack

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-32)

Ref: HU-32
Ref: DES-43
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-32 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu32-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Infrastructure build passes
- ef migrations add produces ONLY the evidence_trace_entries table — assert the diff: no change to
  live_sessions, its owned-child tables, or the outbox tables
- a trace entry round-trips with state + reason + resolvedAt, and upserting the same
  EvidenceSubmissionId twice yields ONE row (at-least-once delivery safety)
- an integration test proves the domain facts are inserted into the transactional outbox atomically
  with the evidence write
- a broker-backed delivery test proves that submitting evidence end-to-end results in a trace row
  reaching its terminal state (Accepted, or Rejected WITH a non-null reason for a wrong QR scan) once
  the outbox drains — polled with a timeout, not asserted synchronously
- the three consumers are thin transport adapters (ISender + ILogger, map, send) with no projection
  logic, no DbContext and no repository in the consumer body (ADR-0017 section 5); each carries a
  one-line comment noting it is the first audit/history consumer in this service per AC#5 /
  ddd_solution_model.md:615 (override of the stale matrix transport table :59 "neither")
- consumers are registered with bus.AddConsumer<T>(); ConfigureEndpoints already creates the receive
  endpoints — no topology hand-wiring, no exchange bootstrap, no change to the AddEntityFrameworkOutbox
  block, no second publisher stack

Do not touch Api.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-32)

Ref: HU-32
Ref: DES-43
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-32 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu32-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- the assigned operator gets the session's registered evidence with date/time, team, session, substage
  + origin, validation state, and (when rejected) the rejection reason
- ?teamId= filters to one team
- a non-assigned operator gets RFC 7807 403, and an unauthenticated/participant caller is rejected —
  through the inherited ADR-0009 resolver Proxy, with no ad-hoc check and no controller try/catch
  (ADR-0018)
- an end-to-end test proves a QR scan submitted through the participant endpoint appears in the trace
  with its terminal state after the outbox drains and the consumer runs — polled, since the read is
  eventually consistent by AC#5 — including a rejected scan carrying its reason
- the write path is unaffected when the broker is down: the evidence write still commits and the trace
  catches up on recovery (bus outbox off the critical path)
- service coverage passes the ADR-0005 gate (coverlet.msbuild, aggregate branch coverage >= 95%)

Do not touch frontend or mobile.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-32)

Ref: HU-32
Ref: DES-43
Ref: DES-70
```

---

## 8.5. Docker rebuild

```text
Rebuild and restart the backend runtime for HU-32:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke the new read path and transport through the gateway:
- submit a correct QR scan, then GET /api/sessions/{liveSessionId}/evidence-submissions as the assigned
  operator -> the evidence appears with validationState=Accepted (allow a second for the outbox to drain)
- submit a wrong / duplicate / out-of-context scan -> it appears with validationState=Rejected and a
  non-null rejectionReason
- GET .../evidence-submissions?teamId={teamId} -> filtered to that team
- GET as a non-assigned operator -> RFC 7807 403
- session-evidence-submission-registered / -accepted / -rejected are observable on the RabbitMQ
  exchanges, and their receive endpoints (queues) exist and are draining
```

---

## 9. Frontend slice — generate the plan

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule**
(below), modelled on the exemplar closest to this slice's shape
(`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface;
`@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or
partially-blocked surface) — save it in `@frontend/plans/` for the following:

Use @frontend/AGENTS.md

HU-32 operator evidence-traceability panel — a small, READ-ONLY surface over exactly one new endpoint:
GET /api/sessions/{liveSessionId}/evidence-submissions?teamId={optional}, Operator-guarded.
This slice's shape matches the hu-03 exemplar (single endpoint, few files), not hu-10a.

Seed scope:
- an operator-facing panel listing the session's registered evidence: submission date/time, team,
  substage + origin (trivia question / QR target), submission type, validation state, and the rejection
  reason when rejected
- optional filter by team
- mirror the existing operator read panels in `@frontend/app/dashboard/` — the closest shape is
  `AnsweredMonitorPanel.tsx` (HU-36A: an operator read panel over a single session-scoped endpoint),
  with its server action + client fn + module.css conventions
- read-only: no mutation, no approve/reject controls (ADR-0010 — operator-mediated review is out of
  scope and the backend exposes no such endpoint)
- the read is EVENTUALLY CONSISTENT (the backend feeds it by consuming RabbitMQ events), so just-
  submitted evidence may lag by ~a second — the plan must state how the UI handles it (e.g. a refresh
  affordance and/or a poll), and must not present staleness as an error state

Gate: the plan is reviewed by a human before any frontend code is written (Step 9b implements it).

**Frontend plan concreteness rule** (follow verbatim):

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types,
   real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only
   for the **fully-knowable near-term increments** (typically the foundation + first authoring
   increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract
   table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an
   open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan
   names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor
   targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g.
   "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail
   is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context ·
   Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment**
   (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment ·
   **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no
   "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.
```

---

## 9b. Frontend slice — implement the plan

> Run only after the Step 9 plan is written and reviewed.

```text
Use @frontend/AGENTS.md and the Step 9 plan at @frontend/plans/hu-32-frontend-operator-evidence-traceability.md.

Implement phase by phase in the plan's order, per the plan's own Scope / Gate / Commit Sequence.
The plan is the source of truth and supersedes the Step 9 seed scope.

Stop at any increment the plan marks blocked on an Open Question and name it.
Do not re-generate the plan. Do not modify backend code.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-43 acceptance criteria are satisfied:
- each registered evidence retains its submission date and time
- each evidence identifies team, session, and origin substage (mission node or trivia question)
- the system shows the evidence's validation state
- a rejected evidence retains its rejection reason — including QR rejections, whose reason lives on
  TreasureEvidenceSubmission.ResolutionRejectionReason, not on the base field
- the traceability/history is fed asynchronously by consuming the domain events published on RabbitMQ,
  and the main flow does not depend on RabbitMQ (the bus outbox keeps the broker off the write path:
  the evidence write commits and the trace catches up when the broker recovers)

Also confirm the ticket boundary:
- operator-mediated review is NOT implemented (ADR-0010): no reviewedByUserId/reviewedAt, no
  approve/reject endpoint or control
- the trace covers registered evidence only (trivia late/duplicate answers and structural intake
  failures throw and are never persisted) — HU-29/HU-34 behaviour is unchanged
- DES-33 (HU-24B operator panel) is a downstream consumer of this read surface and is not in this PR

Then open the PR:

gh pr create \
  --base develop \
  --head feature/hu-32-evidence-traceability \
  --title "feat(session-operations): HU-32 evidence traceability" \
  --body "Implements HU-32 / DES-43: the read half of the EvidenceSubmission umbrella. Adds the canonical outcome facts EvidenceSubmissionAccepted/EvidenceSubmissionRejected (deferred by HU-30 for want of a consumer), publishes them through the existing MassTransit bus outbox, and introduces this service's first consumers, which project registered/accepted/rejected into an EvidenceTraceEntry read model. Exposes the operator read GET /api/sessions/{liveSessionId}/evidence-submissions (optional teamId filter) surfacing submission date/time, team, session, substage + origin, validation state, and the normalized rejection reason across both reason models. Includes a small read-only operator traceability panel. No operator-mediated review (ADR-0010)."
```

---

## Rationale

- **Why HU-32 adds domain events at all, when it is 'just a read':** `EvidenceSubmissionRegistered` is
  raised by `LiveSession.RegisterEvidenceCore` with a **hardcoded** `EvidenceValidationState.Pending`,
  before any accept/reject runs. A projection fed only by it would show every evidence as `Pending`
  forever, so AC #3 (validation state) and AC #4 (rejection reason) would be unreachable. The canonical
  outcome facts `EvidenceSubmissionAccepted`/`EvidenceSubmissionRejected` (`ddd_solution_model.md:327-328`)
  are exactly the missing link, and HU-30 deferred them *by name to this HU* ("no in-scope consumer —
  result query is HU-32", `hu30-context.md:85`). This is not new scope invented here; it is scope the
  predecessor parked. **(Stop-1 closed — scope nuance.)** HU-30 deferred the *event types*; the
  **publish** side (contracts + publish handlers + `OutboxDomainEventDispatcher` arms) is **new scope
  added by this HU**, not inherited. Do not conflate "this HU inherits HU-30's parked events" with
  "it inherits HU-30's parked publishers" — HU-30 parked none.
- **Why the reason must be normalized (Stop-1 closed):** there are **two disjoint** reason models —
  `EvidenceSubmission.RejectionReason` (`EvidenceRejectionReason`, HU-30's contextual rejections) and
  `TreasureEvidenceSubmission.ResolutionRejectionReason` (`TargetResolutionRejectionReason`, HU-31's QR
  match failures) — and HU-31 deliberately does **not** set the base field. Reading only the base field
  would report "no reason" for every rejected QR scan, which is the single most likely defect in this
  HU. The rejected fact therefore carries one normalized reason string. **Do not** backfill
  `EvidenceSubmission.RejectionReason` from `TreasureEvidenceSubmission.RejectRegisteredTarget` —
  `EvidenceSubmission.cs:81-84` documents HU-31's invariant; mutating it from HU-31's path is a
  stealth contract change against merged predecessor code (Constraint 2). A `RejectionSource`
  discriminator (contextual vs QR-resolution) is **deferred** — AC #4 only requires the reason text;
  it's an additive nullable later if the frontend UX names the need.
- **Why the trace is fed through the broker rather than in-process:** an in-process notification handler
  projecting straight from the domain event would be simpler, but AC #5 is explicit that the history
  "se alimenta de forma asíncrona **consumiendo los eventos de dominio publicados en RabbitMQ**", and
  canon names the same workflow (`ddd_solution_model.md:615` — `EvidenceSubmissionRegistered` "then
  consumed by audit/history … or projection-support flows"). So the service publishes and consumes its
  own facts. It is this service's first consumer; `cfg.ConfigureEndpoints(context)` is already wired,
  so only `bus.AddConsumer<T>()` is added. The AC's "sin que el flujo principal dependa de RabbitMQ" is
  already satisfied structurally by the shipped bus outbox — the write path never touches the broker.
- **Why the read is eventually consistent, and why that is not a bug:** it follows directly from AC #5.
  Tests must poll for the projection; the UI must present the lag as freshness, not error.
- **Why no pattern is introduced:** `required_patterns_matrix.md:45` puts HU-32 in the no-mandated-pattern
  set, and `:125` adds only an applies-where `Proxy` note — the operator read reuses the existing ADR-0009
  ownership resolver Proxy, which it would inherit anyway. Per `adr/0012:64`'s ceremony test, nothing here
  earns a new pattern gate. (Note: `generator-agent.md` enumerates the applies-where set as "HU-04/05/36B
  only", but its governing rule is to carry the note for the HUs *the matrix actually tags* — and the
  matrix tags HU-32 at `:125`. Flagged at Stop 1 rather than silently resolved.)
- **Why the reviewer fields are excluded even though canon models them (Stop-1 closed):**
  `bd_umbral_entity_spec.md:432-433` and `:982` (RF-09) model `reviewedByUserId`/`reviewedAt`, but
  ADR-0010 §Decisions in scope overrides for first delivery — both forms are system-resolved and
  "operator-mediated human review is out of scope". DES-43's body says the same. **`hu30-context.md:26,87`
  claims those fields are HU-32's — that note is stale** and is superseded by ADR-0010 + the realigned
  DES-43 body. **Resolved:** the fields are excluded; `EvidenceTraceEntry` carries a one-line code
  comment (`// reviewer fields excluded per ADR-0010; override of hu30-context.md:26,87`) so the
  override is discoverable at the code site. Do not update `hu30-context.md` — its inaccuracy stays as
  historical record; the code comment + ADR-0010 are the governed artifacts.
- **Where the outcome events are raised (Stop-1 closed): commit to raising on the base's three
  resolution transitions, gated by a targeted `OwnsMany` probe.** All four resolution paths funnel
  through `Reject`, `MarkAcceptedByConcreteForm`, `MarkRejectedByConcreteForm(string reason)`, so
  raising there covers every path exactly once. The risk — that `ChangeTracker.Entries<BaseEntity>()`
  in the interceptor (`DispatchDomainEventsInterceptor.cs:104,:126`) does not collect events raised
  on owned-entity entries — is an untested EF assumption: the existing
  `DispatchDomainEventsInterceptorBranchTests.cs` exercise only a standalone `DbSet<TestEntity>`, not
  an `OwnsMany` parent/child relationship. **Resolved:** X.1 adds an `OwnsMany` probe mirroring
  `DispatchDomainEventsInterceptorBranchTests.cs` (in-memory DbContext, `ParentEntity` `OwnsMany`
  `ChildEntity` raising a domain event on save, assert the event reaches the dispatcher exactly once;
  ~30 lines). If the probe passes, commit. If it surprisingly fails, fall back to raising from
  `LiveSession` + `LiveSession.RejectEvidence(submission, reason)` for the facade path — the cost of
  being wrong is small and localized.
- **Why the trace read model's name is derived:** canon's nearest artifact is `EvidenceReviewQueueProjection`
  (`bd_umbral_entity_spec.md:931`), but its framing — a *pending review queue* for operator decisions — is
  precisely what ADR-0010 puts out of scope. Canon names no artifact for a system-resolved traceability
  projection, so `EvidenceTraceEntry` is derived here, keeping canon's projection *shape* (`:925-931`) and
  its repository rule (`ddd_solution_model.md:366` — repositories around "aggregate roots and clearly owned
  projections"). Renaming to the canonical term is a Stop-1 call.
- **Why the trace covers only registered evidence:** HU-34's late/duplicate answers and HU-29's structural
  intake failures **throw** — no record is ever persisted. AC #1 says "Cada evidencia **registrada**", so
  this matches. Widening the trace to blocked attempts would change HU-29/HU-34's shipped behaviour and is
  out of scope (Constraint 2); if the product wants blocked attempts audited, that is a separate ticket.
