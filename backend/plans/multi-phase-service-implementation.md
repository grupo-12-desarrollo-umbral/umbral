# Multi-phase implementation plan — Umbral backend services

**Canonical references for all implementation decisions:**
- Field-level entity specs: `docs/bd_umbral_entity_spec.md`
- Aggregate map, domain events, repository interfaces, domain services, application services: `docs/ddd_solution_model.md` sections 5–9
- Folder layout and layer rules: `structure.md`

---

## Prompt & commit protocol

**One prompt covers exactly one phase. Nothing more.**

- Finish a phase → verify it → commit → then write the next prompt.
- Never combine phases in a single prompt, even if they feel small.
- Each commit message must reference the phase: `feat(mission-design): phase 1.1 — domain layer`.

### Verification gate per phase

| Phase | Must pass before committing |
|---|---|
| X.1 Domain | Project compiles with no errors; all domain types resolve |
| X.2 Application | `dotnet build` clean; handler unit tests green |
| X.3 Infrastructure | `dotnet ef migrations add` succeeds; repository integration test green |
| X.4 Api | At least one endpoint returns expected response via `curl` or HTTP test |

If a phase fails its gate, fix it in the **same prompt session** before committing. Do not carry broken state into the next phase.

---

## Service order

1. `mission-design-service` — two clean aggregate roots, no real-time, best scaffold baseline
2. `identity-access-service` — auth foundation needed by all other services
3. `scoring-monitoring-service` — read-model heavy, moderate complexity
4. `session-operations-service` — largest aggregate, real-time, evidence flows

---

## Per-service phase template

Every service is implemented in four sequential phases before moving to the next service.

---

### Phase X.1 — Domain layer

Derive all of the following exclusively from the canonical docs:

| What to build | Where to look |
|---|---|
| Aggregate roots and their fields | `bd_umbral_entity_spec.md` — section for this bounded context |
| Child entities and their fields | `bd_umbral_entity_spec.md` — section for this bounded context |
| Value objects and enums | `bd_umbral_entity_spec.md` — *Supporting value objects and enums* table |
| Domain events | `ddd_solution_model.md` — section 6, subsection for this bounded context |
| Domain services and policies | `ddd_solution_model.md` — section 8, subsection for this bounded context |
| Domain exceptions | one exception class per invariant stated in the *Key constraints* rows of `bd_umbral_entity_spec.md` |

Output: `Domain/Entities/`, `Domain/Events/`, `Domain/ValueObjects/`, `Domain/Enums/`, `Domain/Exceptions/`, `Domain/Services/`

---

### Phase X.2 — Application layer

| What to build | Where to look |
|---|---|
| Repository interfaces | `ddd_solution_model.md` — section 7, subsection for this bounded context |
| Commands and queries | `ddd_solution_model.md` — section 9, application services for this bounded context |
| Handlers | one handler per command or query derived above |
| DTOs | shaped from the aggregate fields in `bd_umbral_entity_spec.md` |
| `Application/Common` behaviours, exceptions, interfaces, models | `structure.md` — *Application/Common* section (same baseline for all services) |

Output: `Application/Common/`, `Application/<Feature>/Commands/`, `Application/<Feature>/Queries/`, `Application/<Feature>/Handlers/`, `Application/<Feature>/DTOs/`

---

### Phase X.3 — Infrastructure layer

| What to build | Where to look |
|---|---|
| EF Core entity configurations | entity fields and relationships in `bd_umbral_entity_spec.md` |
| Repository implementations | interfaces established in Phase X.2 |
| `ApplicationDbContext` | aggregates owned by this bounded context per `ddd_solution_model.md` section 3 |
| Interceptors | `structure.md` — `Infrastructure/Persistence/Interceptors/` |
| Keycloak wiring (identity service only) | `structure.md` — `Infrastructure/Identity/Keycloak/` |
| SignalR notifier (session-operations only) | `structure.md` — `Infrastructure/Realtime/` |
| RabbitMQ outbound publisher (session-operations, scoring-monitoring) | minimum workflow in `ddd_solution_model.md` section 10 |

Output: `Infrastructure/Persistence/`, `Infrastructure/Identity/` (where applicable), `Infrastructure/Realtime/` (where applicable), `Infrastructure/Integrations/` (where applicable)

---

### Phase X.4 — Api layer

| What to build | Where to look |
|---|---|
| Endpoint groups | one `<Feature>Endpoints.cs` per feature folder created in Phase X.2 |
| SignalR hub (session-operations only) | `structure.md` — `Api/Hubs/` |
| `CurrentUserService` adapter | `structure.md` — `Api/Services/` |
| `Program.cs` and `DependencyInjection.cs` | `structure.md` — `Api` layer |

Output: `Api/Endpoints/`, `Api/Hubs/` (where applicable), `Api/Services/`, `Api/Program.cs`

---

## Service-specific notes

**`mission-design-service`** — start here; bounded context section in both docs is the most self-contained. No real-time or messaging concerns.

**`identity-access-service`** — `JoinToken` ownership rationale is in `bd_umbral_entity_spec.md` under the `JoinToken` entity. Keycloak wiring is the main infrastructure emphasis.

**`scoring-monitoring-service`** — `Ranking` and `AuditHistory` are projection-oriented; treat as read-model aggregates, not write-side roots per `ddd_solution_model.md` section 5. Minimum RabbitMQ consumption contract described in section 10.

**`session-operations-service`** — single aggregate root `LiveSession` owns all child entities listed in `ddd_solution_model.md` section 3. Four read-model projections (`TeamBoardProjection`, `OperatorDashboardProjection`, `EvidenceReviewQueueProjection`, `SessionAuditTrailProjection`) are specified in `bd_umbral_entity_spec.md` — *Supporting read models*. Minimum RabbitMQ publication (`EvidenceSubmissionRegistered`) described in `ddd_solution_model.md` section 10.
