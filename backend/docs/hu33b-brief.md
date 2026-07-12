# HU-33B — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Historical brief — not active messaging guidance.** This records the original
> HU-33B hand-rolled RabbitMQ implementation and its decisions at delivery time.
> ADR-0017 supersedes its publisher-boundary and latency wording for new or migrated
> events: Application uses MassTransit's `IPublishEndpoint`, RabbitMQ mechanics stay
> in Infrastructure, and post-commit publishing is bounded-blocking best effort with
> the linked five-second timeout and logged/swallowed failures. Do not restore
> `IIntegrationEventPublisher` or `RabbitMqIntegrationEventPublisher` from this brief.

> **Nature of this HU:** feature build — the **async-publication sibling** of the Done HU-33A
> (DES-78). HU-33A already landed the trivia runtime (question close, substage advancement,
> `SessionCompletion → Finished`) and the SignalR broadcasts. HU-33B adds the one thing HU-33A
> deferred: publish the round-close and final-results **facts to RabbitMQ** after transactional
> success, for async history/consolidation, **without the runtime depending on RabbitMQ**. It is a
> **producer bootstrap** — the broker already runs in `docker-compose.yml` but no .NET service
> publishes yet. Never authorize a subagent to compute puntaje/ranking (emitted, not computed —
> `ScoringMonitoring`/HU-37A/37B/39B; D-1), add a new domain event (reuse the existing two — D-2),
> make the runtime depend on the broker (best-effort publish — D-3), or add a REST endpoint /
> frontend change (D-4).
>
> **✅ The four decisions D-1…D-4 are RESOLVED and committed to scope** (2026-07-08) — X.1 may start once Stop 1 confirms the slice is grabbed.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-33B — Cierre automático de preguntas y resultados finales de trivia | DES-45 | DES-70 | session-operations-service | feature/hu-33b-trivia-round-close-results-publication | develop |

Governing inputs: ADR-0005 (winner emitted, not computed; reuse existing events) · PRD DES-70 `:271` (score/ranking out of scope) · the `rabbitmq-events-dotnet` skill (publisher pattern). Predecessor substitution: DES-45's `blockedBy` names the **Canceled DES-44** — the real predecessor is **DES-78 (HU-33A, Done)**. DES-51 (HU-37A) is the downstream **consumer** in another service, not a predecessor.

## Resolved decisions (committed scope — the phases build to these)
- **D-1 — RESOLVED: no puntaje/ranking in session-ops.** Publish the facts; `ScoringMonitoring` (HU-37A/37B/39B) derives score/ranking. AC #1 read through canon (PRD `:271`, `ddd_solution_model.md:177`, scoring-monitoring `CONTEXT.md:3`, ADR-0005, `CONTEXT.md:195-197`).
- **D-2 — RESOLVED: reuse existing domain events.** `QuestionClosedEvent` + `SessionStateChangedEvent(→Finished)`; add **no** new `Domain/Events/*` type (ADR-0005). "SessionResultsFinalized" is an Application contract, not a domain event.
- **D-3 — RESOLVED: best-effort post-commit publish.** Publish in the `SaveChanges` domain-event dispatch; log + swallow broker failures **and keep the publish non-blocking on the broker** (bounded confirm timeout or background-channel offload — a *slow* broker must not stall close/finish, not just a *down* one) so the runtime never depends on RabbitMQ (AC #6). No outbox this slice.
- **D-4 — RESOLVED: no new REST endpoint; no frontend.** Verify the existing SignalR broadcasts (HU-33A/21A); add the RabbitMQ producer + contract. "Resultados finales" is the existing `Finished` state; the results read surface is downstream (HU-39B/HU-36B).

## Required pattern(s) → owning phase
HU-33 is the only sprint area mandating three patterns (`trivia_sprint_required_patterns_matrix.md:75`, transport `SignalR + RabbitMQ`). HU-33A realized all three; HU-33B reuses the seams + adds the RabbitMQ transport.
- **`Facade`** (phase X.2) — `:75`, `CONTEXT.md:209-211`: `TriviaRoundOrchestratorFacade` / `TransitionSessionStateFacade` stay the single fact-raising entry point; MediatR publish handlers bridge the facts to RabbitMQ — obligation: no publish scattered across worker/endpoint/repository.
- **`State`** (phase X.1) — `:75`: facts raised only on legitimate transitions (real close; `Finished` only via `SessionCompletion`). Reused; no new state type.
- **`Strategy`** (phase X.1, **reused seam**) — `:75`, ADR-0012: the score/ranking Strategy is **downstream** (`ScoringMonitoring`); session-ops reuses the existing `IQuestionActivationStrategy` close-vs-advance seam — do **not** manufacture a score Strategy here (D-1).
- **Transport SignalR + RabbitMQ** (X.3/X.4) — `:29,30,75`: SignalR close/finish broadcasts already exist (verified in X.4); the **RabbitMQ producer** (durable topic exchange, persistent + confirmed messages) is the new hard gate.
- No new `Proxy` gate — HU-33B adds no protected endpoint (D-4); publish/broadcast-centric.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks that `QuestionClosedEvent` is raised on a real close and `SessionStateChangedEvent(→Finished)` only via `SessionCompletion`, each carrying the history-correlation fields; **no puntaje/ranking computed** (D-1); **no new domain event added** (D-2) | `State` (+ `Strategy` reused seam) |
| X.2 Application | App build; tests prove a `QuestionClosedEvent` publishes one `QuestionClosedIntegrationEvent` and a `SessionStateChangedEvent(→Finished)` publishes one `SessionResultsFinalizedIntegrationEvent` through a fake `IIntegrationEventPublisher`; a **non-Finished** change publishes nothing; contracts carry **no** score (D-1); a **throwing** publisher does not propagate (D-3); Facade stays the single fact-raising entry point | `Facade` |
| X.3 Infrastructure | Infra build; `RabbitMQ.Client` added; `RabbitMqIntegrationEventPublisher` declares a **durable topic** exchange, publishes **persistent + confirmed** messages on `session.question.closed`/`session.results.finalized` over a **long-lived** connection; broker failures logged + swallowed **and a slow/unresponsive broker adds no latency to the dispatch (bounded or offloaded confirm, never an unbounded block)** (D-3); integration test receives a published event on a bound queue (Testcontainers RabbitMQ) — or, absent that, a broker-down publish logs + does not throw; registered in `AddInfrastructureServices` | — |
| X.4 Api | End-to-end test: a closed question + a finished session publish the two integration events on the exchange **and** the existing SignalR `QuestionClosed`/`SessionStateChanged(→Finished)` broadcasts still reach `live-session:{id}`; the RabbitMQ contract documented for HU-37A; ADR-0005 coverage. **No new REST endpoint** (D-4) | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-33B)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-33B)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-33B)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-33B)`

Trailer (every phase): `Ref: HU-33B` / `Ref: DES-45` / `Ref: DES-70`

## Acceptance criteria
- each question close and the final-results milestone are published to RabbitMQ as domain-fact integration events after transactional success, for async history/consolidation, **without the runtime flow depending on RabbitMQ** (AC #6)
- the round-close and session-completion facts are broadcast live over SignalR (already landed by HU-33A/21A — verified, unchanged)
- puntaje/ranking is **not** computed in session-ops — the facts are emitted and scoring derives them downstream (AC #1 read through canon — D-1)
- **out of scope (surface, do not build):** score/ranking computation (HU-37A/37B/39B); a new domain event (D-2); a transactional outbox (D-3); a RabbitMQ consumer (HU-37A); any new REST endpoint or frontend change (D-4)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints (D-4). HU-33B adds a RabbitMQ **producer**, not a REST route — smoke it by driving a trivia round and observing the broker. Behavioural change: session-ops now publishes to RabbitMQ; the runtime must be unaffected when the broker is down._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway rabbitmq`), smoke on an **all-trivia, ≥2-substage** mission through the gateway, watching the RabbitMQ management UI (`http://localhost:15672`, guest/guest — bind a temp queue to `umbral.session-operations` on `session.#`):
- `POST /api/sessions` (mission with ≥2 trivia substages) → **201**; then `PATCH …/state` → `Preparing` → `Active`
- let the question timers expire (or observe the worker): each question close publishes a **`session.question.closed`** message
- after the **final** substage's last question closes → session `Finished` (`SessionCompletion`) and a **`session.results.finalized`** message is published
- messages carry **only** history-correlation fields (session id, question index / finished-at) — **no** score/ranking (D-1)
- **stop the `rabbitmq` container mid-round** → the session still closes questions and finishes normally, publish failures logged (D-3)
- a SignalR client on `live-session:{id}` still receives `QuestionClosed` and the `SessionStateChanged(→Finished)` signal
- published contract for the HU-37A/history consumer: exchange `umbral.session-operations` (durable topic); routing keys `session.question.closed` → `QuestionClosedIntegrationEvent`, `session.results.finalized` → `SessionResultsFinalizedIntegrationEvent`

## Frontend slice
**None.** HU-33B is a backend async-publication slice with no user-facing contract change; the live UI already renders question close and session completion over SignalR (HU-33A/HU-21A). Steps 9/9b of `prompt_example_feature_hu33b.md` are no-op stubs — do not generate a frontend plan or modify frontend code.
