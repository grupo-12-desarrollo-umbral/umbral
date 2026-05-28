# Plan: Local Backend Platform Baseline (DES-66)

> Source PRD: `docs/prd/DES-66-local-backend-platform-baseline.md`

## Architectural decisions

Durable decisions that apply across all phases:

- **Auth**: Gateway-only JWT validation. Downstream services never parse tokens.
  `CurrentUser` reads `X-User-Id`, `X-User-Role`, `X-User-Email` HTTP headers
  injected by the gateway (ADR-0001). Only `identity-access-service` carries a
  Keycloak SDK dependency (ADR-0003).
- **Routes**: all public traffic enters through `api-gateway`.
  `/api/missions/` → `mission-design-service`,
  `/api/identity/` → `identity-access-service`,
  `/api/sessions/` and `/hubs/` → `session-operations-service`,
  `/api/scoring/` → `scoring-monitoring-service`.
- **Persistence**: one shared PostgreSQL server; one logical database per
  service. No service reads or writes another service's tables.
- **Migrations**: service-owned EF Core migrations applied at startup via
  `InitialiseDatabaseAsync`. Migration strategy must be reproducible without
  undocumented setup steps.
- **Orchestration**: root `docker-compose.yml` is the canonical local
  entrypoint. Each service is its own container. Platform services (PostgreSQL,
  RabbitMQ, Keycloak) are always present even before all integrations exist.
- **Health/readiness**: every service must distinguish liveness from dependency
  readiness. A health endpoint that returns 200 without probing PostgreSQL does
  not count.
- **Configuration**: environment-variable driven; no IDE-profile secrets in
  committed config files.

---

## Phase 1: Proven public auth path

**User stories**: 11, 12, 13, 15, 17, 18, 41, 42, 43

### What to build

Close the "no proven public auth path" gap with the smallest possible scope.

`mission-design-service`'s `CurrentUser` service currently reads JWT claims
directly from the `HttpContext` principal. Flip it to read the three trusted
headers (`X-User-Id`, `X-User-Role`, `X-User-Email`) that the gateway injects.
Remove the `IHttpContextAccessor` JWT claim reads; replace with header reads.
This is the only source change needed in the service itself.

Add a multi-stage Dockerfile for `api-gateway`: `dotnet publish` in an SDK
image, copy the output into a runtime image, expose port 8080.

Write the root `docker-compose.yml` under the repository root. It must define:
- `postgres` — shared PostgreSQL server
- `keycloak` — Keycloak with the `umbral` realm
- `rabbitmq` — RabbitMQ (present in the baseline before broker flows exist)
- `api-gateway` — built from its Dockerfile, depends on `keycloak`
- `mission-design-service` — built from its Dockerfile, depends on `postgres`

Environment variables for each service must be the sole configuration source.
Keycloak realm and client configuration (`umbral-web`, `umbral-mobile`, realm
roles `Administrador`/`Operador`/`Participante`) must be importable via a
committed realm-export file so setup is reproducible.

### Acceptance criteria

- [ ] `mission-design-service` `CurrentUser.Id` returns the value from
  `X-User-Id` header, not from a JWT claim
- [ ] `mission-design-service` `CurrentUser.Roles` returns the value from
  `X-User-Role` header
- [ ] `api-gateway` builds successfully from its Dockerfile
- [ ] `docker compose up` starts postgres, keycloak, rabbitmq, api-gateway, and
  mission-design-service without manual steps beyond the command itself
- [ ] A request with a valid Keycloak-issued JWT reaches
  `mission-design-service` through the gateway with `X-User-Id` set
- [ ] A request with no token or an invalid token is rejected at the gateway
  with 401 and does not reach the service

---

## Phase 2: `mission-design-service` — first real service slice

**User stories**: 2, 3, 4, 5, 6, 7, 8, 9, 10, 14, 19, 20, 21, 22, 23, 24,
25, 36, 37

### What to build

Make `mission-design-service` a real bounded-context service that owns
persistent domain data and proves that ownership through an externally usable
authoring workflow.

**Domain layer**: `Mission` aggregate root with its identity, name,
description, and status (draft). Value objects and domain events as needed by
the authoring invariants from DES-62. No `TriviaQuiz`, no cross-service
concerns.

**Application layer**: `CreateMission` command (produces a new `Mission` in
draft state), `GetMissions` query (catalog list), `GetMissionById` query
(detail). All wired through MediatR with the existing pipeline behaviors.

**Infrastructure layer**: EF Core entity configuration for `Mission` mapped to
the service's own logical database. First EF Core migration. Repository wiring
behind `IApplicationDbContext`.

**Api layer**: three endpoints — `POST /api/missions`, `GET /api/missions`,
`GET /api/missions/{id}`. Replace the static `/health` response with a real
dependency health check that probes the PostgreSQL connection.

**Containerization**: `mission-design-service` Dockerfile (same multi-stage
pattern as the gateway). Update `docker-compose.yml` to build and run the real
image.

### Acceptance criteria

- [ ] `POST /api/missions` with a valid payload persists a `Mission` row in
  the service's database and returns the created resource
- [ ] `GET /api/missions` returns the catalog including the previously created
  mission
- [ ] `GET /api/missions/{id}` returns the detail for a known mission; returns
  404 for an unknown id
- [ ] EF Core migration runs automatically on startup; the schema exists before
  the first request
- [ ] `/health` returns unhealthy (not 200) when PostgreSQL is unreachable
- [ ] `mission-design-service` builds and runs from its Dockerfile inside
  Compose
- [ ] The full authoring flow (create → list → detail) is reachable through the
  gateway using a valid Keycloak token

---

## Phase 3: Gateway integration test

**User stories**: testing decisions — gateway auth path

### What to build

One test project (or suite within an existing test project) that validates the
gateway's auth path end-to-end against the running Compose stack. The test must
not mock the gateway or Keycloak; it must exercise the real token validation
path.

Two scenarios are required:
1. Valid Keycloak-issued JWT → request is admitted, `X-User-Id` / `X-User-Role`
   / `X-User-Email` are present on the downstream request, `Authorization`
   header is absent downstream.
2. Missing or tampered JWT → gateway returns 401, request does not reach the
   downstream service.

### Acceptance criteria

- [ ] Test project exists and runs with `dotnet test`
- [ ] Valid-token scenario passes: admission confirmed, all three trusted
  headers present, `Authorization` absent downstream
- [ ] Invalid-token scenario passes: 401 returned, no downstream hit
- [ ] Tests are runnable against the Compose stack from Phases 1–2 without
  additional manual setup

---

## Phase 4: `session-operations-service` baseline

**User stories**: 26, 29, 30

### What to build

Upgrade `session-operations-service` from a `.gitkeep` skeleton to an
infrastructure-capable service that can boot, connect to its own logical
database, and fail clearly when PostgreSQL is unavailable.

Apply the same Clean Architecture scaffolding and infrastructure conventions
established in Phases 1–2:
- Own `DbContext` with its own logical database and connection string
- Domain base types, application layer boilerplate, infrastructure persistence
  wiring
- Initial EF Core migration (empty schema is acceptable; the service does not
  need domain features yet)
- Real PostgreSQL health/readiness check
- Dockerfile using the established multi-stage pattern
- Added to `docker-compose.yml` with its own database environment variables

No SignalR hub implementation, no broker consumer, no business features are
required in this phase. The service must boot and prove persistence ownership.

### Acceptance criteria

- [ ] Service starts inside Compose and passes its health check when PostgreSQL
  is available
- [ ] Service fails its readiness check (not a crash) when PostgreSQL is
  unreachable
- [ ] Service's DbContext connects only to its own logical database; no
  reference to another service's connection string
- [ ] Initial migration creates the service's schema on first boot
- [ ] `docker compose up` brings up the service alongside all previously added
  containers

---

## Phase 5: `scoring-monitoring-service` baseline

**User stories**: 27, 29, 30

### What to build

Apply the same infrastructure-capable baseline from Phase 4 to
`scoring-monitoring-service`:
- Own `DbContext`, logical database, connection string
- Domain base types, application layer boilerplate, infrastructure persistence
  wiring
- Initial EF Core migration
- Real PostgreSQL health/readiness check
- Dockerfile and Compose entry

No scoring domain features required. The service must boot and prove persistence
ownership.

### Acceptance criteria

- [ ] Service starts inside Compose and passes its health check when PostgreSQL
  is available
- [ ] Service fails its readiness check when PostgreSQL is unreachable
- [ ] Own logical database; no cross-service schema access
- [ ] Initial migration runs on first boot
- [ ] `docker compose up` starts the full six-service stack cleanly

---

## Phase 6: `identity-access-service` baseline

**User stories**: 28, 29, 30

### What to build

Apply the same infrastructure-capable baseline from Phases 4–5 to
`identity-access-service`, with one addition: this is the only service that
carries the Keycloak SDK dependency (ADR-0003). Wire the Keycloak admin or
OIDC client under `Infrastructure/Identity/Keycloak/` even if no full
`AuthenticateUser` flow is implemented yet.

- Own `DbContext`, logical database, connection string
- Domain base types, application layer boilerplate, infrastructure persistence
  wiring
- Initial EF Core migration
- Keycloak SDK registered under Infrastructure; no other service carries this
  dependency
- Real PostgreSQL health/readiness check
- Dockerfile and Compose entry

No full identity/user-provisioning features required. The service must boot,
own its persistence boundary, and have the Keycloak wiring in place as the
established home for that dependency.

### Acceptance criteria

- [ ] Service starts inside Compose and passes its health check
- [ ] Own logical database; no cross-service schema access
- [ ] Keycloak SDK dependency exists only in `identity-access-service`; no
  other service references it
- [ ] Initial migration runs on first boot
- [ ] Full seven-container Compose stack (postgres, keycloak, rabbitmq,
  api-gateway, and all four services) starts with `docker compose up` and
  all health checks pass
