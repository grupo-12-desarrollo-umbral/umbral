---
name: progress
description: Show a visual checklist of all implementation phases across all 4 services. Detects completed phases from git log, file existence, dotnet build, and Linear issue state. Use when you want to know where you are in the plan.
---

# Progress

Show the current implementation state of all phases across all 4 services.

## How to detect phase completion

Run all four checks. A phase is **done** only when all four pass. Mark it ⚠️ if any check disagrees with the others and note the discrepancy inline.

### 1. Git log — commit exists for this phase
```
git log --oneline | grep -i "<service-short-name>.*phase X\.Y"
```
Service short names: `mission-design`, `identity-access`, `scoring-monitoring`, `session-operations`

Extract the commit date with:
```
git log --format="%ad %s" --date=short | grep -i "<service-short-name>.*phase X\.Y"
```
Use this date in the output.

### 2. File existence — expected output is present and non-empty

| Phase | Key path to check |
|---|---|
| X.1 Domain | `services/<svc>/src/Domain/Entities/` |
| X.2 Application | `services/<svc>/src/Application/Common/` |
| X.3 Infrastructure | `services/<svc>/src/Infrastructure/Persistence/` |
| X.4 Api | `services/<svc>/src/Api/Program.cs` |

### 3. Build health — layer project compiles now

Run `dotnet build` on the relevant project and check the exit code:

| Phase | Project to build |
|---|---|
| X.1 Domain | `services/<svc>/src/Domain/` |
| X.2 Application | `services/<svc>/src/Application/` |
| X.3 Infrastructure | `services/<svc>/src/Infrastructure/` |
| X.4 Api | `services/<svc>/src/` (full solution) |

A phase with a passing commit but a failing build is marked ⚠️ broken.

### 4. Linear state — issue matches git

Query Linear for issues labelled `svc:<service-label>` in team **umbral-equipo-12**.

| Service | Label |
|---|---|
| `mission-design-service` | `svc:mission-design-service` |
| `identity-access-service` | `svc:identity-access-service` |
| `scoring-monitoring-service` | `svc:scoring-monitoring-service` |
| `session-operations-service` | `svc:session-operations-service` |

- If git says done but the Linear issue is still Backlog or In Progress → mark ⚠️ Linear drift
- If Linear says Done but git has no commit → mark ⚠️ Linear drift

---

## Service order and phase map

Check in this exact order:

1. `mission-design-service` (phases 1.1 → 1.4)
2. `identity-access-service` (phases 2.1 → 2.4)
3. `scoring-monitoring-service` (phases 3.1 → 3.4)
4. `session-operations-service` (phases 4.1 → 4.4)

---

## Output format

Print a single checklist block. No prose, no extra headers.

Symbols:
- `[x]` — all four checks pass
- `[!]` — at least one check disagrees (show what failed inline)
- `[ ]` — not started

```
mission-design-service
  [x] 1.1 Domain        — 2026-05-20
  [!] 1.2 Application   — 2026-05-21  ⚠️ build failing
  [ ] 1.3 Infrastructure
  [ ] 1.4 Api

identity-access-service
  [ ] 2.1 Domain
  [ ] 2.2 Application
  [ ] 2.3 Infrastructure
  [ ] 2.4 Api

scoring-monitoring-service
  [ ] 3.1 Domain
  [ ] 3.2 Application
  [ ] 3.3 Infrastructure
  [ ] 3.4 Api

session-operations-service
  [ ] 4.1 Domain
  [ ] 4.2 Application
  [ ] 4.3 Infrastructure
  [ ] 4.4 Api

Next: 1.3 Infrastructure — mission-design-service
```

The last line always states the next fully-unblocked phase. If a phase is ⚠️, flag it before the Next line:
```
⚠️  1.2 Application is broken — fix before proceeding.
Next: 1.2 Application — mission-design-service
```

If all phases are done, print `All phases complete.`
