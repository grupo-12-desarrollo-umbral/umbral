# Prompt Example — HU-33B Round-close & final-results async publication (Feature Slice)

Concrete prompt sequence for driving DES-45 (HU-33B) through a full slice on `feature/hu-33b-trivia-round-close-results-publication`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-45 slice:** HU-33B is the **async-publication sibling** of the already-**Done** HU-33A (DES-78). HU-33A landed the trivia runtime (question close, substage advancement, `SessionCompletion → Finished`) and the SignalR broadcasts. HU-33B adds the **one thing HU-33A deferred**: publishing the round-close and final-results **facts to RabbitMQ** after transactional success, for asynchronous history/consolidation, **without the runtime flow depending on RabbitMQ** (AC #6). It is a **producer bootstrap** — the RabbitMQ broker already runs in `docker-compose.yml`, but **no .NET service publishes to it yet**; HU-33B wires the first publisher. It computes **no** puntaje/ranking — that is `ScoringMonitoring` (HU-37A/37B/39B), and the pre-canon AC #1 is read through canon (D-1). Governing inputs: **ADR-0005** (winner emitted, not computed; reuse existing events) + **PRD DES-70 `:271`** (score/ranking/projections out of scope) + the `rabbitmq-events-dotnet` skill.

> **✅ The four decisions (D-1…D-4) are RESOLVED and committed to scope (2026-07-08)** by the canon authority chain (canon docs / ADR-0005 > tracker AC > existing code). Carry them forward as fixed scope, not a blocking gate. Step 5 (X.1) may start once Stop 1 confirms the slice is grabbed.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. Do not ask for backend and frontend implementation in the same phase prompt. **HU-33B has no frontend slice** (the live UI already renders close/finish over SignalR from HU-33A) — Steps 9/9b are no-op stubs.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- session-ops **publishes facts, computes no score/ranking** — `QuestionClosed` and `SessionResultsFinalized` are emitted; puntaje/ranking is derived downstream by `ScoringMonitoring` (HU-37A/37B/39B). AC #1 "calcula puntaje y ranking" is read through canon (PRD `:271` out-of-scope; ADR-0005 emitted-not-computed) — **D-1**
- the publishable facts **reuse the existing domain events** `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)` — **no new `Domain/Events/*` type** is added (ADR-0005) — **D-2**
- the RabbitMQ publish happens **after transactional success** (in the `SaveChanges` domain-event dispatch) and is **best-effort**: broker failures are logged and swallowed so the runtime flow never depends on RabbitMQ (AC #6) — **D-3**
- **no new REST endpoint**; X.4 is publish/broadcast-centric — the existing SignalR broadcasts (HU-33A/21A) are verified, not re-added; "resultados finales" is the existing `Finished` state — **D-4**
- the three mandated patterns are honored: `State` (facts raised only on legitimate transitions), `Facade` (`TriviaRoundOrchestratorFacade`/`TransitionSessionStateFacade` stay the single fact-raising entry point; publish handlers bridge to RabbitMQ), `Strategy` (**reused** close-vs-advance seam — the score/ranking Strategy proper is downstream, not manufactured in session-ops; ADR-0012 genuine-vs-ceremony)
- transport: **RabbitMQ producer** (durable topic exchange, persistent + confirmed messages, stable routing keys) is the new hard gate; **SignalR** close/finish broadcasts already exist and are verified

**These four decisions are RESOLVED and committed to scope** (2026-07-08; rationale below) — carry them forward as fixed scope:
- **D-1 — RESOLVED:** session-ops computes no puntaje/ranking; it emits the facts, scoring derives them (PRD `:271`, `ddd_solution_model.md:177`, scoring-monitoring `CONTEXT.md:3`, ADR-0005, `CONTEXT.md:195-197`).
- **D-2 — RESOLVED:** reuse `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)`; add no new domain event (ADR-0005).
- **D-3 — RESOLVED:** best-effort post-commit publish; broker failures logged + swallowed **and the publish is non-blocking on the broker (bounded confirm timeout or background-channel offload — a slow broker must not stall close/finish)**; no outbox this slice.
- **D-4 — RESOLVED:** no new REST endpoint; verify the existing SignalR broadcasts; add the RabbitMQ producer + contract.

---

## Required design patterns

HU-33 is the only sprint area mandating three patterns (`trivia_sprint_required_patterns_matrix.md:75`, transport `SignalR + RabbitMQ`). HU-33A **realized** all three; HU-33B **reuses the seams** and adds the RabbitMQ transport.

- **`Facade` (mandated, X.2 Application)** — `trivia_sprint_required_patterns_matrix.md:75`; `CONTEXT.md:209-211` (the Facade "triggers outbound event publication"). `TriviaRoundOrchestratorFacade` (question close) and the `SessionCompletion → Finished` path stay the single fact-raising entry point; MediatR **publish handlers** bridge those facts to RabbitMQ — no publish scattered across worker/endpoint/repository.
- **`State` (mandated, X.1 Domain)** — `trivia_sprint_required_patterns_matrix.md:75`; `CONTEXT.md:213-215`. The facts are raised only on legitimate transitions: `QuestionClosedEvent` on a real close; `SessionStateChangedEvent(→Finished)` only via `SessionCompletion` (never operator-forced). Reused from HU-33A/21A; no new state type.
- **`Strategy` (mandated, reused seam, X.1)** — `trivia_sprint_required_patterns_matrix.md:75`; ADR-0012. The score/ranking Strategy the matrix cites lives **downstream** in `ScoringMonitoring` (HU-37A/37B/39B); session-ops reuses the existing `IQuestionActivationStrategy` close-vs-advance seam that selects which fact is published. **Do not manufacture a score Strategy in session-ops** (D-1; ADR-0012 genuine-vs-ceremony).
- **Transport: SignalR + RabbitMQ (mandated)** — `trivia_sprint_required_patterns_matrix.md:29,30,75`. SignalR close/finish broadcasts already exist (HU-33A/21A) — verified in X.4. **RabbitMQ producer** is the new gate (X.3 + X.4 smoke).

> Applies-where note (no new gate): HU-33B adds no new protected endpoint (D-4); it is publish/broadcast-centric. It is not in the applies-where `Proxy` set — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-07-08)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-45 (HU-33B) is **Todo**, `svc:session-operations-service`, feature flow. Its build-on predecessors are all Done/merged:

- **DES-78 (HU-33A) — Done (the direct seam).** `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` closes the active question (raising `QuestionClosedEvent`), broadcasts `QuestionClosed`, advances substages, and on the final substage routes through `SessionCompletion → MoveTo(Finished)` (raising `SessionStateChangedEvent(→Finished)`). **These are exactly the facts HU-33B publishes.** Domain events dispatch on `SaveChanges` via `DispatchDomainEventsInterceptor`; MediatR auto-registers `INotificationHandler<>`s.
- **DES-76 (HU-21A) — Done.** `SessionStateChangedEvent` + `TransitionSessionStateFacade` + `SessionStateChangedNotificationHandler` (domain event → SignalR broadcast) — the exact **mirror** for HU-33B's publish handlers.
- **DES-77 (HU-22) — Done.** `AuthoritativeSessionTimerWorker` drives `CloseAndAdvanceAsync` on timer expiry — the trigger producing each `QuestionClosedEvent`. HU-33B does not touch it.

Landed-untouched: DES-22/75 (HU-15/16 snapshot), DES-25/26/27 (HU-18/19/20), DES-11/12 (HU-07A/07B). **Not a predecessor:** DES-44 (HU-33A cycle-1, Canceled — superseded by DES-78); **DES-51 (HU-37A)** is the downstream **consumer** in `scoring-monitoring-service` (Todo, a different service — not read here). No same-service In Progress predecessor → branch base is `develop`.

### What HU-33B adds (per DES-45, ADR-0005, PRD DES-70, and the trivia patterns matrix)

| Concern | New work |
|---|---|
| Integration-event port | `IIntegrationEventPublisher.PublishAsync(evt, ct)` (`Application/Common/Interfaces/`). |
| Integration-event contracts | `QuestionClosedIntegrationEvent` + `SessionResultsFinalizedIntegrationEvent` (history-correlation only; **no** score — D-1). |
| Publish handlers | MediatR `INotificationHandler<QuestionClosedEvent>` and `<SessionStateChangedEvent>` (Finished-only) mapping fact → integration event → port. Mirror `SessionStateChangedNotificationHandler`. |
| RabbitMQ producer (bootstrap) | `RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher` + `RabbitMqOptions` + long-lived connection; durable topic exchange, persistent + confirmed messages, stable routing keys. First AMQP publisher in the backend. |
| Resilience | Broker failures logged + swallowed — the runtime flow never faults on RabbitMQ (D-3, AC #6). |
| Config + DI + package | `RabbitMq` appsettings section; register the publisher in `AddInfrastructureServices`; add `RabbitMQ.Client`. |
| Verify (not re-add) | The `QuestionClosed`/`SessionStateChanged(→Finished)` SignalR broadcasts already exist (HU-33A/21A) — X.4 verifies them. |

### Out of scope for this slice (surface at Stop 1, do not build)

- **Score/ranking computation** — `TriviaQuestionScore` summation, `TriviaSubstageWinner`, `SessionRanking` (D-1). Owned by `ScoringMonitoring` (HU-37A/37B/39B). PRD `:271` out-of-scope.
- **A new domain event** — reuse `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)` (D-2, ADR-0005).
- **A transactional outbox** — best-effort at-most-once is sufficient for history/consolidation (D-3); add an outbox only if a consumer needs at-least-once.
- **A RabbitMQ consumer** — HU-33B is the producer; HU-37A consumes.
- **A new REST endpoint / frontend slice** (D-4) — publish/broadcast-centric; the live UI already renders close/finish over SignalR (HU-33A).

### Branch state and prerequisite

`feature/hu-33b-trivia-round-close-results-publication` branches from `develop`. All build-on dependencies (HU-15/16/21A/22/33A) are Done/merged; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) that `QuestionClosedEvent` + `SessionStateChangedEvent` exist and are raised by `LiveSession.CloseActiveQuestion` / `SessionCompletion`; that `SessionStateChangedNotificationHandler` bridges a domain event onto a broadcast (the mirror); that `DispatchDomainEventsInterceptor` dispatches domain events on `SaveChanges`; that the broker runs in `docker-compose.yml:47` (`rabbitmq:3-management`) and **no** csproj references `RabbitMQ.Client` yet. **D-1…D-4 are already resolved (committed scope) — build to them.**

### Linear state (as of 2026-07-08)

- DES-45 (HU-33B): **Todo**, labels: `ready-for-agent`, `svc:session-operations-service`, `Feature` (both required labels present)
- DES-78 (HU-33A), DES-76 (HU-21A), DES-77 (HU-22), DES-22 (HU-15), DES-75 (HU-16): **Done** (the foundation this slice builds on)
- DES-44 (HU-33A cycle-1): **Canceled** — superseded; DES-45's `blockedBy` still names it, substitute DES-78
- DES-51 (HU-37A): **Todo**, `scoring-monitoring-service` — the downstream consumer, not a predecessor

> Linear live state may have changed. Use the Linear MCP to verify DES-45 status/labels if needed, but do not re-fetch PRD scope — read `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`, overlaid by `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md` + `@backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`.

---

## 1. Orient — read service state, PRD, and the async-publication seam

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-08.

```text
Read the following and summarise what is implemented today vs. what HU-33B must add:
- @backend/docs/hu33b-context.md — the pre-resolved HU-33B context (primary), incl. D-1…D-4
- @backend/docs/hu33a-context.md — the runtime HU-33B publishes from (facade, QuestionClosedEvent, SessionCompletion)
- @backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md — winner emitted-not-computed; reuse existing events
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md — US33; :271 score/ranking out of scope
- @backend/services/session-operations-service/CONTEXT.md — §Runtime Authority (:195-197), §SessionCompletion (:185-187), §Facade (:209-211)
- @backend/.claude/skills/rabbitmq-events-dotnet/SKILL.md — the publisher pattern (first AMQP producer in the backend)

Then grep the existing session-operations source to confirm:
- QuestionClosedEvent + SessionStateChangedEvent exist and are raised by LiveSession.CloseActiveQuestion / SessionCompletion
- SessionStateChangedNotificationHandler bridges a domain event onto a broadcast (the mirror for the publish handlers)
- DispatchDomainEventsInterceptor dispatches domain events on SaveChanges (the post-commit publish point)
- the rabbitmq broker runs in docker-compose.yml:47 and NO csproj references RabbitMQ.Client yet

Then use the Linear MCP to fetch the current live state and labels of DES-45.

Output: what is already landed (the runtime + SignalR broadcasts + the two domain facts), what HU-33B adds (the IIntegrationEventPublisher port + contracts + publish handlers + RabbitMQ producer), and a crisp statement of D-1…D-4.
Do not start planning or implementing yet.
```

---

## 2. Label DES-45 as ready-for-agent

```text
Use the Linear MCP to confirm DES-45 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-45 ticket state and labels (ready-for-agent, svc:session-operations-service, Feature).
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-45 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
(:271 puts score/ranking/projections out of scope), overlaid by ADR-0005.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- session-ops publishes facts, computes no puntaje/ranking (D-1)
- reuse QuestionClosedEvent + SessionStateChangedEvent(->Finished); no new domain event (D-2)
- best-effort post-commit publish; broker failures logged + swallowed; runtime never depends on RabbitMQ (D-3)
- no new REST endpoint; verify the existing SignalR broadcasts; add the RabbitMQ producer + contract (D-4)
- State + Facade realized; Strategy reused (score Strategy is downstream); RabbitMQ producer is the new hard gate

Then acknowledge D-1…D-4 as already resolved and committed to scope (see the Rationale) — carry them forward as fixed scope, not a blocking gate.

Output the confirmed HU id, title, acceptance criteria, labels, the guard confirmation, and the D-1…D-4 resolutions before planning the slice.
```

In the remaining steps, `HU-33B` and `DES-45` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by ADR-0005 + the realignment document).

---

## 4. Start the slice

```text
Prepare the round-close & final-results async-publication slice on branch feature/hu-33b-trivia-round-close-results-publication.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service only (no frontend — the live UI already renders close/finish over SignalR from HU-33A).

The pre-resolved orient at the top of this document lists what is already landed (the HU-33A runtime + SignalR + the two
domain facts) and what HU-33B adds (the RabbitMQ producer + the publish path). Do not re-read the PRD for scoping unless you need a precise detail.

HU-33B is the async-publication sibling of the Done HU-33A: publish QuestionClosed and SessionResultsFinalized facts to
RabbitMQ after transactional success, for history/consolidation, WITHOUT the runtime depending on RabbitMQ (best-effort,
logged+swallowed). Compute NO puntaje/ranking (that is ScoringMonitoring — HU-37A/37B/39B; AC #1 read through canon). Reuse
the existing domain events; add NO new domain event. No new REST endpoint. The broker already runs in docker-compose; wire
the first .NET publisher to it.

D-1…D-4 are already resolved (committed scope) — build to them. Move DES-45 to In Progress and output the exact scope,
branch name, base branch (develop), and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-33B in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu33b-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open). Apply the D-1…D-4 resolutions from Stop 1.

Gate:
- Domain build passes; a unit test locks that QuestionClosedEvent is raised on a real CloseActiveQuestion and SessionStateChangedEvent(->Finished) only via SessionCompletion, each carrying the history-correlation fields
- no puntaje/ranking is computed in the domain (D-1)
- no new Domain/Events/* type is added — the two existing facts are the publishable set (D-2)
- State verified: the facts are gated by the lifecycle model, not ad-hoc conditionals

Do not compute score/ranking (HU-37A) or add a RabbitMQ dependency in Domain. Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-33B)

Ref: HU-33B
Ref: DES-45
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-33B in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu33b-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; tests prove a QuestionClosedEvent publishes exactly one QuestionClosedIntegrationEvent through a fake IIntegrationEventPublisher, and a SessionStateChangedEvent(->Finished) publishes exactly one SessionResultsFinalizedIntegrationEvent
- a non-Finished state change publishes nothing; the integration-event contracts carry no score/ranking (D-1)
- a throwing fake publisher does not propagate out of the handler (D-3)
- Facade verified: publication bridges off the orchestrator's facts (TriviaRoundOrchestratorFacade / TransitionSessionStateFacade stay the single fact-raising entry point), not scattered across worker/endpoint/repository

Do not add the RabbitMQ implementation here (Application depends only on the IIntegrationEventPublisher port). Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-33B)

Ref: HU-33B
Ref: DES-45
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-33B in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu33b-context.md** (your spec — do not
re-read the canon or re-inspect the tree). Ground the publisher on the
@backend/.claude/skills/rabbitmq-events-dotnet/ skill (SKILL.md §§1-4) — this is
the first AMQP publisher in the backend, so there is no in-repo mirror.

Gate:
- Infrastructure build passes; RabbitMQ.Client is added to Infrastructure.csproj
- RabbitMqIntegrationEventPublisher declares a DURABLE topic exchange and publishes PERSISTENT, publisher-CONFIRMED messages on stable routing keys (session.question.closed, session.results.finalized) over a LONG-LIVED connection (never per-publish)
- broker/publish failures are logged and swallowed — the publisher never throws into the dispatch, **and a slow/unresponsive broker adds no latency: the confirm wait is bounded or offloaded to a background channel, never an unbounded `WaitForConfirms` on the runtime thread** (D-3)
- an integration test proves a published integration event is received on a bound queue (Testcontainers RabbitMQ); if no Testcontainers RabbitMQ is available in the test stack, instead prove a broker-down publish logs and does not throw (do not weaken the D-3 resilience gate)
- registered in AddInfrastructureServices; RabbitMq appsettings section points at host rabbitmq, port 5672

Do not touch Api or frontend. Do not add a consumer (HU-37A consumes).
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-33B)

Ref: HU-33B
Ref: DES-45
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-33B in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu33b-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- an end-to-end test proves closing a question and finishing a session publish the two integration events on the exchange (reuse the X.3 harness), AND the existing SignalR QuestionClosed / SessionStateChanged(->Finished) broadcasts still reach live-session:{id}; authorization enforced on the reads
- the published RabbitMQ contract (exchange, routing keys, message schemas) is documented for the HU-37A/history consumer
- service coverage reaches the repo gate target (ADR-0005 coverlet gate)

Add NO new REST endpoint (publish/broadcast-centric — D-4). Do not add a consumer. Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-33B)

Ref: HU-33B
Ref: DES-45
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway rabbitmq

Smoke the async publication through the gateway on an ALL-TRIVIA MULTI-SUBSTAGE mission (reuse the HU-33A smoke path;
create the mission via the seeders / mission-design if needed). There is NO publish endpoint — drive it by the timer and
observe the broker:
- open the RabbitMQ management UI at http://localhost:15672 (guest/guest); bind a temporary queue to the umbral.session-operations exchange on session.# (or run a throwaway consumer)
- POST /api/sessions with a mission carrying >=2 trivia substages -> 201; then PATCH .../state to Preparing, then Active
- let the question timers expire (or observe the worker): each question close publishes a session.question.closed message
- after the final substage's last question closes, the session reaches Finished (SessionCompletion) and a session.results.finalized message is published
- confirm the messages carry ONLY history-correlation fields (session id, question index / finished-at) — NO score/ranking
- stop the rabbitmq container mid-round and confirm the session still closes questions and finishes normally (best-effort publish; runtime not dependent — D-3), with the publish failures logged
- confirm a SignalR client on live-session:{id} still receives QuestionClosed and the SessionStateChanged(-> Finished) signal

Output:
- container status
- smoke results (published messages observed on the exchange + the broker-down resilience check + the SignalR broadcasts)
- confirmation the runtime flow is unaffected when RabbitMQ is down (D-3)
```

---

## 9. Frontend slice

```text
No frontend slice for HU-33B.

HU-33B is a backend async-publication slice: it adds a RabbitMQ producer for history/consolidation and changes no
user-facing contract. The live participant/operator UI already renders question close and session completion over SignalR
(QuestionClosed / SessionStateChanged(-> Finished)) from HU-33A/HU-21A — those broadcasts are unchanged here. There is no
new REST or SignalR contract, and no read surface for "resultados finales" in this slice (the final ranking/results read is
downstream — HU-39B ranking, HU-36B post-close review).

Do not generate a frontend plan and do not modify frontend code for HU-33B.
```

---

## 9b. Implement the frontend plan

```text
Not applicable — HU-33B has no frontend slice (see Step 9). Skip.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-45 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- session-ops publishes QuestionClosed + SessionResultsFinalized facts and computes no puntaje/ranking (D-1)
- the facts reuse the existing domain events; no new domain event (D-2)
- the publish is best-effort post-commit; a broker-down round still closes questions and finishes (D-3)
- no new REST endpoint; the existing SignalR broadcasts verified; RabbitMQ producer + contract added (D-4)
- State + Facade realized; Strategy reused (score Strategy downstream); RabbitMQ producer gate + ADR-0005 coverage green

Run final backend verification required by the repo instructions.
Summarise:
- commits created
- the producer added (IIntegrationEventPublisher port, the two integration-event contracts + publish handlers, RabbitMqIntegrationEventPublisher on a durable topic exchange) and the documented contract for HU-37A
- tests and gates run (incl. the D-3 resilience check and ADR-0005 coverage)
- confirmation the runtime flow is independent of RabbitMQ

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-33b-trivia-round-close-results-publication \
  --title "feat(session-operations): round-close & final-results async publication (HU-33B)" \
  --body "Adds DES-45/HU-33B: session-operations publishes the round-close and final-results facts to RabbitMQ after transactional success, for asynchronous history/consolidation, without the runtime flow depending on the broker. Introduces the first AMQP publisher in the backend: an IIntegrationEventPublisher port, QuestionClosedIntegrationEvent + SessionResultsFinalizedIntegrationEvent contracts, MediatR publish handlers that bridge the existing QuestionClosedEvent and SessionStateChangedEvent(->Finished) domain facts to the port, and a RabbitMqIntegrationEventPublisher that publishes persistent, confirmed messages to a durable topic exchange (routing keys session.question.closed / session.results.finalized) over a long-lived connection. Broker failures are logged and swallowed so a down or slow broker never faults the runtime (best-effort at-most-once; no outbox this slice). Reuses HU-33A's runtime and Facade/State seams and the existing SignalR broadcasts (verified, not re-added). Computes NO puntaje/ranking — the winner/ranking is emitted, not computed (ADR-0005); scoring is derived downstream by ScoringMonitoring (HU-37A/37B/39B), and the pre-canon AC #1 is read through canon (PRD DES-70 :271 out-of-scope). No new REST endpoint; no frontend change. The published contract is documented for the HU-37A consumer."
```

---

## Rationale

DES-45's acceptance (on question close: results/score; after the last question: final results; broadcast live; and — AC #6 — publish the close and final results to RabbitMQ for asynchronous history/consolidation without the main flow depending on RabbitMQ) is read **through the post-rewrite canon**. The mission-runtime rewrite made trivia a `SubstagePlayMode` inside a mission and moved scoring/ranking to `ScoringMonitoring`; HU-33A (DES-78, Done) already landed the trivia runtime (question close, substage advancement, `SessionCompletion → Finished`) and the SignalR broadcasts. So the genuine, non-duplicated work in HU-33B is **AC #6**: publish the round-close and final-results **facts** to RabbitMQ after transactional success. The RabbitMQ broker already runs in `docker-compose.yml` but no .NET service publishes to it — HU-33B is the sprint's designated producer bootstrap (`trivia_sprint_required_patterns_matrix.md:30,33,75`).

The mandated patterns are honored, not invented: `State` (the facts are raised only on legitimate lifecycle transitions — a real close, `SessionCompletion → Finished` — reused from HU-33A/21A), `Facade` (`TriviaRoundOrchestratorFacade` / `TransitionSessionStateFacade` stay the single fact-raising entry point per `CONTEXT.md:209-211` "the Facade triggers outbound event publication"; MediatR publish handlers bridge the facts to RabbitMQ). `Strategy` is the one nuance: the "score/ranking variation" the matrix cites is realized **downstream** in `ScoringMonitoring` (HU-37A/37B/39B), so session-ops **reuses** the existing `IQuestionActivationStrategy` close-vs-advance seam rather than manufacturing a score Strategy that would both be ceremony (ADR-0012 genuine-vs-ceremony) and cross the Runtime-Authority boundary. RabbitMQ is the new mandated transport; SignalR is already satisfied by HU-33A/21A.

Four decisions were surfaced rather than guessed (`generator-agent.md` constraint 3) and **resolved 2026-07-08** against the canon authority chain (canon docs / ADR-0005 > tracker AC > existing code) — now committed scope:

- **D-1 — RESOLVED: session-ops computes no puntaje/ranking; AC #1 is read through canon.** PRD DES-70 `:271` puts "Score calculation, ranking ownership, penalties, and derived projections" out of scope for this service; `CONTEXT.md:195-197` (Runtime Authority) and ADR-0005 make the winner/ranking emitted, not computed. HU-33B publishes the facts; `ScoringMonitoring` (HU-37A ledger, HU-37B/39B ranking) derives puntaje/ranking. The ticket's pre-rewrite "calcula puntaje y ranking" wording is the same drift the realignment overlay records for sibling tickets.
- **D-2 — RESOLVED: reuse existing domain events; add none.** ADR-0005 (Consequences) defers a dedicated completion event "until a consumer needs a signal these two cannot supply" — `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)` (plus the existing `SubstageAdvancedEvent` stream) supply the async-history signal. "SessionResultsFinalized" is an Application integration-event **contract** mapped from the state-change fact, not a new `Domain/Events/*` type.
- **D-3 — RESOLVED: best-effort post-commit publish; runtime never depends on RabbitMQ.** Domain events dispatch inside `SaveChanges` (`DispatchDomainEventsInterceptor`) — after the entity write, i.e. "tras el éxito transaccional". The publisher logs and swallows broker failures **and keeps the publish non-blocking on the broker (bounded confirm timeout or background-channel offload — never an unbounded `WaitForConfirms` on the runtime thread)** so a down **or slow** broker cannot fault or stall the dispatch or the runtime flow (AC #6). No transactional outbox this slice — best-effort at-most-once suffices for history/consolidation; an outbox is the upgrade path if a consumer needs at-least-once.
- **D-4 — RESOLVED: no new REST endpoint; publish/broadcast-centric X.4 (mirrors HU-33A D-3).** "Resultados finales" is the existing `Finished` state and its SignalR broadcast (HU-33A/21A); the final-results read surfaces are downstream (HU-39B ranking, HU-36B review). HU-33B adds the RabbitMQ producer + contract, and X.4 verifies the producer and the already-present SignalR broadcasts. There is no frontend slice — the live UI already renders close/finish over SignalR.

With all four resolved, X.1 may start once Stop 1 confirms the slice is grabbed. Score/ranking computation (HU-37A/37B/39B), a RabbitMQ consumer (HU-37A), and any final-results read surface (HU-39B/HU-36B) remain deliberately out of scope — building them here would cross the service boundary or jump the backlog order.
