# Migration Plan: current repo → structure.md target tree

## Current state summary

| What exists | Location | Gap |
|---|---|---|
| One service (unnamed, Clean Architecture baseline) | `src/` at repo root | Needs to move under `services/mission-design-service/src/` |
| Template test projects (Todo*, Weather*, Counter, Aspire) | `tests/` at repo root | Mixed naming; full of artifacts to delete |
| `Application` has only `Common/` | `src/Application/Common/` | Missing business feature folders (Missions, MissionPlans, Constraints) |
| `Web.csproj` | `src/Api/` | Named wrong — should be `Api.csproj` |
| No `services/` folder | — | Needs to be created |
| No `docs/` folder | — | Needs to be created |
| `deploy/postgres/` already exists | repo root | Already correct, keep in place |

---

## Phase 1 — Repo-level scaffolding

1. Create `services/` at repo root.
2. Create `docs/` at repo root.
3. Create the four service skeletons under `services/` with the full layer structure matching the concrete target tree:
   - `identity-access-service/{src/{Api,Application/{Common,Users,Roles,Permissions},Infrastructure,Domain,Directory.Build.props,Directory.Packages.props},tests/{UnitTests,IntegrationTests,EndToEndTests}}`
   - `mission-design-service/{src/{Api,Application/{Common,Missions,MissionPlans,Constraints},Infrastructure,Domain,Directory.Build.props,Directory.Packages.props},tests/{UnitTests,IntegrationTests,EndToEndTests}}`
   - `session-operations-service/{src/{Api,Application/{Common,Sessions,SessionRuns,SessionAssignments},Infrastructure,Domain,Directory.Build.props,Directory.Packages.props},tests/{UnitTests,IntegrationTests,EndToEndTests}}`
   - `scoring-monitoring-service/{src/{Api,Application/{Common,Scores,Metrics,Alerts},Infrastructure,Domain,Directory.Build.props,Directory.Packages.props},tests/{UnitTests,IntegrationTests,EndToEndTests}}`

---

## Phase 2 — Move current service code into `mission-design-service`

4. Move `src/Api/` → `services/mission-design-service/src/Api/`
5. Move `src/Application/` → `services/mission-design-service/src/Application/`
6. Move `src/Domain/` → `services/mission-design-service/src/Domain/`
7. Move `src/Infrastructure/` → `services/mission-design-service/src/Infrastructure/`
8. Move `src/Directory.Build.props` and `src/Directory.Packages.props` → `services/mission-design-service/src/`
9. Rename `Api/Web.csproj` → `Api/Api.csproj` and update any internal project references.

---

## Phase 3 — Restructure tests into `mission-design-service/tests/`

| Current project | Action | Target |
|---|---|---|
| `tests/Application.UnitTests/` | move + rename | `mission-design-service/tests/UnitTests/` |
| `tests/Infrastructure.IntegrationTests/` | move + rename | `mission-design-service/tests/IntegrationTests/` |
| `tests/Application.FunctionalTests/` | move + rename | `mission-design-service/tests/EndToEndTests/` |
| `tests/TestAppHost/` | **delete** — Aspire artifact | — |
| `tests/Web.AcceptanceTests/` | **delete** — Counter/Weather/Login template | — |

---

## Phase 4 — Delete template artifacts

10. Delete all `Todo*` test files and folders from `UnitTests` and `EndToEndTests`.
11. Delete `Domain.UnitTests/ValueObjects/ColourTests.cs` — `Colour` is a template value object.
12. Delete `tests/Application.UnitTests/Common/Mappings/MappingTests.cs` — tests AutoMapper mapping for Todo entities.

After deletes, `UnitTests` and `IntegrationTests` will be near-empty but structurally correct stubs.

---

## Phase 5 — Add feature folder stubs for all four services

13. Create feature folder stubs under each service's `src/Application/` matching the concrete target tree:
    - `identity-access-service`: `Users/`, `Roles/`, `Permissions/`
    - `mission-design-service`: `Missions/`, `MissionPlans/`, `Constraints/`
    - `session-operations-service`: `Sessions/`, `SessionRuns/`, `SessionAssignments/`
    - `scoring-monitoring-service`: `Scores/`, `Metrics/`, `Alerts/`

---

## Phase 6 — Fill one missing file

14. Add `Application/Common/Models/LookupDto.cs` — present in the target spec, absent in current code.

---

## Phase 7 — Root cleanup

15. Delete now-empty `src/` and `tests/` from repo root.
16. Add per-service `structure.md` and `README.md` stubs to each service folder (the spec requires both per service).

---

## Phase 8 — Update stub service structure.md files

17. Update `services/identity-access-service/structure.md`, `services/session-operations-service/structure.md`, and `services/scoring-monitoring-service/structure.md` to match the monorepo structure wording instead of the stale "scaffold only" message. Each should read:

```markdown
# <service-name>

This service follows the monorepo target structure.
```

(This mirrors `mission-design-service/structure.md` but drops the "migrated code" part since those three services have no migrated code yet.)

### Canonical sources for DDD implementation

When implementing any DDD-related element — aggregates, entities, value objects, enums, domain events, repository interfaces, or application services — across any service in any phase, the following documents are the authoritative references:

| What you are implementing | Canonical source |
|---|---|
| Aggregate roots and their boundaries | `docs/ddd_solution_model.md` — section 5 (Aggregate map) |
| Application services and use-case names | `docs/ddd_solution_model.md` — section 9 (Application services derived from the backlog) |
| Domain events | `docs/ddd_solution_model.md` — section 6 (Domain events) |
| Repository interfaces | `docs/ddd_solution_model.md` — section 7 (Repository interfaces) |
| Domain services and policies | `docs/ddd_solution_model.md` — section 8 (Domain services and policies) |
| Entity fields, value objects, enums, and per-entity constraints | `docs/bd_umbral_entity_spec.md` |

Do not invent aggregate names, field names, enum values, or repository method signatures that are not traceable to one of these two documents. If a concept appears in `ddd_solution_model.md` but its fields are not yet detailed in `bd_umbral_entity_spec.md`, leave a `// TODO: align with bd_umbral_entity_spec.md` comment rather than guessing.

---

## What is not touched

- `deploy/postgres/` — already correct location, no changes needed.
- `README.md` — kept at root.
- `.gitignore` — kept at root.
- The three stub services (`identity-access-service`, `session-operations-service`, `scoring-monitoring-service`) get the full directory skeleton (`Api`, `Application`, `Domain`, `Infrastructure`, `Directory.Build.props`, `Directory.Packages.props`, `README.md`, `structure.md`) but no source files until those bounded contexts are started.
