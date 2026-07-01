# FAQ — Workflow & Sprint Planning

---

## Q: What is the difference between `docs/current_workflow.md` and `plans/multi-phase-service-implementation.md`?

They serve different purposes and are complementary, not competing.

| | `current_workflow.md` | `multi-phase-service-implementation.md` |
|---|---|---|
| Scope | End-to-end process orchestration | Technical build spec per phase |
| Covers | Branching, issue routing, coverage gate, PR opening, Linear ticket lifecycle | What to build, which canonical docs to consult, output folders |
| Audience | "How do I run the process?" | "What exactly do I implement in phase X.Y?" |

`current_workflow.md` is the primary reference. `multi-phase-service-implementation.md` fills in the *what to build* detail per phase.

**Reading order:** `current_workflow.md` for process decisions → `multi-phase-service-implementation.md` when sitting down to implement a phase.

---

## Q: If I want to work a Linear backlog ticket (e.g. HU-01), what do I do?

HU-01 belongs to `identity-access-service` (HU-01 to HU-08), the 2nd service in build order. `mission-design-service` must be fully done first.

When ready:

1. **Create PRD** (one-time): run `/to-prd` after reading HU tickets + canonical docs → publishes `DES-XX` to Linear.
2. **Create feature branch**: `git checkout -b feature/identity-access develop`
3. **Fetch the ready backlog for the service** in Linear (`svc:identity-access-service` + `ready-for-agent`) and resolve only the HU ticket(s) for the slice you are starting.
4. **Move only those resolved HU ticket(s) to In Progress** in Linear.
5. **Run 4 phases** in order for that slice — each phase: implement → gate passes → commit with `Ref: HU-XX` for the resolved slice ticket(s).
6. **After phase 1.4**: coverage gate (≥95%) → open draft PR → verify the resolved slice ticket(s)' acceptance criteria → move only covered ticket(s) to Done.

---

## Q: Concrete example — how do I complete HU-09?

HU-09 is part of `mission-design-service` (HU-09 to HU-14). PRD DES-62 already exists. Branch `feature/mission-design-service` already created.

**Steps:**

1. Move HU-09 to HU-14 → **In Progress** in Linear.
2. **Phase 1.1** — implement `Domain/` layer → `dotnet build` exits 0 → commit:
   ```
   feat(mission-design): phase 1.1 — domain layer
   Ref: HU-09, HU-10, HU-11, HU-12, HU-13, HU-14
   ```
3. **Phase 1.2** — Application layer → build clean + handler unit test green → commit
4. **Phase 1.3** — Infrastructure → EF migration succeeds + repo integration test green → commit
5. **Phase 1.4** — Api layer → endpoint responds + run coverage gate:
   ```bash
   dotnet test --coverage --coverage-output-format cobertura
   python .agents/skills/aspnet-backend-testing/scripts/check_cobertura_threshold.py merged.cobertura.xml 95
   ```
   → open draft PR → verify each HU ticket's acceptance criteria → move to Done.

---

## Q: Does `current_workflow.md` work service-level or ticket-by-ticket?

**Service-level.** All HU tickets for a service (e.g. HU-09 to HU-14) are implemented together across 4 phases — not one ticket at a time.

This is because Clean Architecture requires all layers before any single user story is fully delivered (an endpoint needs entity + handler + repository + API layer).

The HU tickets are **acceptance criteria** checked at the end, not sequential work items.

| Unit | Purpose |
|---|---|
| HU ticket | Acceptance criteria to verify after phase 1.4 |
| Phase (1.1–1.4) | Actual unit of implementation work |
| Service | The complete deliverable |

---

## Q: Wouldn't ticket-by-ticket (HU-by-HU) be better for testability and traceability?

Partially yes. Vertical slices per HU (all 4 layers for one story at a time) give story-level traceability and earlier testability. The trade-off:

| | Phase-based | HU-by-HU |
|---|---|---|
| Traceability | Phase-level | Story-level |
| Testability | Only at phase 1.4 | After each HU |
| Domain model | Designed complete upfront | Grows incrementally (rework risk) |
| Token cost | Lower (docs loaded once per phase) | Higher (docs re-read per HU) |

For DDD projects, the domain model needs to be designed holistically — aggregate boundaries and invariants across all HU tickets inform each other. Designing HU-by-HU risks inconsistency and multiple migrations.

**Resolution:** keep the 4-phase structure but scope commits inside each phase to individual HUs:
```
feat(mission-design): CreateMission command + endpoint   Ref: HU-09
feat(mission-design): UpdateMission command + endpoint   Ref: HU-10
```

This gives story-level git traceability without the token cost or DDD quality risk.

---

## Q: Which approach consumes fewer tokens and gives the best implementation quality?

**Phase-based wins on both.**

- **Token efficiency:** canonical docs (`ddd_solution_model.md`, `bd_umbral_entity_spec.md`, PRD) are loaded once per phase for all HUs — not once per HU per session.
- **Implementation quality:** the domain model is designed as a whole. No incremental entity patches, no repeated migrations, no cross-HU inconsistency.

Fix the traceability gap at commit granularity (one commit per handler/endpoint, each referencing its specific HU) rather than by changing the workflow.

---

## Q: For a trivia sprint (frontend + backend + mobile), which tickets do I need?

Trivia requires ~17 tickets across 4 services. Dependency order:

**1. Auth & Identity**
| Ticket | What |
|---|---|
| HU-01 | Inicio de sesión general |
| HU-06 | Inicio de sesión de participantes |
| HU-07A | Validación de membresía en sesión |
| HU-07B | Reconexión autorizada |

**2. Quiz design** (`mission-design-service`)
| Ticket | What | Blocked by |
|---|---|---|
| HU-11 | Crear/editar quizzes | nothing — start here |
| HU-14A | Gestión de preguntas y opciones | HU-11 |
| HU-14B | Validación de preguntas | HU-14A |
| HU-12 | Publicar/archivar quiz | HU-11 |

**3. Session setup** (`session-operations-service`)
| Ticket | What | Blocked by |
|---|---|---|
| HU-16 | Crear sesión trivia | HU-12 |
| HU-18 | Asociar equipos a sesión | HU-16 |
| HU-19 | Asignar operador a sesión | HU-16 |
| HU-21A | Transiciones válidas de estado | HU-16 |
| HU-22 | Temporizador autoritativo | HU-21A |

**4. Trivia gameplay loop**
| Ticket | What | Blocked by |
|---|---|---|
| HU-33A | Orquestación automatizada por rondas | HU-16, HU-21A, HU-22 |
| HU-33B | Cierre automático + resultados finales | HU-33A |
| HU-34A | Registrar primera respuesta del equipo | HU-33A |
| HU-34B | Rechazar respuestas tardías/repetidas | HU-34A |
| HU-35 | Revelar resultado y explicación | HU-33B |
| HU-36A | Monitoreo operador (respondido/no) | HU-34A |

**5. Scoring** (`scoring-monitoring-service`)
| Ticket | What | Blocked by |
|---|---|---|
| HU-39B | Ranking en tiempo real trivia | HU-33B |

**6. Mobile**
| Ticket | What |
|---|---|
| ENABLER | Cliente móvil React Native |

Critical path: `HU-11 → HU-14A → HU-14B → HU-12 → HU-16 → HU-21A → HU-22 → HU-33A → HU-33B → HU-34A → HU-35 → HU-39B`

---

## Q: Is trivia created from a Mission?

Trivia is **authored into a `Mission`**. A trivia round is a `Substage` of the mission that
selects one whole published `TriviaQuiz` (`TriviaQuizSelection`); `Mission` is the **only**
`SessionSource`. A `TriviaQuiz` cannot create a `LiveSession` on its own, and there is no
session-level `sessionMode`.

```
Mission (→ trivia Substage → whole TriviaQuiz)  →  the only source of a LiveSession
```

At creation, `LiveSession` freezes the full runtime plan — including every trivia question and
option — into the immutable `MissionRuntimeSnapshot`.

> **Superseded (DES-75 / HU-16):** the earlier DES-23 model — `TriviaQuiz` as a standalone
> session source with `sessionMode = Trivia` — is retired. Since HU-15/HU-17 there is a single
> mission source per session and no quiz-as-source creation route.

---

## Q: Should I create a "Sprint 1" category in the backlog that copies the trivia tickets?

No — avoid duplicating tickets. Use **Linear Cycles** (native sprint feature) instead.

A Cycle assigns existing tickets to a sprint without copying them. Tickets stay in the backlog; the cycle just tracks them. When a ticket is done, it closes everywhere.

**To set up:**
- Linear UI → team → **Cycles** → **New Cycle** → name "Sprint 1" → set dates → drag the 17 trivia tickets in.

If you prefer a simpler option without dates, apply a `sprint-1` **label** to the relevant tickets — same filtered view, less overhead than copying.

> As of 2026-05-29, no cycles exist in the workspace yet — must be created manually in the Linear UI.
