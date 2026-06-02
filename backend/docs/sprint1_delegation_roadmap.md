# Sprint 1 (Trivia) — Delegation Roadmap

Splits the remaining **Trivia Sprint 1** work between **you** (the hardest + foundational
HUs) and **a friend** (downstream leaves).

**Single source of truth:** this roadmap is built directly from the **Linear dependency
graph** (`blockedBy` relations), not from the `Bloqueado por` prose in
`tickets_trivia_v2.md`. Pattern/tier requirements come from
`docs/trivia_sprint_required_patterns_matrix.md`. Out-of-scope (TreasureHunt / mission /
evidence / penalty) blockers present in Linear have been stripped — see the `†` / `§`
footnotes for exactly which edges were removed and why.

## Status

- **Sprint total:** 31 HUs (identity 12 · mission-design 5 · session-operations 11 · scoring 3).
- **Done (in-sprint, 8):** `HU-01`, `HU-02`, `HU-03`, `HU-04`, `HU-05` (identity) ·
  `HU-11`, `HU-14A`, `HU-14B` (mission-design).
- **Done (out-of-sprint):** `HU-09` — mission authoring, *not* part of the trivia sprint;
  adds no trivia progress.
- **Remaining in-sprint:** 23 → **you: 10**, **friend: 13**.

> The `HU-11 → HU-14A → HU-14B` content-authoring chain is already **complete**, so the
> mission-design content base is done. What remains of mission-design is `HU-12` (yours)
> and `HU-13` (friend).

## Split principle (and where it bends)

- **You own the spine** — the long critical-path chain through session creation,
  lifecycle, timer, round orchestration, and answer handling.
- **Friend owns the leaves** — auth, scoring ledger/ranking, audit, and read/monitoring
  projections that hang off your spine.
- **Honest exception — two cross-team handoffs exist** (the old "you never wait on your
  friend" claim does **not** hold against Linear):
  1. **Your `HU-34A` is blocked by friend `HU-07A`.** Low risk: your path to `HU-34A` is
     ~7 HUs deep, while `HU-07A` is only 2 deep for the friend (`HU-06 → HU-07A`), so the
     friend will be ready long before you need it — *as long as the friend starts `HU-06`
     on day one*.
  2. **Your `HU-33B` is blocked by friend `HU-37A`, and `HU-37A` is in turn blocked by
     your `HU-34A`.** This is a genuine you → friend → you ping-pong: after you land
     `HU-34A`, the friend builds the scoring ledger `HU-37A`, and only then can you close
     out `HU-33B`. Fill that gap with `HU-34B` (which only needs `HU-34A`) so you don't idle.

---

## YOUR 10 HUs — hardest + foundational

Listed in **true Linear topological order** (top = start first). This is the critical path.

| # | HU | Title | Service | Pattern(s) | Transport | Blocked by (trivia-scope, Linear) | Tier |
|---|----|-------|---------|-----------|-----------|-----------------------------------|------|
| 1 | `HU-12` | Publicación y archivado de quizzes | mission-design | `Template Method` | — | — (HU-11 ✅, HU-14A ✅, HU-14B ✅) → **ready now** | base root |
| 2 | `HU-16` | Creación de sesiones trivia | session-ops | `Facade` | — | HU-12 (you) (HU-11/14A/14B ✅) | base |
| 3 | `HU-18` | Asociación de equipos a sesiones | identity | `Facade` | — | HU-16 (you) (HU-04 ✅) † | base |
| 4 | `HU-19` | Asignación de operador a sesión | identity | `Facade`, `Proxy` | — | HU-16 (you) (HU-03 ✅) † | base |
| 5 | `HU-21A` | Transiciones válidas de estado de sesión | session-ops | `State`, `Chain of Responsibility` | SignalR | HU-16 (you), HU-18 (you), HU-19 (you) † | **Tier 1 hardest** |
| 6 | `HU-22` | Temporizador autoritativo de sesión | session-ops | `State` | SignalR | HU-21A (you) | Tier 3 |
| 7 | `HU-33A` | Orquestación automatizada por rondas | session-ops | `Facade`, `State`, `Strategy` | SignalR | HU-16 (you), HU-21A (you), HU-22 (you) | **Tier 1 hardest** |
| 8 | `HU-34A` | Registro de primera respuesta válida | session-ops | `Template Method`, `Chain of Responsibility` | SignalR + RabbitMQ | HU-33A (you), **HU-07A (friend)** | **Tier 2 hard** |
| 9 | `HU-34B` | Rechazo de respuestas tardías/repetidas | session-ops | `Template Method`, `Chain of Responsibility` | — | HU-34A (you) | **Tier 2 hard** |
| 10 | `HU-33B` | Cierre automático + resultados finales | session-ops | `Facade`, `State`, `Strategy` | SignalR + RabbitMQ | HU-33A (you), **HU-37A (friend)** | **Tier 1 hardest** |

`†` = a `HU-15` (mission-session creation, **out of scope**) edge was stripped from the
Linear `blockedBy` set for `HU-18`, `HU-19`, and `HU-21A`.

**Transport column** (per `trivia_sprint_required_patterns_matrix.md`): `SignalR` = real-time
push to clients/operator; `RabbitMQ` = domain event published after transactional success for
async secondary processing; `—` = neither (synchronous request/response only). **RabbitMQ
never sits on the critical path** — the main game flow does not depend on it. The demonstrated
end-to-end async workflow is `HU-34A` publishing `AnswerRegistered` (SignalR + RabbitMQ),
consumed by scoring (`HU-37A`) and audit.

**Critical-path reality (not two parallel tracks):** the spine is essentially one long
chain — `HU-12 → HU-16 → {HU-18, HU-19} → HU-21A → HU-22 → HU-33A → HU-34A → HU-33B`.
`HU-21A` is **not** a root and **not** parallel to content; in Linear it sits *downstream*
of session creation (`HU-16 → HU-18/19 → HU-21A`). The only real intra-spine parallelism
is **`HU-18` ∥ `HU-19`** (both need only `HU-16`) and **`HU-34B` ∥ waiting-for-`HU-37A`**
(both branch off `HU-34A`).

**Why these are yours:** they contain every Tier 1/2/3 hard HU from the matrix (`HU-21A`,
`HU-33A/B`, `HU-34A/B`, `HU-22`) plus the base roots the rest depends on
(`HU-12 → HU-16`, and `HU-18`/`HU-19`/`HU-21A` feeding `HU-33A`).

---

## FRIEND's 13 HUs — leaves

Listed by earliest start. "Can start" reflects the **real** Linear graph (done blockers
resolved, out-of-scope blockers stripped).

| HU | Title | Service | Pattern(s) | Transport | Blocked by (trivia-scope, Linear) | Can start |
|----|-------|---------|-----------|-----------|-----------------------------------|-----------|
| `HU-06` | Inicio de sesión de participantes | identity | `Proxy` | — | — (HU-01 ✅) | **day one** |
| `HU-07A` | Validación de membresía en sesión | identity | `Proxy` | SignalR | HU-06 (friend) (HU-05 ✅) | after own 06 |
| `HU-07B` | Reconexión autorizada del participante | identity | `Proxy` | SignalR | HU-07A (friend) | after own 07A |
| `HU-08` | Sincronización multi-dispositivo | identity | real-time enabler | SignalR | HU-07A, HU-07B (friend) | after own 07A/07B |
| `HU-13` | Duplicación y retiro de quizzes | mission-design | `Template Method` | — | HU-12 (**you**) (HU-11 ✅) | after your 12 |
| `HU-20` | Consulta de sesiones asignadas | identity | `Proxy` | — | HU-19 (**you**) | after your 19 |
| `HU-21B` | Auditoría de cambios de estado | session-ops | `State` | RabbitMQ | HU-21A (**you**) | after your 21A |
| `HU-37A` | Ledger de puntaje por respuestas | scoring | `Strategy` | RabbitMQ | **HU-34A (you)** § | after your 34A |
| `HU-37B` | Actualización de ranking tras cambios | scoring | `Strategy` | SignalR | HU-37A (friend) | after own 37A |
| `HU-36A` | Monitoreo restringido resp./no resp. | session-ops | `Proxy` | SignalR | HU-33A, HU-34A (**you**) | after your 34A |
| `HU-36B` | Revisión post-cierre de resp. y puntos | session-ops | `Proxy` (applies) | SignalR | HU-33B (**you**), HU-37A (friend) | after your 33B + own 37A |
| `HU-39B` | Ranking en tiempo real para trivia | scoring | `Strategy` | SignalR + RabbitMQ | HU-33B (**you**), HU-37A, HU-37B (friend) | after your 33B + own 37A/37B |
| `HU-35` | Revelación de resultado y explicación | session-ops | — (reads closed state) | SignalR | HU-33B (**you**), HU-39B (friend) | after your 33B + own 39B |

`§` = `HU-37A`'s Linear `blockedBy` also includes `HU-30A` (evidence-context validation)
and `HU-38` (penalties) — both **out of scope** — stripped here, leaving only `HU-34A`.

**Friend's true day-one start is just `HU-06`.** Everything else is gated: `HU-07A` needs
the friend's own `HU-06` first; the scoring ledger `HU-37A` waits on your `HU-34A` (it is
**not** a day-one start). So the friend's realistic opening is the participant-auth chain
`HU-06 → HU-07A → HU-07B → HU-08`, which they can run end-to-end without you.

**Friend's internal chains** (friend-blocked-by-friend, fully under their control):
- `HU-06 → HU-07A → HU-07B → HU-08`
- `HU-37A → HU-37B` (chain *starts* only after your `HU-34A`)
- `HU-39B → HU-35` (both also gated by your `HU-33B`)

---

## Cross-team dependency summary

The only edges that cross the you/friend boundary:

| Blocked HU | Owner | Waits on | Owner | Note |
|---|---|---|---|---|
| `HU-13` | friend | `HU-12` | you | leaf off content lifecycle |
| `HU-20` | friend | `HU-19` | you | |
| `HU-21B` | friend | `HU-21A` | you | |
| `HU-34A` | **you** | `HU-07A` | **friend** | ⚠️ you wait on friend |
| `HU-37A` | friend | `HU-34A` | you | |
| `HU-36A` | friend | `HU-33A`, `HU-34A` | you | |
| `HU-33B` | **you** | `HU-37A` | **friend** | ⚠️ you wait on friend (ping-pong with `HU-34A`) |
| `HU-36B` | friend | `HU-33B` | you | + own `HU-37A` |
| `HU-39B` | friend | `HU-33B` | you | + own `HU-37A`/`HU-37B` |
| `HU-35` | friend | `HU-33B` | you | + own `HU-39B` |

---

## Handoff order (what each side unblocks, and when)

**Day one (parallel):** you start `HU-12`; friend starts `HU-06`.

As you land spine HUs, they release friend work:

1. **You finish `HU-12`** → unblocks friend `HU-13`; unblocks your `HU-16`.
2. **You finish `HU-16`** → unblocks your `HU-18` + `HU-19`.
3. **You finish `HU-19`** → unblocks friend `HU-20`.
4. **You finish `HU-18` + `HU-19`** → unblocks your `HU-21A`.
5. **You finish `HU-21A`** → unblocks friend `HU-21B`; unblocks your `HU-22`.
6. **You finish `HU-22`** → unblocks your `HU-33A`.
7. **You finish `HU-33A`** (needs friend `HU-07A` already done) → unblocks your `HU-34A`.
8. **You finish `HU-34A`** → unblocks your `HU-34B`, friend `HU-36A`, **and friend `HU-37A`**.
9. **Friend finishes `HU-37A`** → unblocks your `HU-33B`; friend `HU-37B`, `HU-36B`.
10. **You finish `HU-33B`** → unblocks friend `HU-35`, `HU-36B`, `HU-39B` (with their own
    `HU-37A`/`HU-37B`/`HU-39B` prerequisites).

**Sequencing advice:**
- **Friend: start `HU-06` on day one** so `HU-07A` is ready well before your `HU-34A`.
- **You: do `HU-34B` while the friend builds `HU-37A`**, so the `HU-34A → HU-37A → HU-33B`
  ping-pong doesn't leave you idle.

## Dependency map (remaining sprint only)

```
DONE: HU-01 02 03 04 05 (identity) · HU-11 14A 14B (mission-design)

YOU (spine, critical path)
--------------------------
HU-12 ─▶ HU-16 ─┬─▶ HU-18 ─┐
                └─▶ HU-19 ─┴─▶ HU-21A ─▶ HU-22 ─▶ HU-33A ─┬─▶ HU-34A ─▶ HU-34B
                                                          └─▶ HU-33B
                                                              (HU-33B also needs friend HU-37A)
                                                              (HU-34A also needs friend HU-07A)

FRIEND (leaves)
---------------
day one:  HU-06 ─▶ HU-07A ─┬─▶ HU-07B ─▶ HU-08
                           └─(gates your HU-34A)
after your 12:   HU-13
after your 19:   HU-20
after your 21A:  HU-21B
after your 34A:  HU-37A ─▶ HU-37B          ·  HU-36A
after your 33B:  HU-36B (+37A) · HU-39B (+37A/37B) ─▶ HU-35

CROSS-TEAM (boundary-crossing edges)
------------------------------------
friend HU-07A ──▶ your   HU-34A
your   HU-34A ──▶ friend HU-37A ──▶ your HU-33B      (you → friend → you)
```

`✅` = already done · `(you)` / `(friend)` = owner of the blocker.
