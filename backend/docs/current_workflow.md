# Backend Implementation Workflow

This document defines the end-to-end workflow for building Umbral by feature slice while preserving the backend phase gates and service boundaries.

---

## Foundational rules

- The delivery unit is a **feature slice**, not a whole service.
- A feature slice is the thinnest user-meaningful flow you can verify end-to-end.
- A slice may touch `frontend/` plus one or more backend services under `backend/services/`.
- Linear tracks **HU tickets**. Do not create phase issues in Linear.
- Backend implementation still happens **one phase at a time** and **one service at a time**.
- Every backend commit carries `Ref: HU-XX, HU-YY, ...` linking back to the user stories.

## What counts as a slice

Good slices:

- `trivia-quiz-authoring`
- `trivia-quiz-publish`
- `participant-rejoin-session`
- `operator-live-scoreboard`

Bad slices:

- `mission-design-service`
- `session-operations-service`
- `frontend-cleanup`

A service is a bounded context, not a feature. Branches and PRs should be named after the behavior being delivered.

## Issue tracker routing

| Tool | Target | Output |
|---|---|---|
| `/to-prd` | Linear + `docs/prd/<slug>.md` locally | `DES-XX` PRD issue + local markdown copy |
| `/to-issues` | GitHub (`gh issue create` on the correct repo) | GitHub issues — one per feature slice |

**Never use `/to-issues` to create Linear issues. Never use `/to-prd` to create GitHub issues.**

---

## Step 1 — Create PRDs per backend service

PRDs are still backend-service scoped because the canonical domain model is service scoped.

For each backend service that does not yet have a PRD:

1. Read the HU tickets in Linear for that service.
2. Read `docs/ddd_solution_model.md` and `docs/bd_umbral_entity_spec.md`.
3. Run `/to-prd` to publish `DES-XX` to Linear and save the local markdown copy.

The PRD is not the delivery slice. It is the backend reference document for the service that a slice may touch.

After the PRD exists, fetch the ready backlog for the service and resolve only the HU ticket(s) needed for the current vertical slice. Do not move the entire service backlog unless the explicit plan is to implement the whole service in one batch.

**PRD status per service:**

| Service | HU tickets | PRD |
|---|---|---|
| `mission-design-service` | HU-09 to HU-14 | DES-62 |
| `identity-access-service` | HU-01 to HU-08 | pending |
| `scoring-monitoring-service` | HU-37 to HU-40 | pending |
| `session-operations-service` | HU-15 to HU-36 | pending |

---

## Step 2 — Plan the feature slice

Before writing code:

1. Pick the HU or small HU bundle that forms one testable slice.
2. Identify the touched surfaces:
   - `frontend/`
   - `backend/services/<svc>/...`
3. Prefer slices that touch one backend service plus the frontend.
4. If the acceptance criteria require multiple backend services, keep one branch and one PR for the slice, but execute backend work service-by-service.
5. If a proposed slice is really a service scaffold with no user-visible outcome, rename it as an enabler and do not pretend it is a feature.

Examples:

| Slice | Frontend | Backend services |
|---|---|---|
| `trivia-quiz-authoring` | admin quiz screens | `mission-design-service` |
| `participant-rejoin-session` | participant join flow | `identity-access-service`, `session-operations-service` |
| `operator-live-scoreboard` | operator dashboard | `session-operations-service`, `scoring-monitoring-service` |

---

## Step 3 — Branching and PR model

### Branching

```text
main        ← production-ready releases only
develop     ← integration branch
  └── feature/<slice-slug>   ← one branch per feature slice
```

Use names like:

- `feature/trivia-quiz-authoring`
- `feature/trivia-quiz-publish`
- `feature/participant-rejoin-session`

Avoid names like:

- `feature/mission-design-service`
- `feature/scoring-monitoring-service`

### PR shape

- One draft PR per feature slice branch.
- The PR may include both `frontend/` and `backend/` changes.
- The PR description must state the user-visible outcome and list the touched backend services.
- If GitHub issues exist for the slice, include `Closes #N`.

**Setup required once:** `git checkout -b develop main && git push -u origin develop`

---

## Step 4 — Execution model inside a slice

The slice is horizontal from the product perspective, but backend implementation remains phased.

### If the slice touches frontend and one backend service

Typical order:

1. Prepare the branch and move the relevant HU tickets to **In Progress**.
2. Implement the backend service through phases X.1 → X.4.
3. Implement or finish the frontend against the verified backend contract.
4. Verify the end-to-end slice.
5. Open or update the draft PR.

### If the slice touches frontend and multiple backend services

Use one branch, but do backend work in isolated service rounds:

1. Backend service A: X.1 → X.4
2. Backend service B: X.1 → X.4
3. Frontend integration and end-to-end validation

Do not mix backend phases in one prompt. Do not blur service boundaries because the branch is shared.

---

## Backend phase protocol

For backend work, keep the existing gate discipline:

```text
Read: relevant service PRD + canonical docs + plans/multi-phase-service-implementation.md
    ↓
Execute one backend phase
    ↓
Verification gate passes
    ↓
Commit backend changes
```

### Verification gates

| Phase | Gate |
|---|---|
| X.1 Domain | `dotnet build` on Domain project exits 0 |
| X.2 Application | `dotnet build` clean; at least one handler unit test green |
| X.3 Infrastructure | `dotnet ef migrations add Init` succeeds; repository integration test green |
| X.4 Api | At least one endpoint returns expected response via HTTP test or `curl`; **aggregate coverage ≥95% (see below)** |

**Coverage gate:** the backend must reach **≥93% line coverage and ≥93% branch coverage**, measured as an aggregate across all of the touched service's test projects combined. This remains a hard gate for each backend service completed inside the slice.

Collect and enforce coverage through the canonical gate script (per ADR-0005), which chains coverlet across the service's test projects and fails `dotnet test` if either line or branch coverage falls short:

- `make gate SVC=<service>` — runs `backend/scripts/cover-gate.sh` and renders `coverage/gate/Summary.txt` from the exact gated file
- `make gate-all` — runs the gate for every discovered service

Exclude only true wiring such as `Program.cs`, DI extension methods, and generated EF migrations. Never exclude Domain or Application logic to make the number pass.

---

## Commit model

### Backend commits

Backend commits stay phase-based because that is how the code is verified.

Format:

```text
feat(<service-short-name>): phase X.Y — <layer name>

Ref: HU-XX, HU-YY, ...
Ref: #N, #M
```

Example on branch `feature/trivia-quiz-authoring`:

```text
feat(mission-design): phase 2.1 — domain layer
feat(mission-design): phase 2.2 — application layer
feat(mission-design): phase 2.3 — infrastructure layer
feat(mission-design): phase 2.4 — api layer
```

### Frontend commits

Frontend commits should describe the slice behavior, not a backend service:

```text
feat(frontend): trivia quiz authoring screens
feat(frontend): trivia publish flow
```

The branch name binds the frontend and backend commits into one slice.

---

## Ticket state workflow

### Before backend phase 1.1 for the first touched service

Query the relevant HU tickets and move them to **In Progress**.

- For single-service slices, query by `svc:<service>` plus the target HU IDs.
- For multi-service slices, query by the exact HU IDs, not by one service label alone.

Carry those HU IDs through all backend phase commits and the final PR.

### When to move tickets to Done

Move a HU ticket to **Done** only when:

1. The required backend phase X.4 gate has passed for every touched backend service.
2. The frontend behavior for the slice is working where applicable.
3. The HU acceptance criteria are independently verified.

Phase X.4 passing is necessary, not sufficient.

---

## Traceability chain

```text
docs/ddd_solution_model.md
docs/bd_umbral_entity_spec.md
    +
HU-XX tickets
    ↓
DES-XX service PRD(s)
    ↓
feature/<slice-slug> branch
    ↓
backend phase commits + frontend feature commits
    ↓
draft PR to develop
    ↓
verified HU acceptance criteria
```

---

## Key constraints

- Plan and branch by feature slice, not by whole backend service.
- Keep backend execution one phase at a time and one service at a time.
- Do not combine backend phases in a single prompt.
- Do not create phase-level issues in Linear.
- Do not use a service name as a substitute for a feature name.
- If a backend verification gate fails, fix it in the same session before moving on.
