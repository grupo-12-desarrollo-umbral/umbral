# Agent Skills & Workflow Reference — Umbral Backend

This document records the agent, skills, and workflow conventions established for the Umbral backend monorepo.

For the full step-by-step workflow see `docs/current_workflow.md`.

---

## Agents

### `architect-agent` — `.agents/architect-agent.md`

Architectural authority. Records ADRs, enforces bounded context boundaries, owns canonical docs. Does not write implementation code.

**Invoke for:** cross-service questions, aggregate ownership, canonical doc changes, folder structure changes.

---

### `backend-agent` — `.agents/backend-agent.md`

Implementation authority. Executes one phase at a time per `plans/multi-phase-service-implementation.md`. Enforces layer boundaries, derives everything from canonical docs, manages Linear HU ticket state and GitHub issue references, follows git-flow.

**Invoke for:** executing any phase, writing or fixing code under `services/*/src/` or `services/*/tests/`.

---

## Skills

### Workflow

| Skill | Folder | Purpose |
|---|---|---|
| `debrief` | `.agents/skills/debrief/` | Appends a structured decision entry to `services/<svc>/decisions/untracked.md` after finishing a phase. Captures what was built, why, what was skipped, which HU tickets were advanced, and what the next session needs to know. Run as `/debrief`. |
| `split-commits-git-flow` | `.agents/skills/split-commits-git-flow/` | Splits a dirty worktree into coherent commits aligned to git-flow. |
| `zoom-out` | `.agents/skills/zoom-out/` | Produces a module map using the domain glossary vocabulary. Use when unfamiliar with an area. |

### Planning

| Skill | Folder | Purpose |
|---|---|---|
| `prd-to-plan` | `.agents/skills/prd-to-plan/` | Breaks a PRD into tracer-bullet vertical slices, saved as a plan file in `./plans/`. |
| `to-prd` | `.agents/skills/to-prd/` | Synthesizes context into a PRD, publishes it to Linear (`DES-XX`), and saves a local copy to `docs/prd/DES-XX-<slug>.md`. Never creates GitHub issues. |
| `to-issues` | `.agents/skills/to-issues/` | Breaks a PRD into vertical slices and creates GitHub issues (`gh issue create`) in the current repo. Never creates Linear issues. |
| `grill-with-docs` | `.agents/skills/grill-with-docs/` | Cross-examines an implementation or proposal against the canonical docs. |
| `prototype` | `.agents/skills/prototype/` | Scaffolds a quick throwaway prototype to validate an idea. |
| `write-a-skill` | `.agents/skills/write-a-skill/` | Writes a new skill file following the established format. |

### Technical

| Skill | Folder | Purpose |
|---|---|---|
| `cqrs-mediatr-aspnetcore` | `.agents/skills/cqrs-mediatr-aspnetcore/` | CQRS commands, queries, handlers, pipeline behaviours with MediatR in ASP.NET Core. |
| `ef-core-postgresql` | `.agents/skills/ef-core-postgresql/` | EF Core entity configurations, migrations, DbContext with PostgreSQL. |
| `aspnet-backend-testing` | `.agents/skills/aspnet-backend-testing/` | Unit and integration tests for handlers and repositories. |
| `rabbitmq-events-dotnet` | `.agents/skills/rabbitmq-events-dotnet/` | Outbound event publishing and consumer wiring with RabbitMQ in .NET. |
| `signalr-websockets-aspnetcore` | `.agents/skills/signalr-websockets-aspnetcore/` | Hub setup, group management, real-time notifier implementation with SignalR. |

---

## Issue tracker routing

| Tool | Target | Output |
|---|---|---|
| `/to-prd` | Linear + `docs/prd/` | `DES-XX` issue in Linear + `docs/prd/DES-XX-<slug>.md` locally |
| `/to-issues` | GitHub (`gh issue create`) | GitHub issues — one per AFK vertical slice |

**Rule:** Linear tracks only HU user stories and PRDs. GitHub Issues track implementation-level feature slices.

---

## Prompt & commit protocol

**One prompt = one phase = one commit.**

- Finish a phase → gate passes → commit → run `/debrief`.
- Commit format: `feat(<service-short-name>): phase X.Y — <layer name>`
- Commit body must include two `Ref:` lines:
  ```
  Ref: HU-XX, HU-YY, ...   ← Linear user stories
  Ref: #N, #M, ...          ← GitHub feature slice issues
  ```
- Phase 1.4 PR description must include `Closes #N` for each GitHub issue — auto-closes on merge to `develop`.
- To resume work on a service, read the last 3 entries of `services/<svc>/decisions/untracked.md`.

### Git-flow branching

```
main        ← production releases only
develop     ← integration branch — all feature PRs merge here
  └── feature/<service-short-name>   ← one per service, all 4 phases commit here
```

- Feature branches cut from `develop`: `feature/mission-design`, `feature/identity-access`, etc.
- Agent opens a **draft PR** from `feature/<name>` → `develop` after phase 1.4 gate passes.
- `develop` → `main` via a release branch when ready to ship.

### Phase sequence per service

```
X.1 Domain         → Domain/ only
X.2 Application    → Application/ only
X.3 Infrastructure → Infrastructure/ only
X.4 Api            → Api/ only
```

### Service build order

```
1. mission-design-service
2. identity-access-service
3. scoring-monitoring-service
4. session-operations-service
```

---

## Linear backlog

Team: **umbral-equipo-12** — workspace `desarrollo-equipo-12`

Linear tracks only HU (user story) tickets and PRDs — no phase-level issues, no AFK slices.

**State transitions:**
- Phase 1.1 starts → query `svc:<service>` + `ready-for-agent` → move matched HU tickets to **In Progress**
- Phases 1.2 and 1.3 → no state change; HU IDs appear in every commit's `Ref:` field
- Phase 1.4 gate passes → verify each HU ticket's acceptance criteria are met → move only verified tickets to **Done**

**PRD local copies:** `docs/prd/DES-XX-<slug>.md` — authoritative local reference for the backend agent.
