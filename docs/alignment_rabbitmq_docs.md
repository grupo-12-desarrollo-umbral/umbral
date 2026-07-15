# RabbitMQ flows ↔ requirements alignment

How UMBRAL's actual MassTransit-over-RabbitMQ flows map to the academic requirements in
[`proyecto_requisitos_documento_clave.md`](./proyecto_requisitos_documento_clave.md). For the technical
wiring detail (contracts, consumers, URN convention, audit findings) see
[`backend/docs/integration-event-wiring-audit.md`](../backend/docs/integration-event-wiring-audit.md).

## The requirement

The enunciado frames RabbitMQ as the decoupling channel for **secondary processes**, in three named
categories:

- **RF-14** — *"La aplicación debe publicar eventos del dominio en RabbitMQ al registrarse evidencias o
  cambios significativos."*
- **RNF-05** — *"Los procesos asíncronos deben desacoplarse mediante RabbitMQ."*
- **§11** — *"Cola RabbitMQ para el desacoplamiento de eventos secundarios, tales como **auditoría,
  recálculo y notificaciones**."*
- **§15 (aceptación)** — *"La publicación y consumo de mensajes por RabbitMQ se demuestra con al menos un
  flujo de negocio relevante."*

So the bar is: publish domain events on evidence/significant changes, decouple **audit / recalculation /
notifications**, and demonstrate at least one full publish→consume business flow.

## What is actually implemented

Only `session-operations-service` and `scoring-monitoring-service` use the bus; both publish through the
EF transactional outbox. (`identity-access-service` and `mission-design-service` use no messaging.)

### ✅ Recálculo de puntaje (RF-10) — fully covered, publish + consume

| Flow | Requirement satisfied |
|---|---|
| `AnswerRegisteredEvent` → scoring → `ScoreEntryRegistered` → recalc ranking | RF-10 "recalcular el puntaje cuando una evidencia sea validada" |
| `TargetResolvedEvent` (QR scan) → scoring → score entry → recalc | RF-10, RF-12 (ranking en tiempo real) |
| operator penalty → `ScoreEntry(Penalty)` → recalc — authorized by `LiveSessionOperatorAssignedEvent` | RF-10 + RF-11 "penalizaciones justificadas" + RB-06 (motivo y momento) |

Strongest evidence for §15: verified live end-to-end (scan→score and penalty→recalc), publish **and**
consume, score keyed on `ReferenceTeamId` with full traceability (RB-07 "el puntaje… no puede quedar sin
trazabilidad de origen").

### ✅ Auditoría / historial (RF-15) — covered, publish + consume

| Flow | Requirement satisfied |
|---|---|
| `EvidenceSubmissionRegistered / Accepted / Rejected` → `EvidenceTrace` consumer → `evidence_trace_entries` | RF-09 "cada envío… registrado", RF-15 "historial… para auditoría", §11 "auditoría", RF-14 "al registrarse evidencias" |

This is §11's *auditoría* category as a real publish→consume flow. Two consumer-side bugs were fixed here
(audit doc, Finding 1): the redelivery **idempotency** fault that dead-lettered 16 messages, and — found
later, while closing that finding's recorded "residual" — a **silent data-loss** bug in the same
`UpsertAsync`. Registration and resolution are independent messages that arrive in either order; the upsert
only ever inserted, so whenever a resolution created the row first, the registration's context
(`SubmittedByParticipantId`, `OriginReference`) was written **nowhere**. The trace row existed and the
queues were clean, so every check reported healthy while the audit trail was quietly incomplete — worth
stating in the memoria, because RF-09's *"cada envío… registrado"* is about the row being **right**, not
merely present. The merge rule now lives in the domain (`EvidenceTraceEntry.MergeFrom`): registration owns
the submission context, resolution owns the terminal state, neither clobbers the other.

### ⚠️ Notificaciones — intentionally NOT over RabbitMQ

§11 lists *notificaciones* as a Rabbit use, but UMBRAL's notifications (ranking / penalty / state pushes to
the UI) travel over **SignalR / WebSockets**, not the bus — which satisfies **RNF-03** (tiempo real sobre
WebSockets) and RF-12/RF-13. This is a defensible tool split, and should be stated explicitly in the
memoria:

> **RabbitMQ** = asynchronous server-to-server decoupling (auditoría, recálculo).
> **WebSockets** = real-time client push (notificaciones, ranking, cambios de estado).

It is the one §11 category not realized as a Rabbit flow — by design, not by omission.

### ✅ RF-14 "cambios significativos" — published **and** consumed into the session history

`SessionStateChangedIntegrationEvent`, `QuestionClosedIntegrationEvent`, and
`SessionResultsFinalizedIntegrationEvent` are published on significant changes **and are now consumed** by
`SessionEventHistoryConsumer` in `scoring-monitoring-service`, which folds all three into the
**`SessionEvent`** history/audit log (`session_events`) that §12 defines for *"historial y auditoría"* —
delivering the *"consolidación del historial"* §7 calls for. This closes both halves of RF-14 and
reinforces RF-15. Idempotency comes from a deterministic `SourceEventKey` per source event, so
MassTransit's at-least-once redelivery cannot double-append a history row.

**Demonstrated live.** Two operator-driven transitions on session `7f614460-539c-4f92-a39a-b28c7f1aef2a`
(`Active→Paused`, then `Paused→Active`) each landed a `session_events` row on the running stack, with the
responsible operator (`444`) carried on the event itself. Together with the `SessionEventHistory` queue
bound to all three exchanges and the publisher-contract → production-consumer path passing against real
RabbitMQ + Postgres in the Testcontainers guardrail suite, this makes the history projection a
**demonstrated** §15 flow — the fourth. (Finding 2 in the audit doc — resolved, R1 + R2 both verified.)

## Summary table

| Requirement | Category | Status |
|---|---|---|
| RF-10 recálculo on validation/penalty | recálculo | ✅ publish + consume, verified live |
| RF-11 penalización justificada | recálculo | ✅ (penalty → score entry → recalc) |
| RF-09 / RF-15 evidence audit trail | auditoría | ✅ publish + consume |
| RF-14 publish on evidence registration | auditoría | ✅ |
| RF-14 publish on significant changes | historial | ✅ publish + consume (`SessionEventHistoryConsumer` → `session_events`), verified live |
| §11 notificaciones | notificaciones | ⚠️ done over WebSockets, not RabbitMQ (design choice) |
| §15 ≥1 demonstrated business flow | — | ✅ **exceeded** — four (scan→score, penalty→recalc, evidence→trace, state-change→history) |

## Takeaways for the memoria técnica

1. The §15 minimum is **exceeded**: four real publish→consume business flows, verified live.
2. Two of §11's three categories — **auditoría** (evidence trace) and **recálculo** (scoring) — are
   genuinely wired over RabbitMQ; **notificaciones** is deliberately on WebSockets (state that split
   explicitly).
3. The former top improvement — wiring **one consumer** for the three publish-only session events into a
   `SessionEvent` history — is **done**: `SessionEventHistoryConsumer` closes Finding 2 and directly
   satisfies RF-14 + RF-15 + the §12 `SessionEvent` concept. **Every** exchange the backend publishes now
   has a production consumer; there are no publish-only events left.
4. That history projection is now the **fourth demonstrated §15 flow**: an operator state transition on the
   running stack produced its `session_events` row (the R2 check), on top of the contracts, consumer,
   migration, live queue binding, and Testcontainers coverage backing it.
5. Cross-service wiring is now guarded by a reusable Testcontainers harness that publishes each
   **publisher's own contract** and asserts the **production consumer's** effect, covering all six
   cross-service pairs — the regression class behind the two prior incidents.
6. **The guard is not automated yet.** The repo has no CI pipeline, so the harness only fires when someone
   runs `make -C backend test SVC=<service>` by hand. A GitHub Actions workflow is **drafted** at
   `.github/workflows/backend-tests.yml` (matrix over all four services, with a hard Docker preflight and a
   zero-skips assertion — the suites skip themselves when Docker is unreachable, and a skip counts as a
   pass), but it is **uncommitted and has never run**. Until it lands, "guarded" means "guarded by whoever
   remembers".
7. That gap is not hypothetical. A full four-service sweep on 2026-07-15 found **two services red on
   committed `develop`**, both invisible because each session only ever ran the one service it was working
   on:
   - `session-operations-service` — 17 integration failures. HU-38 added a constructor parameter to
     `OutboxDomainEventDispatcher` and updated the unit fixtures but not the nine integration ones, so
     every fixture-built DI container failed to resolve it.
   - `mission-design-service` — 5 failures, and these were **two stale failures stacked**. `694265f` made
     trivia authoring Operator-only, turning `MissionEndpointsTests` (which runs as Administrator and
     authors quizzes as setup) red. Then `83a6cc2` removed the author-entered question sequence field in
     favour of insertion identity — its author updated that test's payload but not its expectation, and
     **could not have noticed, because the test was already red from the 403**. Fixing the auth exposed the
     ordering failure hiding behind it.

   Both fixed, tests only — no production code, no `[Authorize]` attribute touched; the breaking changes
   were deliberate and the tests had simply not caught up. The sweep now reads **identity-access 357 ✅,
   session-operations 1154 ✅, scoring-monitoring 221 ✅, mission-design 698 ✅ — all four green.**
8. **A red test stops reporting.** It does not just fail; it silently absorbs the next regression, which is
   how `83a6cc2`'s stale expectation hid behind `694265f`'s 403 for three days. That is the strongest
   argument for takeaway 6: CI's value is not catching the first break, it is refusing to let a second one
   hide behind it.

## References

- Requirements: [`docs/proyecto_requisitos_documento_clave.md`](./proyecto_requisitos_documento_clave.md)
- Wiring / findings: [`backend/docs/integration-event-wiring-audit.md`](../backend/docs/integration-event-wiring-audit.md)
- Prior RabbitMQ incident (URN / consumer-auth): [`backend/docs/hu-25b-scan-score-handoff-2026-07-15.md`](../backend/docs/hu-25b-scan-score-handoff-2026-07-15.md)
