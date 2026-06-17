# Sprint 1 (Trivia) — Delegation Roadmap

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`,
> `backend/docs/bd_umbral_entity_spec.md`, and
> `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`.
> Rebuild delegation order before assigning new work. The old roadmap assumes
> standalone trivia-session creation and `Scheduled`; the current model uses
> mission-only `LiveSession` creation in `Preparing`.

Splits the remaining **Trivia Sprint 1** work between **Samuel** (the hardest + foundational
HUs) and **Salomon** (downstream leaves).

**Single source of truth:** this roadmap is built directly from the **Linear dependency
graph** (`blockedBy` relations), not from the `Bloqueado por` prose in
`tickets_trivia_v2.md`. Pattern/tier requirements come from
`docs/trivia_sprint_required_patterns_matrix.md`. Out-of-scope (TreasureHunt / mission /
evidence / penalty) blockers present in Linear have been stripped — see the `†` / `§`
footnotes for exactly which edges were removed and why.

## Status

- **Linear snapshot:** 2026-06-03.
- **Sprint total:** 31 HUs (identity 12 · mission-design 5 · session-operations 11 · scoring 3).
- **Done (in-sprint, 9):** `HU-01`, `HU-02`, `HU-03`, `HU-04`, `HU-05`, `HU-06` (identity) ·
  `HU-11`, `HU-14A`, `HU-14B` (mission-design).
- **In progress:** `HU-12` (Samuel), `HU-07A` (Salomon).
- **Done (out-of-sprint):** `HU-09` — mission authoring, *not* part of the trivia sprint;
  adds no trivia progress.
- **Remaining in-sprint:** 22 → **Samuel: 10**, **Salomon: 12**.

> The `HU-11 → HU-14A → HU-14B` content-authoring chain is already **complete**, so the
> mission-design content base is done. What remains of mission-design is `HU-12` (Samuel,
> **in progress**) and `HU-13` (Salomon, still blocked on `HU-12`).

## Split principle (and where it bends)

- **Samuel owns the spine** — the long critical-path chain through session creation,
  lifecycle, timer, round orchestration, and answer handling.
- **Salomon owns the leaves** — auth, scoring ledger/ranking, audit, and read/monitoring
  projections that hang off Samuel's spine.
- **Honest exception — two cross-team handoffs exist** (the old "Samuel never waits on
  Salomon" claim does **not** hold against Linear):
  1. **Samuel's `HU-34A` is blocked by Salomon's `HU-07A`.** Low risk: Samuel's path to `HU-34A` is
     ~7 HUs deep, while `HU-07A` is only 2 deep for Salomon (`HU-06 → HU-07A`), so Salomon
     will be ready long before Samuel needs it — and that precondition is already being met
     because `HU-06` is done and `HU-07A` is in progress.
  2. **Samuel's `HU-33B` is blocked by Salomon's `HU-37A`, and `HU-37A` is in turn blocked by
     Samuel's `HU-34A`.** This is a genuine Samuel → Salomon → Samuel ping-pong: after Samuel lands
     `HU-34A`, Salomon builds the scoring ledger `HU-37A`, and only then can Samuel close
     out `HU-33B`. Fill that gap with `HU-34B` (which only needs `HU-34A`) so Samuel doesn't idle.

---

## SAMUEL'S 10 HUs — hardest + foundational

Listed in **true Linear topological order** (top = start first). This is the critical path.

| # | HU | Title | Service | Pattern(s) | Transport | Blocked by (trivia-scope, Linear) | Tier |
|---|----|-------|---------|-----------|-----------|-----------------------------------|------|
| 1 | `HU-12` | Publicación y archivado de quizzes | mission-design | `Template Method` | — | — (HU-11 ✅, HU-14A ✅, HU-14B ✅) → **in progress** | base root |
| 2 | `HU-16` | Creación de sesiones trivia | session-ops | `Facade` | — | HU-12 (Samuel) (HU-11/14A/14B ✅) | base |
| 3 | `HU-18` | Asociación de equipos a sesiones | identity | `Facade` | — | HU-16 (Samuel) (HU-04 ✅) † | base |
| 4 | `HU-19` | Asignación de operador a sesión | identity | `Facade`, `Proxy` | — | HU-16 (Samuel) (HU-03 ✅) † | base |
| 5 | `HU-21A` | Transiciones válidas de estado de sesión | session-ops | `State`, `Chain of Responsibility` | SignalR | HU-16 (Samuel), HU-18 (Samuel), HU-19 (Samuel) † | **Tier 1 hardest** |
| 6 | `HU-22` | Temporizador autoritativo de sesión | session-ops | `State` | SignalR | HU-21A (Samuel) | Tier 3 |
| 7 | `HU-33A` | Orquestación automatizada por rondas | session-ops | `Facade`, `State`, `Strategy` | SignalR | HU-16 (Samuel), HU-21A (Samuel), HU-22 (Samuel) | **Tier 1 hardest** |
| 8 | `HU-34A` | Registro de primera respuesta válida | session-ops | `Template Method`, `Chain of Responsibility` | SignalR + RabbitMQ | HU-33A (Samuel), **HU-07A (Salomon)** | **Tier 2 hard** |
| 9 | `HU-34B` | Rechazo de respuestas tardías/repetidas | session-ops | `Template Method`, `Chain of Responsibility` | — | HU-34A (Samuel) | **Tier 2 hard** |
| 10 | `HU-33B` | Cierre automático + resultados finales | session-ops | `Facade`, `State`, `Strategy` | SignalR + RabbitMQ | HU-33A (Samuel), **HU-37A (Salomon)** | **Tier 1 hardest** |

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

**Why these are Samuel's:** they contain every Tier 1/2/3 hard HU from the matrix (`HU-21A`,
`HU-33A/B`, `HU-34A/B`, `HU-22`) plus the base roots the rest depends on
(`HU-12 → HU-16`, and `HU-18`/`HU-19`/`HU-21A` feeding `HU-33A`).

---

## SALOMON'S 13 HUs — leaves

Listed by earliest start. "Can start" reflects the **real** Linear graph (done blockers
resolved, out-of-scope blockers stripped).

| HU | Title | Service | Pattern(s) | Transport | Blocked by (trivia-scope, Linear) | Can start |
|----|-------|---------|-----------|-----------|-----------------------------------|-----------|
| `HU-06` | Inicio de sesión de participantes | identity | `Proxy` | — | — (HU-01 ✅) | **done** |
| `HU-07A` | Validación de membresía en sesión | identity | `Proxy` | SignalR | HU-06 ✅ (Salomon) (HU-05 ✅) | **in progress** |
| `HU-07B` | Reconexión autorizada del participante | identity | `Proxy` | SignalR | HU-07A (Salomon) | after own 07A |
| `HU-08` | Sincronización multi-dispositivo | identity | real-time enabler | SignalR | HU-07A, HU-07B (Salomon) | after own 07A/07B |
| `HU-13` | Duplicación y retiro de quizzes | mission-design | `Template Method` | — | HU-12 (**Samuel**) (HU-11 ✅) | after Samuel's 12 |
| `HU-20` | Consulta de sesiones asignadas | identity | `Proxy` | — | HU-19 (**Samuel**) | after Samuel's 19 |
| `HU-21B` | Auditoría de cambios de estado | session-ops | `State` | RabbitMQ | HU-21A (**Samuel**) | after Samuel's 21A |
| `HU-37A` | Ledger de puntaje por respuestas | scoring | `Strategy` | RabbitMQ | **HU-34A (Samuel)** § | after Samuel's 34A |
| `HU-37B` | Actualización de ranking tras cambios | scoring | `Strategy` | SignalR | HU-37A (Salomon) | after own 37A |
| `HU-36A` | Monitoreo restringido resp./no resp. | session-ops | `Proxy` | SignalR | HU-33A, HU-34A (**Samuel**) | after Samuel's 34A |
| `HU-36B` | Revisión post-cierre de resp. y puntos | session-ops | `Proxy` (applies) | SignalR | HU-33B (**Samuel**), HU-37A (Salomon) | after Samuel's 33B + own 37A |
| `HU-39B` | Ranking en tiempo real para trivia | scoring | `Strategy` | SignalR + RabbitMQ | HU-33B (**Samuel**), HU-37A, HU-37B (Salomon) | after Samuel's 33B + own 37A/37B |
| `HU-35` | Revelación de resultado y explicación | session-ops | — (reads closed state) | SignalR | HU-33B (**Samuel**), HU-39B (Salomon) | after Samuel's 33B + own 39B |

`§` = `HU-37A`'s Linear `blockedBy` also includes `HU-30A` (evidence-context validation)
and `HU-38` (penalties) — both **out of scope** — stripped here, leaving only `HU-34A`.

**Salomon's opening move has already happened:** `HU-06` is done, and `HU-07A` is now in
progress. The scoring ledger `HU-37A` still waits on Samuel's `HU-34A` (it is **not** an
early parallel start), so Salomon's active near-term chain remains
`HU-07A → HU-07B → HU-08`.

**Salomon's internal chains** (Salomon-blocked-by-Salomon, fully under his control):
- `HU-06 → HU-07A → HU-07B → HU-08`
- `HU-37A → HU-37B` (chain *starts* only after Samuel's `HU-34A`)
- `HU-39B → HU-35` (both also gated by Samuel's `HU-33B`)

---

## Cross-team dependency summary

The only edges that cross the Samuel/Salomon boundary:

| Blocked HU | Owner | Waits on | Owner | Note |
|---|---|---|---|---|
| `HU-13` | Salomon | `HU-12` | Samuel | leaf off content lifecycle |
| `HU-20` | Salomon | `HU-19` | Samuel | |
| `HU-21B` | Salomon | `HU-21A` | Samuel | |
| `HU-34A` | **Samuel** | `HU-07A` | **Salomon** | ⚠️ Samuel waits on Salomon |
| `HU-37A` | Salomon | `HU-34A` | Samuel | |
| `HU-36A` | Salomon | `HU-33A`, `HU-34A` | Samuel | |
| `HU-33B` | **Samuel** | `HU-37A` | **Salomon** | ⚠️ Samuel waits on Salomon (ping-pong with `HU-34A`) |
| `HU-36B` | Salomon | `HU-33B` | Samuel | + own `HU-37A` |
| `HU-39B` | Salomon | `HU-33B` | Samuel | + own `HU-37A`/`HU-37B` |
| `HU-35` | Salomon | `HU-33B` | Samuel | + own `HU-39B` |

---

## Handoff order (what each side unblocks, and when)

**Current opening state:** Samuel is on `HU-12`; Salomon has finished `HU-06` and is on `HU-07A`.

As Samuel lands spine HUs, they release Salomon's work:

1. **Samuel finishes `HU-12`** → unblocks Salomon's `HU-13`; unblocks Samuel's `HU-16`.
2. **Samuel finishes `HU-16`** → unblocks Samuel's `HU-18` + `HU-19`.
3. **Samuel finishes `HU-19`** → unblocks Salomon's `HU-20`.
4. **Samuel finishes `HU-18` + `HU-19`** → unblocks Samuel's `HU-21A`.
5. **Samuel finishes `HU-21A`** → unblocks Salomon's `HU-21B`; unblocks Samuel's `HU-22`.
6. **Samuel finishes `HU-22`** → unblocks Samuel's `HU-33A`.
7. **Samuel finishes `HU-33A`** (needs Salomon's `HU-07A` already done) → unblocks Samuel's `HU-34A`.
8. **Samuel finishes `HU-34A`** → unblocks Samuel's `HU-34B`, Salomon's `HU-36A`, **and Salomon's `HU-37A`**.
9. **Salomon finishes `HU-37A`** → unblocks Samuel's `HU-33B`; Salomon's `HU-37B`, `HU-36B`.
10. **Samuel finishes `HU-33B`** → unblocks Salomon's `HU-35`, `HU-36B`, `HU-39B` (with his own
    `HU-37A`/`HU-37B`/`HU-39B` prerequisites).

**Sequencing advice:**
- **Salomon: finish `HU-07A` next** so Samuel's future `HU-34A` dependency is cleared well in advance.
- **Samuel: do `HU-34B` while Salomon builds `HU-37A`**, so the `HU-34A → HU-37A → HU-33B`
  ping-pong doesn't leave Samuel idle.

## Dependency map (remaining sprint only)

```
DONE: HU-01 02 03 04 05 06 (identity) · HU-11 14A 14B (mission-design)
IN PROGRESS: HU-12 (Samuel) · HU-07A (Salomon)

SAMUEL (spine, critical path)
--------------------------
HU-12 (in progress) ─▶ HU-16 ─┬─▶ HU-18 ─┐
                └─▶ HU-19 ─┴─▶ HU-21A ─▶ HU-22 ─▶ HU-33A ─┬─▶ HU-34A ─▶ HU-34B
                                                          └─▶ HU-33B
                                                              (HU-33B also needs Salomon's HU-37A)
                                                              (HU-34A also needs Salomon's HU-07A)

SALOMON (leaves)
---------------
done/in progress:  HU-06 ✅ ─▶ HU-07A (in progress) ─┬─▶ HU-07B ─▶ HU-08
                                                     └─(gates Samuel's HU-34A)
after Samuel's 12:   HU-13
after Samuel's 19:   HU-20
after Samuel's 21A:  HU-21B
after Samuel's 34A:  HU-37A ─▶ HU-37B          ·  HU-36A
after Samuel's 33B:  HU-36B (+37A) · HU-39B (+37A/37B) ─▶ HU-35

CROSS-TEAM (boundary-crossing edges)
------------------------------------
Salomon's HU-07A ──▶ Samuel's HU-34A
Samuel's HU-34A ──▶ Salomon's HU-37A ──▶ Samuel's HU-33B      (Samuel → Salomon → Samuel)
```

`✅` = already done · `(Samuel)` / `(Salomon)` = owner of the blocker.
