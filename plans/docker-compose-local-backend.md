# Plan: Docker Compose Local Backend Environment

> Source PRD: `umbral-backend/docs/docker-compose-implementation-plan.md`

## Architectural decisions

Durable decisions that apply across all phases:

- **Services**: The local stack centers on `mission-design-service`, `session-operations-service`, `scoring-monitoring-service`, and `identity-access-service`. `api-gateway` remains out of scope until the service containers are stable.
- **Shared infrastructure**: Local orchestration uses one `docker-compose.yml` at the repository root, backed by shared `postgres`, `rabbitmq`, and `keycloak` containers.
- **Persistence boundary**: Each backend service owns its own logical persistence boundary, with one PostgreSQL database per service on the same local PostgreSQL server/container.
- **Configuration contract**: Services consume runtime configuration from environment variables through a consistent contract centered on `ConnectionStrings:Default`, `RabbitMQ:*`, `Keycloak:*`, and logging settings.
- **Verification path**: Each service must remain independently reachable and verifiable through `/health` during local startup.
- **Boundary enforcement**: No service may access another service's tables directly; cross-service collaboration continues through service contracts and shared infrastructure, not shared persistence.
- **Frontend relationship**: `umbral-frontend` is a downstream consumer of the backend stack and should be added only after the backend container/runtime contract is stable enough for one local web entrypoint to depend on it.

---

## Phase 1: First Service Running End-to-End

**User stories**: A developer can run one real backend service from the repository root with Docker Compose, confirm it builds in a container, reaches its own database, and responds on `/health`.

### What to build

Establish the first thin end-to-end slice by taking a single backend service, preferably `mission-design-service`, from source code to a working Compose-managed container. This slice should prove the root orchestration model, the service-level Dockerfile pattern, the runtime configuration contract, and the isolated-database rule without yet solving the same problem for every service.

### Acceptance criteria

- [ ] A root-level Compose workflow can build and start one backend service plus its required shared infrastructure.
- [ ] The selected service exposes `/health` successfully from the host machine.
- [ ] The selected service resolves configuration from the shared environment-driven contract rather than IDE-only settings.
- [ ] The selected service connects only to its own PostgreSQL database.

---

## Phase 2: Template The Remaining Services

**User stories**: A developer can build and boot all four backend services with the same container and configuration conventions, while preserving bounded-context separation.

### What to build

Generalize the proven first-service pattern across `session-operations-service`, `scoring-monitoring-service`, and `identity-access-service`. This slice should make the service contract uniform enough that every backend service can be built and started the same way from the repository root, while still keeping each service as an independent deployable unit.

### Acceptance criteria

- [ ] All four backend services have a consistent container entrypoint and runtime configuration approach.
- [ ] Each service can start under Compose without relying on service-specific manual boot steps outside the shared workflow.
- [ ] Each service continues to expose its own `/health` endpoint for local verification.
- [ ] The rollout preserves one microservice per bounded context with no collapsed service boundaries.

---

## Phase 3: Shared Infrastructure Contract

**User stories**: A developer can boot shared local infrastructure once and have every backend service resolve the same PostgreSQL, RabbitMQ, and Keycloak contract through Compose.

### What to build

Stabilize the shared infrastructure layer so the services all speak the same local runtime language. This slice defines the infrastructure services, service hostnames, healthchecks, root support files, and environment contract that every backend service depends on in local development.

### Acceptance criteria

- [ ] `postgres`, `rabbitmq`, and `keycloak` are defined as shared infrastructure in the root Compose workflow.
- [ ] Each backend service receives its dependency settings through the same environment-variable structure.
- [ ] Infrastructure containers expose the expected local ports and reach a healthy state during startup.
- [ ] Service-to-infrastructure resolution works through Compose-network hostnames rather than host-specific machine settings.

---

## Phase 4: Persistence Ownership Verified Per Service

**User stories**: A developer can confirm each service owns exactly one logical persistence boundary, can initialize its schema, and does not rely on another service's tables.

### What to build

Complete the persistence path inside each backend service by wiring its owning `DbContext`, local mappings, and migrations strategy. This slice is not just about adding persistence assets; it verifies that the local Compose environment preserves the architectural rule that each service owns its database boundary and evolves it independently.

### Acceptance criteria

- [ ] Each backend service has its own persistence setup pointing only to its own database.
- [ ] Mappings and migrations remain local to the owning service.
- [ ] The local migration strategy is defined clearly enough for repeatable setup in development.
- [ ] Validation confirms that no backend service requires direct access to another service's tables.

---

## Phase 5: Local Demo Stack Ready

**User stories**: A developer can run `docker compose up --build` from the repository root and get a demoable backend environment with healthy infrastructure and healthy APIs.

### What to build

Turn the previous slices into a reliable local demo path. This slice focuses on startup sequencing, readiness validation, support files such as `.dockerignore` and optional local environment templates, and a repeatable verification pass that proves the stack is usable by other developers without hidden setup knowledge.

### Acceptance criteria

- [ ] `docker compose up --build` from the repository root starts the shared infrastructure and the four backend services successfully.
- [ ] All four backend services respond successfully on `/health`.
- [ ] The local stack can be brought up by another developer using the documented root workflow and support files.
- [ ] The resulting environment is stable enough for backend development and demo readiness.

---

## Phase 6: Gateway Integration Later

**User stories**: A developer can add the API gateway after the backend service containers and internal Compose networking are already stable.

### What to build

Introduce the gateway only after the backend services have a stable local contract. This slice keeps gateway containerization and routing concerns intentionally deferred so that issues in service startup, persistence, or shared infrastructure are solved before a public entrypoint is layered on top.

### Acceptance criteria

- [ ] Gateway containerization is explicitly deferred until the four backend services are stable in Compose.
- [ ] The later gateway slice is defined against Compose-network service hostnames, not ad hoc local URLs.
- [ ] Adding the gateway does not require changing the persistence-boundary rules established in earlier phases.
- [ ] The deferred gateway work remains compatible with the root-level Compose orchestration model.

---

## Phase 7: Frontend Consumes The Local Stack

**User stories**: A developer can start `umbral-frontend` against the local backend environment and verify one real browser-facing flow using the stabilized backend contract.

### What to build

Add the frontend as a consumer slice only after the backend services, shared infrastructure, and optional gateway path are stable enough to present one reliable local API surface. This slice should define the frontend runtime contract for local development, wire the web app to the Compose-backed backend environment, and prove that at least one real user-facing flow can run against the local stack without manual endpoint rewiring on every machine.

### Acceptance criteria

- [ ] `umbral-frontend` has a clear local runtime contract for connecting to the Compose-backed backend environment.
- [ ] Frontend local configuration resolves against stable backend URLs rather than ad hoc developer-specific settings.
- [ ] At least one real frontend flow can be exercised successfully against the local backend stack.
- [ ] Adding the frontend does not force changes to the backend bounded-context or persistence-boundary rules established in earlier phases.
