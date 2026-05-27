# Agent Skills & Workflow Reference — Umbral Backend

This document records the agent, skills, and workflow conventions established for the Umbral backend monorepo.

---

## Agents

### `architect-agent` — `.agents/architect-agent.md`

Architectural authority. Records ADRs, enforces bounded context boundaries, owns canonical docs. Does not write implementation code.

**Invoke for:** cross-service questions, aggregate ownership, canonical doc changes, folder structure changes.

---

### `backend-agent` — `.agents/backend-agent.md`

Implementation authority. Executes one phase at a time per `plans/multi-phase-service-implementation.md`. Enforces layer boundaries, derives everything from canonical docs, moves Linear issues to In Progress / Done.

**Invoke for:** executing any phase, writing or fixing code under `services/*/src/` or `services/*/tests/`.

---

## Skills

### Workflow

| Skill | Folder | Purpose |
|---|---|---|
| `progress` | `.agents/skills/progress/` | Visual checklist of all 16 phases across 4 services. Detects completion via git log, file existence, `dotnet build`, and Linear issue state. Shows commit date, flags drift/broken phases with ⚠️, ends with the next pending phase. |
| `next` | `.agents/skills/next/` | Outputs the exact ready-to-paste prompt for the next pending phase, including a `Ref: DES-NNN` from Linear. Checks for blockers first — if the previous phase is broken or has Linear drift, outputs a warning instead of a prompt. |
| `split-commits-git-flow` | `.agents/skills/split-commits-git-flow/` | Splits a dirty worktree into coherent commits aligned to Git Flow. |
| `zoom-out` | `.agents/skills/zoom-out/` | Produces a module map using the domain glossary vocabulary. Use when unfamiliar with an area. |

### Planning

| Skill | Folder | Purpose |
|---|---|---|
| `prd-to-plan` | `.agents/skills/prd-to-plan/` | Breaks a PRD into tracer-bullet vertical slices, saved as a plan file in `./plans/`. |
| `to-prd` | `.agents/skills/to-prd/` | Converts raw requirements or notes into a structured PRD. |
| `to-issues` | `.agents/skills/to-issues/` | Converts a plan or PRD into Linear issues. |
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

## Prompt & commit protocol

**One prompt = one phase = one commit.**

- Finish a phase → verify (gate passes) → commit → write the next prompt.
- Commit format: `feat(<service-short-name>): phase X.Y — <layer name>` (the `Ref:` from `/next` should appear in the commit body for Linear traceability)
- Use `/next` to get the exact prompt. Use `/progress` to see where you are.

### Phase sequence per service

```
X.1 Domain        → Domain/ only
X.2 Application   → Application/ only
X.3 Infrastructure → Infrastructure/ only
X.4 Api           → Api/ only
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

The backend agent queries Linear at runtime using the service label (`svc:mission-design-service`, etc.) — no hardcoded issue IDs. It moves matching issues to **In Progress** on phase start and **Done** when the verification gate passes.
