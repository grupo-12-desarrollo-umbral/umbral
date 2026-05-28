# Backend Implementation Workflow

This document defines the end-to-end workflow for building the Umbral backend using agents, skills, and the Linear backlog.

---

## Foundational rules

- Linear tracks only **HU tickets** (user stories). No phase issues, no AFK slices.
- PRDs reference existing HU tickets — they do not create new ones.
- The backend-agent executes one phase at a time, one service at a time.
- Every commit carries `Ref: HU-XX, HU-YY, ...` linking back to the user stories.
- `/debrief` records non-obvious decisions after every phase commit.

## Issue tracker routing

| Tool | Target | Output |
|---|---|---|
| `/to-prd` | Linear + `docs/prd/<slug>.md` locally | `DES-XX` PRD issue + local markdown copy |
| `/to-issues` | GitHub (`gh issue create` on the correct repo) | GitHub issues — one per AFK vertical slice |

**Never use `/to-issues` to create Linear issues. Never use `/to-prd` to create GitHub issues.**

---

## Step 1 — Create a PRD per service (one-time, before any code)

For each service that does not yet have a PRD:

1. Read the HU tickets for the service in Linear
2. Read `docs/ddd_solution_model.md` and `docs/bd_umbral_entity_spec.md`
3. Run `/to-prd` → publishes `DES-XX` PRD to Linear

The PRD bridges the user story requirements with the technical decisions derived from the canonical docs. It references the relevant HU tickets but does not create new ones.

**PRD status per service:**

| Service | HU tickets | PRD |
|---|---|---|
| `mission-design-service` | HU-09 to HU-14 | DES-62 ✓ |
| `identity-access-service` | HU-01 to HU-08 | pending |
| `scoring-monitoring-service` | HU-37 to HU-40 | pending |
| `session-operations-service` | HU-15 to HU-36 | pending |

---

## Step 2 — Implement each service (4 phases × 4 services)

Build order: `mission-design-service` → `identity-access-service` → `scoring-monitoring-service` → `session-operations-service`

### Branching (git-flow)

```
main        ← production-ready releases only
develop     ← integration branch — all feature PRs merge here
  └── feature/<service-short-name>   ← one per service, all 4 phases commit here
```

- Feature branches cut from `develop`: `feature/mission-design`, `feature/identity-access`, etc.
- All 4 phase commits land on the feature branch.
- After phase 1.4 gate, agent opens a **draft PR** from `feature/<name>` → `develop`.
- PR description includes `Closes #N` for each GitHub feature slice issue.
- You promote `develop` → `main` via a release branch when ready to ship.

**Setup required once:** `git checkout -b develop main && git push -u origin develop`

### Before phase 1.1 only

Create the feature branch. Query Linear for HU tickets matching `svc:<service>` + `ready-for-agent`. Move all matched tickets to **In Progress**. Carry these HU IDs and GitHub issue numbers through all four phases.

### Each phase

```
Read: service PRD + canonical docs + plans/multi-phase-service-implementation.md
    ↓
Execute phase X.Y
    ↓
Verification gate passes
    ↓
Commit:
  feat(<service-short-name>): phase X.Y — <layer name>

  Ref: HU-XX, HU-YY, ...   ← Linear user stories
  Ref: #1, #2, #3           ← GitHub issues (feature slices)
    ↓
/debrief → services/<svc>/decisions/untracked.md
```

### After phase 1.4 only

1. Open a **draft PR** from the feature branch to `develop`. PR description must include `Closes #1, Closes #2, Closes #3` — GitHub auto-closes the feature slice issues on merge.
2. Before moving any Linear HU ticket to **Done**, verify its acceptance criteria are met by the phase 1.4 gate. Phase 1.4 passing is necessary but not sufficient — every HU ticket's criteria must be independently confirmed. Only move tickets whose criteria are actually covered.

### Verification gates

| Phase | Gate |
|---|---|
| X.1 Domain | `dotnet build` on Domain project exits 0 |
| X.2 Application | `dotnet build` clean; at least one handler unit test green |
| X.3 Infrastructure | `dotnet ef migrations add Init` succeeds; repository integration test green |
| X.4 Api | At least one endpoint returns expected response via HTTP test or `curl` |

---

## Summary

| Step | Tool | Output |
|---|---|---|
| Create PRD | `/to-prd` | `DES-XX` in Linear |
| Execute phase | `backend-agent` | Code + green gate |
| Commit | git | `Ref: HU-XX` in commit body |
| Record decisions | `/debrief` | Entry in `untracked.md` |
| Close stories | Linear | HU tickets → Done |

---

## Traceability chain

```
docs/ddd_solution_model.md
docs/bd_umbral_entity_spec.md
    +
HU-XX tickets (Linear — user stories)
    ↓
DES-XX PRD (Linear — /to-prd)
    ↓
phase 1.1 commit  Ref: HU-XX
phase 1.2 commit  Ref: HU-XX
phase 1.3 commit  Ref: HU-XX
phase 1.4 commit  Ref: HU-XX
    ↓
services/<svc>/decisions/untracked.md  (/debrief)
```

---

## Key constraints

- Do not use `/to-issues` for backend service implementation — the phase plan already defines the breakdown.
- Do not create phase-level issues in Linear — HU tickets are the only trackable unit.
- Do not combine phases even if both feel small.
- If a verification gate fails, fix it in the same session before stopping.
