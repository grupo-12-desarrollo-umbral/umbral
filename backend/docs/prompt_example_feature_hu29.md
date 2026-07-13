# Prompt Example — HU-29 Common evidence-submission intake (umbrella)

Concrete prompt sequence for driving `DES-39` / `HU-29` through the backend slice
on `feature/hu-29-evidence-submission-intake`. Follows
[workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for HU-29:** this is an **extraction / generalization** slice, not a greenfield
feature. HU-34 (DES-46, Done) already shipped the `EvidenceSubmission` umbrella base, the
`TriviaAnswerSubmission` specialization, the `LiveSession.RegisterTriviaAnswer` skeleton, the
`TriviaAnswerValidation` Chain of Responsibility, and the MassTransit-outbox event bridge —
explicitly "shaped so later HU-29/HU-30A can extract into the shared umbrella pipeline." HU-29
**lifts** that shared intake substrate into a reusable `Facade` + generic `Chain`, adds the generic
`EvidenceSubmissionRegistered` intake event, and publishes it through the existing outbox so the
trivia path demonstrates the RabbitMQ workflow end-to-end. It does **not** rebuild HU-34, **not**
build the QR form (HU-31 / DES-42), and **not** build the deeper pre-acceptance validation
(HU-30A / DES-40). The invariant is "generalize one shared intake pipeline without regressing the
trivia path," not "add a new gameplay action."

When working from the monorepo root, make the target workload explicit in each prompt. For backend
steps, point to `@backend/.agents/backend-agent.md`.

---

## Required design patterns

- `Facade`
  - Why: `required_patterns_matrix.md:40,122` — on transactional success the Facade publishes
    `EvidenceSubmissionRegistered` (the canonical RabbitMQ workflow) over the shared intake subsystem.
  - Phase owner: X.2 Application.
  - Gate obligation: a **discrete** `EvidenceIntakeFacade` in `Sessions/Common/` (shared by ≥2
    consumers — trivia now, QR in HU-31 — so ADR-0013's single-consumer inline rule does **not**
    apply) is the single orchestration entry point: chain → domain registration core → persist →
    outbox. No ad-hoc intake logic in the command handler.

- `Chain of Responsibility`
  - Why: `required_patterns_matrix.md:42,122` — evidence submission runs a composable validation
    pipeline.
  - Phase owner: X.2 Application.
  - Gate obligation: ordered generic intake-admission links (runtime participation →
    session-admits-reception → active-substage-present) short-circuit on first failure; the trivia
    chain is refactored to compose these shared links + its trivia-specific links. HU-29 owns only
    the generic intake links — target/question validators stay with HU-30A/HU-31.

Transport obligations:
- **RabbitMQ** — publish `EvidenceSubmissionRegistered` after transactional success through the
  existing MassTransit EF-Core **transactional bus outbox** (ADR-0017 as amended 2026-07-12):
  a pre-commit `OutboxMessage` insert that rides the business `SaveChanges`; async broker delivery
  by `BusOutboxDeliveryService`. No second publisher stack, no post-commit five-second publish, no
  hand-rolled AMQP client.

---

## Pre-resolved orient (as of 2026-07-13)

> Step 1 has already been run. Paste this section into any agent session that needs context before
> picking up a phase; no need to re-run the orient prompt unless source or Linear state changed.

### What predecessors have already landed

`DES-39` is an **extraction feature** over the realigned session runtime and the Done HU-34 substrate:

- **DES-46 / HU-34** — `EvidenceSubmission` abstract base + `TriviaAnswerSubmission`, the
  `RegisterTriviaAnswer` skeleton, the `TriviaAnswerValidation` chain, and the outbox bridge
  (`OutboxDomainEventDispatcher` → `PublishAnswerRegisteredIntegrationEventHandler` →
  `IPublishEndpoint`). This is the code HU-29 extracts.
- **DES-76 / HU-21A** — session state machine; the "session admits reception" gate
  (`Paused`/`Finished`/`Cancelled` reject gameplay).
- **DES-78 / HU-33A** — active-substage pointer / active-question runtime.
- **DES-11 / DES-12 / HU-07A / HU-07B** — `RuntimeParticipationGuard` (participant admission).
- **Outbox infra** — MassTransit EF-Core transactional bus outbox (`20260713020055_AddMassTransitTransactionalOutbox`, ADR-0017 amended).

`DES-39` was Canceled twice under the old "QR-only" framing and **re-activated by ADR-0010
(Accepted 2026-07-10)** as the generic evidence intake — it is not in any supersession column.
No *other* same-service build-on predecessor is In Progress, so the branch base is `develop`.

### What HU-29 adds on top

| Concern | New work |
|---|---|
| Generic intake event | `EvidenceSubmissionRegisteredEvent` (`ddd_solution_model.md:326`), absent from code today |
| Initial `pending` | Add `Pending` to `EvidenceValidationState` (`CONTEXT.md:101-102`; PRD AC) |
| Shared registration core | Extract the common prefix of `RegisterTriviaAnswer` on `LiveSession` |
| Intake Facade | `EvidenceIntakeFacade` (`Sessions/Common/`, shared) — chain → core → persist → outbox |
| Generic intake Chain | `EvidenceIntakeValidation/` generic links, composed by the trivia chain |
| RabbitMQ workflow | `EvidenceSubmissionRegisteredIntegrationEvent` via the outbox — demonstrated end-to-end through the trivia path |
| Contract | **No new participant endpoint** (base is abstract); new **async** `EvidenceSubmissionRegistered` for audit/history/notification/projection consumers |

### Branch state and prerequisite

`feature/hu-29-evidence-submission-intake` branches from `develop`. No *other* same-service
build-on predecessor is In Progress, so there is no feature-branch dependency to inherit first.

### Linear state (as of 2026-07-13)

- DES-39 (HU-29): **In Progress**, labels: `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-70 (PRD): **Backlog**, labels: `svc:session-operations-service`, `canon-realign`, `ready-for-agent`
- Same-service Done build-on predecessors: DES-46, DES-76, DES-78, DES-11, DES-12

> Linear live state may have changed. Use the Linear MCP to verify DES-39 status and labels if
> needed, but do not re-fetch PRD scope — read the local PRD file at
> `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.

---

## 1. Orient — read service state and the resolved HU-29 context

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the
> service source, README, or Linear state may have changed since 2026-07-13.

```text
Read the following files and summarize what has already landed and what HU-29 must add:
- @backend/services/session-operations-service/README.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/adr/0010-evidence-qr-only-first-delivery.md
- @backend/docs/adr/0013-facades-in-application-command-slices.md
- @backend/docs/adr/0017-masstransit-abstractions-in-application.md
- @backend/docs/hu29-context.md

Then inspect only the current session-operations evidence/intake seams you need to anchor on:
- @backend/services/session-operations-service/src/Domain/Entities/EvidenceSubmission.cs
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/TriviaAnswerValidation/TriviaAnswerValidationChain.cs
- @backend/services/session-operations-service/src/Application/Sessions/Common/SessionTeamAssociationFacade.cs
- @backend/services/session-operations-service/src/Application/Sessions/EventHandlers/OutboxDomainEventDispatcher.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-39 (HU-29 - Envío de evidencias por parte del equipo)
- DES-70 (PRD - SessionOperations)

Output:
- the live DES-39 status and labels
- confirmation that ADR-0010 re-activated DES-39 (it is not superseded / QR-only)
- the direct build-on seams (HU-34 substrate + HU-21A/33A/07A/07B + outbox)
- the extraction decision: generalize HU-34's inline intake, do NOT rebuild it, do NOT build QR (HU-31) or deep validation (HU-30A)

Do not start planning or implementing yet.
```

---

## 2. Label DES-39 as ready-for-agent

```text
Use the Linear MCP to confirm DES-39 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-39 ticket state and labels.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-39 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm:
- ADR-0010 (Accepted 2026-07-10) re-activated DES-39 as the generic evidence intake; the old QR-only cancellation is superseded
- HU-29 extracts HU-34's shared intake substrate; it does not rebuild the base entity, the skeleton, or the outbox bridge
- HU-29 does not build the QR form (HU-31/DES-42) or the deeper EvidenceValidationPolicy (HU-30A/DES-40)
- the trivia path must keep raising AnswerRegistered; HU-29 adds the umbrella EvidenceSubmissionRegistered additively
- RabbitMQ reuses the existing MassTransit EF-Core transactional outbox (ADR-0017 amended)

Output the confirmed HU id, title, acceptance criteria, labels, and the extraction-scope confirmation before planning the slice.
```

In the remaining examples below, `HU-29` and `DES-39` are the resolved values for this slice.
`DES-70` is the shared PRD reference for `session-operations-service`.

---

## 4. Start the slice

```text
Prepare the HU-29 slice on branch feature/hu-29-evidence-submission-intake.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend `session-operations-service` only.

The pre-resolved orient at the top of this document lists what existing code has already landed and
what HU-29 adds. Do not re-read the PRD for scoping unless you need to resolve a precise
implementation detail.

Move DES-39 to In Progress if the team process requires it, and output the exact scope, branch name,
base branch, and touched surfaces. Note explicitly that HU-29 adds NO new participant endpoint
(EvidenceSubmission is abstract; concrete-form endpoints are trivia = Done, QR = HU-31), and that its
only outward-new contract is the async EvidenceSubmissionRegistered RabbitMQ message.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-29 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu29-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; a unit test proves the shared registration core registers a base EvidenceSubmission in Pending and raises EvidenceSubmissionRegisteredEvent on success only
- generic intake rejections fire: session not admitting reception, unknown team, missing active substage
- ALL pre-existing HU-34 trivia domain tests remain green (first-write-wins, late/duplicate rejection, AnswerRegisteredEvent on success) — no behavior regression
- no QR/target logic introduced
- no mandated pattern in this phase (Facade + Chain land in X.2); do not label the core "Template Method"

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-29)

Ref: HU-29
Ref: DES-39
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-29 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu29-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- clean build passes; tests cover the generic chain order + short-circuit, the facade orchestration, and the publisher mapping
- Facade verified: EvidenceIntakeFacade is a discrete Sessions/Common/ class (shared ≥2 consumers, ADR-0013) that orchestrates chain → domain core → persist; no ad-hoc intake logic in the command handler
- Chain of Responsibility verified: generic intake links run runtime-participation → session-admits-reception → active-substage-present, short-circuit on first failure; the trivia chain composes these shared links, not a duplicate
- the trivia command delegates shared intake to the facade with NO change to its participant response / no-leak guarantee (existing SubmitTriviaAnswer tests still pass)
- EvidenceSubmissionRegisteredIntegrationEvent publishes through IPublishEndpoint off the domain fact and is wired via OutboxDomainEventDispatcher (pre-commit outbox insert), not a post-commit path

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-29)

Ref: HU-29
Ref: DES-39
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-29 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu29-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- infrastructure build passes
- NO new migration unless `ef migrations add` reports a real model diff — run the check, assert Pending is string-compatible with the existing validation_state column, and record the outcome (do not fabricate an empty migration)
- the base-evidence row round-trips with ValidationState = Pending
- an integration test proves EvidenceSubmissionRegistered is inserted into the transactional outbox atomically with the business write and drains through the existing RabbitMQ registration — no second publisher stack or exchange bootstrap

Do not touch Api.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-29)

Ref: HU-29
Ref: DES-39
Ref: DES-70
```

---

## 8. Backend phase X.4 — Api layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-29 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu29-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- NO new participant endpoint (EvidenceSubmission is abstract; concrete-form endpoints are trivia = Done, QR = HU-31) — unless Stop 1 explicitly opted into a generic endpoint, in which case scope it separately
- an endpoint/messaging integration test proves the existing trivia submission path publishes EvidenceSubmissionRegistered end-to-end after transactional success
- a session-not-admitting-reception submission returns consistent RFC 7807 ProblemDetails through the existing handler
- service coverage passes the repo gate (ADR-0005)

Do not touch frontend or mobile.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-29)

Ref: HU-29
Ref: DES-39
Ref: DES-70
```

---

## 8.5. Docker rebuild

```text
Rebuild and restart the backend runtime for HU-29:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Then smoke the umbrella intake through the existing trivia submission path via the gateway:
- a first valid participant answer -> accepted response (unchanged HU-34 behavior)
- EvidenceSubmissionRegistered is observable on the RabbitMQ exchange after transactional success
- a submission while the session does not admit reception (Paused/Finished/Cancelled) -> RFC 7807 rejection
- confirm AnswerRegistered is still published (the trivia specialized fact is not removed)
```

---

## 9. Downstream consumer contract hand-off

> HU-29 adds **no** client-facing surface — the umbrella is an internal substrate plus an async
> event consumed by *backend* services (audit/history, notification, secondary recalc/projection),
> not a mobile/web client. So there is no frontend plan for this slice (the same posture the HU-34
> prompt took when its client work split to separate tickets). Instead, record the async contract
> for the downstream consumers.

```text
Do not implement any client (mobile/web) code as part of DES-39 — HU-29 has no client-facing contract.

Instead, record the verified async contract that downstream backend consumers must consume:

- EvidenceSubmissionRegistered integration-event shape (EntityName + fields: liveSessionId, teamId, evidenceSubmissionId, activeSubstageId, submissionType, submittedAt, validationState)
- the exchange / routing convention it publishes on (the existing session-operations service-owned exchange)
- the transactional-outbox guarantee (published only after the business write commits; at-least-once delivery)
- confirmation that the trivia specialized fact AnswerRegistered remains in force and unchanged
- confirmation that the QR form (HU-31/DES-42) will reuse this substrate and additionally raise TargetResolved

Then hand off the contract to the correct downstream consumers:

- session-event history / audit (HU-40 / DES-56 lineage)
- notification and secondary recalc/projection consumers in ScoringMonitoring

Output:
- the verified async contract table
- the explicit note that no client endpoint/UI is in scope for HU-29
- any open questions the substrate leaves for HU-31 (QR) and HU-30A (deep validation)
```

---

## 9b. Optional follow-up execution

```text
If the team explicitly chooses to continue after DES-39, start a new ticket-scoped session for the
first consumer of this substrate — HU-31 (DES-42, QR TreasureEvidenceSubmission + TargetResolved) —
or HU-30A (DES-40, EvidenceValidationPolicy deep context validation).

Do not continue that work under the DES-39 scope or branch, and do not re-open the extracted
trivia path.
```

---

## 10. Close-out

```text
Before opening the PR, confirm all DES-39 acceptance criteria are satisfied:
- a participant can register an EvidenceSubmission tied to the active valid substage (mission node in treasure hunt; active question in trivia)
- each evidence is associated with exactly one team, one session, and the originating active substage
- the system blocks submission when the session does not admit reception (paused, finished, cancelled)
- the evidence is recorded with the information needed for later validation/traceability (initial validation state pending)
- after transactional success, EvidenceSubmissionRegistered is published to RabbitMQ for asynchronous consumption, without the main flow depending on RabbitMQ

Also confirm the scope boundary:
- HU-34's trivia path behavior is unchanged (all existing tests green; AnswerRegistered still published)
- no QR form (HU-31) or deep EvidenceValidationPolicy (HU-30A) was built
- no new client endpoint/UI was added

Then open the PR:

gh pr create \
  --base develop \
  --head feature/hu-29-evidence-submission-intake \
  --title "feat(session-operations): HU-29 common evidence-submission intake" \
  --body "Implements HU-29 / DES-39: extracts the shared EvidenceSubmission intake substrate from HU-34 into a discrete EvidenceIntakeFacade + a generic EvidenceIntakeValidation Chain of Responsibility, adds the canonical EvidenceSubmissionRegistered domain event and its transactional-outbox RabbitMQ bridge, and adds Pending to EvidenceValidationState. The trivia submission path now demonstrates the EvidenceSubmissionRegistered workflow end-to-end while keeping its AnswerRegistered fact and behavior unchanged. Re-activated per ADR-0010; the old QR-only cancellation is superseded. QR form (HU-31/DES-42) and deep EvidenceValidationPolicy (HU-30A/DES-40) remain out of scope."
```

---

## Rationale

- **Why this is an extraction, not a greenfield feature:** HU-34 shipped the `EvidenceSubmission`
  base and a deliberately-extractable trivia intake before this umbrella HU (an intentional ordering
  recorded in `hu34-context.md:86,111` and `workflow_refactor.md`). The canonical move is to
  generalize that substrate, not to re-model evidence from scratch. Every refactored span's gate is
  "HU-34 behavior unchanged."
- **Open scope question surfaced for Stop 1 — does HU-29 add a participant endpoint?** The default
  interpretation here is **no**: `EvidenceSubmission` is an *abstract* umbrella, so there is no
  generic evidence to POST — every real submission is a concrete form (trivia = Done, QR = HU-31).
  Under this reading HU-29's outward-new contract is purely the async `EvidenceSubmissionRegistered`
  event, and its api phase is verification + coverage. The alternative (add a generic intake
  endpoint, or defer the live `EvidenceSubmissionRegistered` raise entirely to HU-31) is a real
  option the PRD does not settle; it is flagged for the human at Stop 1. If the human wants the
  generic endpoint, X.4 grows additively.
- **Why the trivia path raises two facts after this HU:** ADR-0010:38-39 fixes that the QR flow has
  two ordered facts (`EvidenceSubmissionRegistered` then `TargetResolved`). To make HU-29 *the one
  demonstrated end-to-end RabbitMQ workflow* (`required_patterns_matrix.md:182,197`) with a live
  raiser today — before the QR form exists — the shared registration core raises the umbrella
  `EvidenceSubmissionRegistered` on every registration, so the existing trivia path emits it
  additively alongside its specialized `AnswerRegistered` (scoring consumer). This is additive and
  canon-consistent, not a replacement.
- **Why the Facade is a discrete class (not handler-inlined):** ADR-0013 inlines *single-consumer*
  mandated Facades into their handler, but this Facade is shared by ≥2 consumers (trivia now, QR in
  HU-31), which is the ADR's explicit exception — it stays a discrete `Sessions/Common/` class,
  mirroring `SessionTeamAssociationFacade` / `TriviaRoundOrchestratorFacade`.
- **Why the Chain is only the generic intake-admission links:** HU-30 owns the deeper pre-acceptance
  context validators (`EvidenceValidationPolicy`) and HU-31 owns QR target validation
  (`TargetResolutionPolicy`) — `required_patterns_matrix.md:123-124`. Pulling those forward would
  trespass sibling tickets. HU-29's chain is the reusable admission substrate they compose onto.
- **Why RabbitMQ uses the outbox as-is:** ADR-0017 (amended 2026-07-12) replaced the post-commit,
  five-second-bounded, swallow-on-failure publish with a MassTransit EF-Core transactional bus
  outbox. Publish is a pre-commit `OutboxMessage` insert atomic with the business write; delivery is
  async. HU-29 wires the new event through the existing `OutboxDomainEventDispatcher`, adding no new
  transport infrastructure.
- **Why there is no frontend step:** HU-29 produces no client contract — its consumers are backend
  services. Forcing a frontend plan would invent scope (generator Constraint 2). Step 9 is therefore
  a downstream-consumer contract hand-off, mirroring the HU-34 prompt's contract-handoff posture.
- **Canon-vs-code delta carried into X.1:** `EvidenceValidationState` is canonically
  pending/accepted/rejected (`CONTEXT.md:101-102`; PRD AC requires initial `pending`), but the
  as-built enum from HU-34 is only `{Accepted, Rejected}` (trivia resolves instantly). HU-29 adds
  `Pending`; it is string-persisted, so it is schema-compatible (verify in X.3, no assumed migration).
