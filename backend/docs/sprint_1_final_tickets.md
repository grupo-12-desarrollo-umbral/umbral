# Sprint 1 (Trivia) — Final Ticket Scope

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`,
> `backend/docs/bd_umbral_entity_spec.md`, and
> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.
> Do not use this final scope as current planning input without rebuilding
> DES-22/DES-23/DES-24 and related trivia tickets around mission snapshots and
> trivia substages.

Final, scoped ticket list for **Trivia Sprint 1**, reconciled against the live Linear
backlog. Decides exactly which HUs are **still to do**, which are **already done**, and
which are **deferred out of this sprint** while keeping trivia fully operable.

- **Linear snapshot:** 2026-06-03 (`umbral-equipo-12`, project *Umbral Proyecto Desarrollo — Equipo 12*).
- **Sources:** statuses from Linear; scope/patterns from
  `docs/trivia_sprint_required_patterns_matrix.md`; ordering from
  `docs/sprint1_delegation_roadmap.md`.
- **Team model that drives scope:** a *team* is multiple humans; play supports **one
  device per team _or_ multiple devices per team**, and the **first submitted answer per
  team wins**. This is why multi-device sync (`HU-08`) and first-answer enforcement
  (`HU-34B`) stay in scope.

## Headline

- Trivia sprint = **31 HUs** (identity 12 · mission-design 5 · session-operations 11 · scoring 3).
- **Done: 14** · **To do: 12** · **Deferred out of sprint: 5**.
- The drop list is **entirely Salomon's leaves** — Samuel's critical-path spine is fully retained.
- All five academic gates still pass: design patterns, WebSockets (incl. operator
  dashboard), RabbitMQ (`HU-34A → HU-37A`), basic pipeline, SOLID.

---

## 0. The 8-hour cut (what to actually build today)

If there is one workday left, **do not attempt the 12 to-do HUs.** They were sliced thin for
planning; stripped of everything optional the real work is small. **RabbitMQ and the
authoritative timer are mandatory and stay**; everything else is cut or folded in.

**The only irreducible order:** *session running → active question → answer → score.* Scoring
needs an answer, an answer needs an active question, an active question needs a running session
(and a timer counting it down). Nothing reorders that away — but everything hanging off it is optional.

### KEEP — build these 7 pieces

| # | Piece | From | Why it's in |
|---|-------|------|-------------|
| 1 | Teams in session | `HU-18` (in progress) | an answer comes *from a team* |
| 2 | Operator assigned | `HU-19` (in progress) | operator runs the session |
| 3 | Minimal session state — enum + one guarded `Transition()` (`Scheduled → Active → Finished`) | `HU-21A` (slim) | need a *running* session |
| 4 | **Authoritative timer — SignalR ticks, expiry auto-closes the question** | `HU-22` | **mandatory**; enforces the round, not decorative |
| 5 | Activate question | thin slice of `HU-33A` | something to answer + to time |
| 6 | Submit answer (first-per-team wins) → **publish `AnswerRegistered`** to RabbitMQ | `HU-34A` (+ `34B` folded in) | the RabbitMQ **producer** |
| 7 | **Consume `AnswerRegistered`** → ledger → team score | `HU-37A` | the RabbitMQ **consumer** |

That covers all five gates: RabbitMQ end-to-end (`publish → consume`), WebSockets (SignalR
timer ticks + state broadcast), patterns (Facade/Proxy already done in `HU-16/19`; Strategy +
Template Method live inside the answer/scoring handlers), pipeline, SOLID.

### CUT / fold today (no gate depends on them)

| Drop | HU | Why it's safe to cut |
|------|----|----------------------|
| Full round-orchestration engine | `HU-33A` (full) | Keep only timer-driven activate + auto-close; no multi-round countdown engine. |
| Orchestrated final results | `HU-33B` | Final score = query the ledger total; skip the close-all choreography. |
| Late/dup rejection (separate) | `HU-34B` | Fold into the one answer handler (take first, reject the rest). |
| Reveal as its own HU | `HU-35` | Optional: broadcast the correct answer on the auto-close message. |
| Operator live dashboard | `HU-36A` | Not needed to prove a gate. |
| Multi-device sync | `HU-08` | Independent; nice-to-have, not a gate. |

### Simplifications inside what you keep

- **`HU-21A` = a plain enum + one guarded `Transition()`**, not the six-state machine + Chain of Responsibility.
- **Timer owns the close** — on expiry the question closes itself and stops accepting answers
  (a small auto-close slice of `HU-33A`/`HU-33B`); no separate operator "close" action.
- **Patterns are a code-shape requirement, not a feature count** — show Strategy + Template
  Method inside the answer/scoring handlers; don't owe one HU per pattern.
- **Merge, don't slice** — `HU-34A`+`HU-34B` = one handler.

> The full §2 plan below stays as the *complete* sprint scope. §0 is the realistic one-day cut.

---

## 1. Already done (14)

| HU | DES | Title | Service |
|----|-----|-------|---------|
| `HU-01` | DES-5 | Inicio de sesión general de usuarios | identity |
| `HU-02` | DES-6 | Gestión de acceso de usuarios | identity |
| `HU-03` | DES-7 | Asignación de roles y permisos | identity |
| `HU-04` | DES-8 | Registro y mantenimiento de equipos | identity |
| `HU-05` | DES-9 | Asignación de participantes a equipos | identity |
| `HU-06` | DES-10 | Inicio de sesión de participantes | identity |
| `HU-07A` | DES-11 | Validación de membresía en sesión | identity |
| `HU-07B` | DES-12 | Reconexión autorizada del participante | identity · session-ops |
| `HU-11` | DES-17 | Creación y edición de quizzes de trivia | mission-design |
| `HU-12` | DES-18 | Publicación y archivado de quizzes | mission-design |
| `HU-13` | DES-19 | Duplicación y retiro de quizzes usados | mission-design |
| `HU-14A` | DES-20 | Gestión de preguntas y opciones de trivia | mission-design |
| `HU-14B` | DES-21 | Reglas de validación de preguntas de trivia | mission-design |
| `HU-16` | DES-23 | Creación de sesiones trivia | session-ops |

> Note: `HU-13` was a drop candidate but is already complete, so it is no longer a scope decision.
> `HU-16` and `HU-07B` were on the to-do list in earlier snapshots; both are now **Done**, which is
> what frees the parallelism described in §2 (`HU-18 ∥ HU-19` and the independent `HU-08`).

---

## 2. Still to do this sprint (12) — *not* one straight line

Earlier snapshots listed these as a flat `1…14` sequence, which made the whole sprint *look*
serial. It isn't. With `HU-16`, `HU-07A` and `HU-07B` now done, the remaining 12 form a DAG
with **three independent entry points** and a **fan-out**. Only one chain is truly serial.

```
HU-08  ───────────────────────────  (independent island — only needs HU-07A✅ + HU-07B✅; start anytime)

HU-18 ┐
      ├──► HU-21A ──► HU-22 ──► HU-33A ──► HU-34A ──┬──► HU-34B
HU-19 ┘                            │               ├──► HU-37A ──► HU-33B ──► HU-35
                                   │               └──► HU-36A
                                   └──────────────────────────────► (HU-33B also needs HU-33A)
```

**Critical path (the only serial chain):**
`HU-18/19 → HU-21A → HU-22 → HU-33A → HU-34A → HU-37A → HU-33B → HU-35` — everything else hangs
off it in parallel. Owner split per the delegation roadmap: **Samuel = spine**, **Salomon = leaves**.

### Waves (what becomes workable, and when)

| Wave | HU | DES | Title | Service | Owner | Pattern(s) | Transport | Unblocked when | Parallel with |
|------|----|-----|-------|---------|-------|-----------|-----------|----------------|---------------|
| **0 · now** | `HU-18` 🟡 | DES-25 | Asociación de equipos a sesiones | identity | Samuel | `Facade` | — | `HU-16` ✅ | `HU-19`, `HU-08` |
| **0 · now** | `HU-19` 🟡 | DES-26 | Asignación de operador a sesión | identity | Samuel | `Facade`, `Proxy` | — | `HU-16` ✅ | `HU-18`, `HU-08` |
| **0 · now** | `HU-08` | DES-13 | Sincronización multi-dispositivo del equipo | identity | Salomon | — (RT enabler) | SignalR | `HU-07A` ✅, `HU-07B` ✅ | **all of §2** |
| **1** | `HU-21A` | DES-28 | Transiciones válidas de estado de sesión | session-ops | Samuel | `State`, `Chain of Responsibility` | SignalR | `HU-18` + `HU-19` | — |
| **2** | `HU-22` | DES-30 | Temporizador autoritativo de sesión | session-ops | Samuel | `State` | SignalR | `HU-21A` | — |
| **3** | `HU-33A` | DES-44 | Orquestación automatizada por rondas | session-ops | Samuel | `Facade`, `State`, `Strategy` | SignalR | `HU-22` | — |
| **4** | `HU-34A` | DES-46 | Registro de primera respuesta válida por equipo | session-ops | Samuel | `Template Method`, `Chain of Responsibility` | SignalR + RabbitMQ | `HU-33A` | — |
| **5 · fan-out** | `HU-34B` | DES-47 | Rechazo de respuestas tardías/repetidas | session-ops | Samuel | `Template Method`, `Chain of Responsibility` | — | `HU-34A` | `HU-37A`, `HU-36A` |
| **5 · fan-out** | `HU-37A` | DES-51 | Ledger de puntaje por respuestas | scoring | Salomon | `Strategy` | RabbitMQ | `HU-34A` | `HU-34B`, `HU-36A` |
| **5 · fan-out** | `HU-36A` | DES-49 | Monitoreo restringido resp./no resp. (dashboard operador) | session-ops | Salomon | `Proxy` | SignalR | `HU-33A` + `HU-34A` | `HU-34B`, `HU-37A` |
| **6** | `HU-33B` | DES-45 | Cierre automático + resultados finales | session-ops | Samuel | `Facade`, `State`, `Strategy` | SignalR + RabbitMQ | `HU-33A` + `HU-37A` | — |
| **7** | `HU-35` | DES-48 | Revelación de resultado y explicación | session-ops | Salomon | — | SignalR | `HU-33B` | — |

🟡 = In Progress in Linear. **Owner totals:** Samuel 8 · Salomon 4.

**Sequencing takeaways:**
- **`HU-08` is fully independent** — both its blockers are done. Salomon can land it at any time, off the critical path. (It was listed *last* before, which hid this.)
- **`HU-18 ∥ HU-19`** share a single done blocker (`HU-16`) → parallel from the start; both already in progress.
- **`HU-34B`, `HU-37A`, `HU-36A` fan out** the moment `HU-34A` lands → up to three people in parallel.
- The serial backbone is **`HU-21A → HU-22 → HU-33A → HU-34A`** — keep one owner (Samuel) driving it so it never stalls; the leaves attach as soon as each gate opens.

**Why each is required for an operable trivia:**
- `HU-18/19` — attach teams and assign the operator to the (already-created) session.
- `HU-21A` — session lifecycle / valid state transitions (foundation for timer + rounds).
- `HU-22` — **authoritative round timer**; this is what makes trivia *timed*.
- `HU-33A` — round orchestration (present question, countdown, advance).
- `HU-34A` — register the **first valid answer per team**; publishes `AnswerRegistered`.
- `HU-34B` — reject the 2nd…nth submission → **enforces "first answer per team wins"**
  (required because multiple teammates can submit).
- `HU-37A` — scoring ledger; **consumes `AnswerRegistered` over RabbitMQ** (the
  demonstrated end-to-end async workflow).
- `HU-33B` — auto-close + final results broadcast.
- `HU-36A` — **operator dashboard, live** answered/not-answered (the SignalR-on-the-dashboard demo).
- `HU-35` — reveal correct answer + explanation (the payoff of each question).
- `HU-08` — **multi-device-per-team** sync (reconnection `HU-07B` already done).

---

## 3. Deferred out of Sprint 1 (5) — trivia still operable without them

These carry no required pattern or transport that the *to-do* set doesn't already cover,
and no live gameplay path depends on them.

| HU | DES | Title | Service | Owner | Why droppable | Requirement still covered by |
|----|-----|-------|---------|-------|---------------|------------------------------|
| `HU-20` | DES-27 | Consulta de sesiones asignadas | identity | Salomon | REST "my sessions" list; operator is bound to the session by `HU-19` and runs it directly. Not the live dashboard. | `Proxy` → `HU-07A`, `HU-19` |
| `HU-21B` | DES-29 | Auditoría de cambios de estado de sesión | session-ops | Salomon | Async audit/history trail; no gameplay path reads it. | `State` → `HU-21A/22/33A/33B`; RabbitMQ → `HU-34A`→`HU-37A` |
| `HU-36B` | DES-50 | Revisión post-cierre de respuestas y puntos | session-ops | Salomon | Post-close review (data frozen). Results already shown by `HU-33B` + reveal by `HU-35`. Live SignalR dashboard is `HU-36A`. | results → `HU-33B`/`HU-35`; `Proxy` → `HU-36A` |
| `HU-37B` | DES-52 | Actualización de ranking tras cambios | scoring | Salomon | Re-ranks after retroactive score edits — none occur in a straight run. Final ranking from `HU-37A` at close. | `Strategy` → `HU-33A/33B/37A` |
| `HU-39B` | DES-55 | Ranking en tiempo real para trivia | scoring | Salomon | Live leaderboard during play (spectacle). Final standings from `HU-37A` at close suffice. | `Strategy` → `HU-33A/33B/37A`; transports → `HU-34A/37A/36A` |

> Dropping `HU-39B` also **simplifies `HU-35`**: in Linear `HU-35` was blocked by
> `HU-33B` **and** `HU-39B`; with `HU-39B` out, `HU-35` only needs `HU-33B`.

**What you give up:** self-service session list, state-change audit log, operator
post-close drill-down, and live/retroactive ranking. None is touched by a player during a
timed, multi-device, first-wins round.

---

## 4. Out of Trivia scope entirely (not part of this sprint)

TreasureHunt / mission-session / QR / evidence / penalty / extra-frontend HUs — never in
the Trivia Sprint 1 cut. Listed for completeness only.

`HU-09` (done, mission authoring) · `HU-10A` · `HU-10B` · `HU-15` · `HU-17` · `HU-23` ·
`HU-24A` · `HU-24B` · `HU-25A` · `HU-25B` · `HU-26` · `HU-27` · `HU-28` · `HU-29` ·
`HU-30A` · `HU-30B` · `HU-31` · `HU-32` · `HU-38` · `HU-39A` · `HU-40A` · `HU-40B`
(+ the `ENABLER` / `PRD` / `AFK` planning tickets).

---

## Academic-requirement coverage (final to-do set)

| Requirement | Covered by |
|---|---|
| Design patterns | Template Method (`HU-34A/34B`), Facade (`HU-16/33A/33B`), State (`HU-21A/22/33A/33B`), Chain of Responsibility (`HU-21A/34A/34B`), Proxy (`HU-19/36A`, + done `HU-07A`), Strategy (`HU-33A/33B/37A`) |
| WebSockets | `HU-21A/22/33A/33B/34A` (gameplay) + `HU-36A` (operator dashboard) + `HU-07B/08/35` |
| RabbitMQ | `HU-34A` publishes `AnswerRegistered` → `HU-37A` consumes — the demonstrated workflow |
| Basic pipeline | spine `HU-16 → 18/19 → 21A → 22 → 33A → 34A → 33B` |
| SOLID | clean-architecture layering across the spine |
