# Sprint 1 — Delegation & HU Order (§0 realistic 8-hour cut)

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`,
> `backend/docs/bd_umbral_entity_spec.md`, and
> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.
> Rebuild this order before assigning new work. It contains old trivia-session
> and `Scheduled` assumptions that conflict with mission snapshots and canonical
> session states.

Two-person execution plan for the **§0 "8-hour cut"** of the Trivia sprint. This is the
*realistic* one-day scope, not the full §2 DAG. Source of truth for scope:
[`sprint_1_final_tickets.md`](./sprint_1_final_tickets.md) §0. Source for build order &
repo seams: `/tmp/claude-1000/handoff-implementation-today.md`.

- **Owners:** Samuel = critical-path spine · Salomon = scoring consumer (the one leaf still in scope).
- **Date:** 2026-06-04.

---

## TL;DR

- **HU-19 is done** (domain → application → infra → api committed; only the frontend
  `SessionOperatorPanel` + e2e spec uncommitted).
- **RabbitMQ is built first as a tracer bullet** — it is the riskiest, longest pole
  (greenfield scoring service, no AMQP wired in code yet), not because anything depends on it.
- **The contract freeze is the only thing gating Salomon.** It is Samuel's very first task,
  so Salomon starts ~10 minutes in — there is no long "meanwhile."
- **HU-08 is CUT** in §0. It is *not* available as parallel work. Salomon's only in-scope
  work is the HU-37A consumer.

### Plain sequence

1. Samuel freezes `AnswerRegistered`.
2. Salomon starts `HU-37A`.
3. Samuel continues `HU-21A → HU-22 → HU-33A → HU-34A`.
4. When Samuel reaches `HU-34A`, Salomon should already have the scoring consumer mostly ready.
5. Then you connect real answer publishing to the already-built consumer.

---

## The one hard ordering

```
session running → active question → answer → score
```

Dependency chain (what literally cannot start until the prior exists):

```
21A → 22 → 33A(thin) → 34A → 37A
```

RabbitMQ is **not** a step in this chain — it is the transport *inside* 34A (producer) and
37A (consumer). The handoff inverts the natural order and builds the pipe first to de-risk it.

---

## Build order

| # | Step | Owner | What this really means |
|---|------|-------|------------------------|
| 0 | Freeze `AnswerRegistered` contract | Samuel | **Only hard gate on Salomon.** Samuel defines the exact payload class/JSON shape, exchange, routing key, and idempotency fields. Once this is frozen, Salomon can wire the consumer safely without waiting for trivia gameplay. |
| 1 | Producer infra — `RabbitMQ.Client` + `IEventPublisher` + connection provider + `INotificationHandler<AnswerRegisteredEvent>` | Samuel | This is only the **transport seam** inside session-ops, not the real answer flow yet. It means session-ops can publish the event before `HU-34A` exists. |
| 2 | Consumer service skeleton — greenfield scoring-monitoring-service (4 csprojs, Program.cs, DI, health endpoint, config, Docker/compose wiring if needed) | **Salomon** | **This can start almost immediately after step 0.** He does **not** need to wait for `HU-21A/22/33A/34A`. Most of this work is independent scaffolding. |
| 3 | Consumer app logic — `BackgroundService` with manual ack + bounded prefetch, contract deserialization, idempotency guard, in-memory ledger, `GET /scores/{sessionId}` | **Salomon** | This is the real `HU-37A` core. Still parallel to Samuel's session-state/timer/question work. The only dependency is the frozen contract from step 0. |
| 4 | Prove `publish → broker → consume → score` with a hardcoded/test publish | both | First meeting point. Use a fake/manual publish before any real trivia answer submission exists. This validates the pipe while Samuel continues on the gameplay spine. |
| 5 | HU-21A — minimal state (enum + one guarded `Transition()`) | Samuel | `Scheduled → Active → Finished`. Samuel's critical-path spine starts here. Salomon is already busy in steps 2-4 while this happens. |
| 6 | HU-22 — authoritative timer (SignalR ticks, auto-close on expiry) | Samuel | **Needs 21A.** Timer owns the close; no separate operator "close". No dependency on scoring implementation. |
| 7 | HU-33A (thin) — activate question + auto-close | Samuel | No multi-round engine. Still independent from Salomon's ledger service. |
| 8 | HU-34A (+34B folded) — first valid answer per team → raises `AnswerRegisteredEvent` | Samuel | This is when the already-proven RabbitMQ pipe gets connected to the real answer flow. By this point Salomon's service should already be listening and able to score. |

---

## What Salomon can adelantar in parallel

The original table makes it look like Salomon only works once Samuel reaches a late step. That is
not the intended reading. In the §0 cut he owns only **one HU (`HU-37A`)**, but he can start most
of that HU long before Samuel reaches the real answer flow.

### Direct HU handoff version

- **After Samuel freezes the `AnswerRegistered` contract**, Salomon can start **`HU-37A`** immediately.
  He does not need to wait for `HU-21A`, `HU-22`, `HU-33A`, or `HU-34A`.
- **After Samuel finishes producer infra**, Salomon continues **`HU-37A`** with real broker wiring,
  but this is not a new dependency for the business logic; it just makes integration easier.
- **While Samuel is doing `HU-21A`**, Salomon should still be doing **`HU-37A`**.
  There is no handoff here.
- **While Samuel is doing `HU-22`**, Salomon should still be doing **`HU-37A`**.
  Timer work does not block scoring.
- **While Samuel is doing `HU-33A (thin)`**, Salomon should still be doing **`HU-37A`**.
  Question activation/auto-close does not block scoring.
- **After Samuel finishes `HU-34A`**, Salomon does **not** start a new HU; he connects the already-built
  **`HU-37A`** consumer to the real answer flow and validates end-to-end scoring.

### Concretely: what Salomon is doing inside `HU-37A`

Right after the contract freeze, Salomon can advance these `HU-37A` slices:

1. Create the new scoring service/project structure.
2. Add host startup, DI registration, configuration binding, and environment variables.
3. Wire RabbitMQ connection/consumer bootstrapping.
4. Implement event DTO deserialization using the frozen `AnswerRegistered` contract.
5. Build the in-memory score ledger keyed by `sessionId` and `teamId`.
6. Expose `GET /scores/{sessionId}`.
7. Add idempotency handling around `messageId`.
8. Add manual ack / retry / poison-message logging behavior.
9. Write tests for "consume one event → ledger updated".

### The simple reading

If you want the shortest possible interpretation, it is this:

- Samuel starts by unblocking `HU-37A`.
- While Samuel builds `HU-21A → HU-22 → HU-33A → HU-34A`, Salomon is already building `HU-37A`.
- When Samuel finally lands `HU-34A`, Salomon should be basically done, and they only need the final integration proof.

### What Salomon does **not** wait for

- He does **not** wait for `HU-21A`.
- He does **not** wait for `HU-22`.
- He does **not** wait for `HU-33A`.
- He does **not** wait for the frontend.
- He only truly waits for the contract freeze, then later meets Samuel again at the real `HU-34A` publish.

---

## Delegation timeline

```
t=0     Samuel: freeze AnswerRegistered contract        ← only gate on Salomon
t≈10m   ── hand contract to Salomon ──
        Samuel:  producer → 21A → 22 → 33A → 34A
        Salomon: service skeleton → Rabbit consumer → in-memory ledger → GET /scores/{id} → consumer tests
        ── both test against a fake publish, meet at the integration test ──
```

- **Coordination point:** exactly one — the contract freeze. After that the two halves don't
  touch each other's code until the integration test.
- **No idle "meanwhile":** the window before Salomon can start is the contract-freeze task
  itself (~10 min), not hours.

---

## The `AnswerRegistered` contract

Defined identically on both sides (producer in session-ops, consumer in scoring):

```
{
  messageId,    // unique id for idempotency / dedup
  sessionId,
  teamId,
  questionId,
  optionId,
  isCorrect,
  points,
  answeredAt    // timestamp
}
```

Transport: topic exchange `domain.events`, routing key `trivia.answer.registered`.
Broker defaults (no creds set in compose): `guest`/`guest`, host `rabbitmq` in-network /
`localhost` from host, vhost `/`, port 5672 (mgmt 15672).

---

## Explicitly NOT in this cut (do not start)

| HU | Why out |
|----|---------|
| `HU-08` multi-device sync | CUT in §0 — independent, nice-to-have, not a gate. |
| `HU-36A` operator dashboard | CUT in §0 — not needed to prove a gate (also would need 33A+34A). |
| `HU-35` reveal | CUT as its own HU — optionally fold into the auto-close message. |
| `HU-33B` orchestrated final results | CUT — final score = query the ledger total. |
| `HU-33A` full round engine | Keep only timer-driven activate + auto-close. |

---

## Risk guard

The greenfield scoring service + RabbitMQ both-ends is the part most likely to eat the day.
If hour ~4 arrives without the pipe green, **protect RabbitMQ end-to-end** and let the SignalR
timer (HU-22) be what slips. RabbitMQ and the timer are the two mandatory gates; between them,
RabbitMQ wins if forced to choose.
