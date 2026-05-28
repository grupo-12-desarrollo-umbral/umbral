# DES-66 PRD - Local Backend Platform Baseline For Infrastructure-Backed Bounded-Context Services

Source:
https://linear.app/desarrollo-equipo-12/issue/DES-66/prd-local-backend-platform-baseline-for-infrastructure-backed-bounded

## Problem Statement

The backend architecture already claims four bounded-context services plus
shared platform dependencies, but the current implementation does not yet prove
that architecture in code or runtime.

From the team’s perspective, the current project structure is still mostly
skeletal:

- each backend service exposes only a minimal `/health` endpoint
- `Application` and `Infrastructure` layers are effectively placeholders
- there is no real service-owned persistence
- there is no proven local `PostgreSQL` contract per bounded context
- there is no shared local runtime for `RabbitMQ` and `Keycloak`
- there is no proven public auth path through `api-gateway` and `Keycloak`
- there is no end-to-end proof that the backend can run locally as actual
  bounded-context services rather than as empty API shells

That creates a gap between the accepted architecture and the executable system.
The risk is not only missing features; it is that the project may continue
implementing domain logic on top of an unproven service foundation, making it
harder to defend bounded-context ownership, local reproducibility, migration
strategy, integration boundaries, and service-to-infrastructure contracts
later.

The immediate problem is therefore to convert the current skeletal APIs into
real infrastructure-backed services that can run locally with their own
persistence boundaries and shared platform dependencies, starting with one
service that proves the pattern, then closing the public auth path through the
gateway, and then extending the platform contract to the rest of the stack.

## Solution

The solution is to establish a local backend platform baseline that proves the
architecture incrementally instead of trying to complete every business
workflow at once.

The first step is to make `mission-design-service` the reference
bounded-context service because it is upstream, less coupled than runtime-heavy
services, and already has a defined first implementation scope in `DES-62`.
That service must become a real deployable with:

- a real authoring workflow around the `Mission` aggregate
- service-owned persistence in its own logical `PostgreSQL` database
- service-owned migrations
- containerized startup
- environment-variable-based runtime configuration
- health/readiness behavior that proves real dependency wiring, not just
  process startup

Once that first slice works, the same infrastructure and runtime conventions
will first be used to prove one narrow public entry path through
`api-gateway`, with token validation delegated to `Keycloak` and authenticated
actor context propagated into the already-proven `mission-design-service`
slice. After that, the same infrastructure and runtime conventions will be
generalized across `session-operations-service`,
`scoring-monitoring-service`, and `identity-access-service` so that each
service can boot independently, connect only to its own persistence boundary,
and rely on the same shared local platform services.

The local orchestration baseline will be repository-level `Docker Compose` with
shared `PostgreSQL`, `RabbitMQ`, and `Keycloak`, while preserving one logical
database per backend service and explicit bounded-context ownership.

This PRD is intentionally platform-first. It does not try to finish all domain
functionality across all services. It defines the implementation path that
turns the backend from architectural intent into a locally runnable
microservices platform.

## User Stories

1. As a backend developer, I want the repository to prove its microservices
   architecture locally, so that the codebase matches the design documented in
   ADR 001.
2. As a backend developer, I want one bounded-context service to become fully
   infrastructure-backed first, so that the team can validate the service
   pattern before scaling it across the stack.
3. As a backend developer, I want `mission-design-service` to be the first real
   service slice, so that the initial proof starts from an upstream context
   with lower runtime coupling.
4. As a backend developer, I want `mission-design-service` to persist a real
   `Mission` aggregate, so that the first slice proves bounded-context
   ownership instead of only container startup.
5. As a backend developer, I want to create a `Mission` through the service API
   and retrieve it later, so that I can verify end-to-end authoring
   persistence.
6. As a backend developer, I want `mission-design-service` to own its own
   logical database, so that persistence boundaries remain aligned with
   bounded-context ownership.
7. As a backend developer, I want each backend service to use only its own
   database, so that no service can rely on direct table access into another
   context.
8. As a backend developer, I want one shared local `PostgreSQL` server to host
   multiple logical service databases, so that local development stays simple
   while ownership remains explicit.
9. As a backend developer, I want service-owned migrations, so that schema
   evolution remains local to the owning bounded context.
10. As a backend developer, I want a clear migration execution strategy, so
    that another developer can bring up the stack without undocumented setup
    steps.
11. As a backend developer, I want a repository-level `Docker Compose`
    entrypoint, so that the backend stack can be started consistently from one
    root workflow.
12. As a backend developer, I want shared `PostgreSQL`, `RabbitMQ`, and
    `Keycloak` containers in that workflow, so that the backend uses the
    committed local platform baseline.
13. As a backend developer, I want each backend service to run as its own
    container, so that local runtime reflects the accepted deployable
    boundaries.
14. As a backend developer, I want each service to expose meaningful
    health/readiness checks, so that dependency failures can be isolated to the
    correct deployable.
15. As a backend developer, I want service configuration to come from
    environment variables, so that local execution does not depend on
    IDE-specific machine setup.
16. As a backend developer, I want the same configuration shape across
    services, so that runtime contracts do not drift between bounded contexts.
17. As a backend developer, I want `RabbitMQ` available in the local platform
    even before all integrations are implemented, so that the infrastructure
    contract is established early.
18. As a backend developer, I want `Keycloak` available in the local platform
    even if the first slice does not depend on it critically, so that the
    platform baseline matches the accepted architecture.
19. As a backend developer, I want `mission-design-service` to define the first
    repeatable infrastructure pattern, so that other services can copy proven
    conventions instead of inventing their own.
20. As a backend developer, I want per-service containerization conventions, so
    that all services can be built and run the same way.
21. As a backend developer, I want per-service dependency injection patterns
    for persistence and infrastructure, so that service startup is predictable
    and testable.
22. As a backend developer, I want the first real service slice to include real
    domain, application, infrastructure, and API behavior, so that the
    architecture is proven vertically instead of by layer stubs.
23. As a backend developer, I want `mission-design-service` to expose catalog
    and detail queries for `Mission`, so that the first real slice proves both
    writes and reads.
24. As a backend developer, I want the `Mission` slice to remain within
    authoring concerns, so that runtime authority does not leak in from
    `SessionOperations`.
25. As a backend developer, I want the first slice to stay compatible with
    `DES-62`, so that the service foundation and the domain implementation do
    not diverge.
26. As a backend developer, I want `session-operations-service` to be upgraded
    from a shell to an infrastructure-capable service after the first slice, so
    that the runtime core can later inherit proven conventions.
27. As a backend developer, I want `scoring-monitoring-service` to be upgraded
    from a shell to an infrastructure-capable service after the first slice, so
    that downstream projections and scoring can later plug into a real platform
    baseline.
28. As a backend developer, I want `identity-access-service` to be upgraded
    from a shell to an infrastructure-capable service after the first slice, so
    that identity/access contracts have a real deployment and persistence home.
29. As a backend developer, I want the remaining services to boot successfully
    against shared local infrastructure even before their full business
    features exist, so that the microservice topology is validated
    incrementally.
30. As a backend developer, I want service startup to fail clearly when
    infrastructure dependencies are unavailable or misconfigured, so that
    readiness behavior is operationally meaningful.
31. As a backend developer, I want cross-service communication to remain
    explicit through APIs or messaging, so that shared persistence shortcuts do
    not appear during local development.
32. As a backend developer, I want the first cross-service proof to happen only
    after the services themselves are real, so that infrastructure problems are
    not hidden inside a broader integration effort.
33. As a backend developer, I want one later proof path from `MissionDesign`
    into another bounded context, so that the backend eventually demonstrates
    actual service collaboration instead of isolated service islands.
34. As a backend developer, I want the local backend stack to be stable enough
    for demos and collaborative development, so that setup knowledge is not
    locked to one machine or one person.
35. As a reviewer, I want the local runtime to make service boundaries visible
    in code, configuration, persistence, and orchestration, so that the
    architecture remains defensible.
36. As a future contributor, I want a proven pattern for adding persistence,
    migrations, health checks, and containerization to a new service, so that
    backend evolution stays coherent.
37. As a development team, I want the first tracer bullet to establish deep
    modules and testing patterns, so that later services reuse stable
    abstractions instead of repeating ad hoc infrastructure glue.
38. As a development team, I want the backend platform baseline to be
    implemented before broad frontend integration, so that UI work consumes a
    stable runtime contract instead of masking backend platform gaps.
39. As an operator-facing platform team, I want the backend stack to run
    locally as actual bounded-context services, so that the project can later
    support realistic end-to-end rehearsal and troubleshooting.
40. As the project team, I want the local backend platform to prove the
    accepted platform shape incrementally, so that architecture claims are
    backed by executable evidence.
41. As a backend developer, I want a minimal `api-gateway` route to front the
    first real service slice, so that the public entry topology from ADR 001
    is proven early instead of remaining only a diagram.
42. As a backend developer, I want the gateway to validate tokens against
    `Keycloak` and propagate authenticated actor context downstream, so that
    authentication wiring follows the accepted architecture without forcing
    temporary OIDC plumbing into each service.
43. As a backend developer, I want the first authenticated proof path to target
    the already-proven `MissionDesign` slice, so that gateway/auth integration
    stays narrow while the backend platform is still stabilizing.

## Implementation Decisions

- The local backend baseline will be implemented as a platform-first tracer
  bullet rather than as a simultaneous full-feature build across all services.
- `mission-design-service` will be the first fully real bounded-context service
  because it is upstream, already documented in `DES-62`, and less coupled to
  runtime-heavy workflows than `SessionOperations` or `Identity`.
- The first real vertical slice inside `mission-design-service` will center on
  the `Mission` aggregate and its minimum authoring flow, not on `TriviaQuiz`
  and not on cross-service interactions.
- A service is only considered real when it owns persistent domain data and
  proves that ownership through an externally usable workflow, not merely by
  exposing `/health`.
- The first real proof must include all four service layers behaving
  meaningfully: domain model, application flow, infrastructure persistence, and
  API endpoints.
- `MissionDesign` must preserve the existing separation between authoring
  drafts and source readiness rules. The first platform slice may use draft
  persistence before all readiness rules are fully exercised, but it must
  remain aligned with the already accepted `DES-62` model.
- Repository-level orchestration will use a single root `Docker Compose`
  workflow as the canonical local entrypoint.
- Shared platform services in scope for the baseline are `PostgreSQL`,
  `RabbitMQ`, and `Keycloak`.
- `Keycloak` is treated here as shared platform infrastructure for the local
  stack, not as a signal that every backend service should implement direct
  public OIDC integration with it.
- `Keycloak` belongs in the local platform baseline even if the first
  `mission-design-service` slice does not depend on it critically. This avoids
  designing a temporary platform shape that conflicts with ADR 001.
- The baseline must prove one narrow public auth path through `api-gateway`
  and `Keycloak` immediately after the first real `mission-design-service`
  slice, so the accepted entry topology becomes executable before the rest of
  the services are upgraded.
- For that first public auth path, `api-gateway` is the primary component that
  integrates with `Keycloak`, validates tokens, and propagates trusted actor
  context downstream so temporary OIDC plumbing is not pushed into every
  backend service.
- `RabbitMQ` also belongs in the local platform baseline, but cross-service
  integration flows over the broker are not required for the first proof slice.
- Each backend service remains its own deployable and container;
  containerization must not collapse services into one convenience executable.
- Local persistence will use one shared `PostgreSQL` server with one logical
  database per backend service.
- No backend service may read or write another service’s tables directly, even
  in local development.
- Cross-service collaboration must continue through explicit contracts, not
  shared persistence.
- A shared runtime configuration contract will be defined across services,
  centered on connection strings, broker settings, identity-provider settings,
  and logging.
- Shared platform runtime glue that is reused across services must live in
  shared building blocks rather than under a bounded-context service path, so
  service ownership remains explicit in the repository structure.
- Service startup must be environment-variable driven rather than IDE-profile
  driven.
- Health behavior must distinguish between process liveness and dependency
  readiness strongly enough to support Compose-managed startup and diagnosis.
- Migrations remain owned by each service. The local stack must choose one
  repeatable strategy for applying those migrations without centralizing schema
  ownership.
- The first implementation should extract stable deep modules instead of
  scattering infrastructure glue across startup code.
- The most important deep platform modules are:
  - a service persistence module that encapsulates `DbContext`, service-owned
    database mapping, migration registration, and repository wiring behind a
    stable interface
  - a local runtime configuration module that maps environment configuration
    into strongly owned service settings
  - a health/readiness module that centralizes dependency checks and startup
    expectations per service
  - a container/runtime convention module that standardizes service ports,
    environment contract, and startup behavior across all backend services
- The first real domain module is the `Mission` authoring slice from
  `MissionDesign`, which should serve as the reference vertical slice for how
  bounded-context logic crosses the four clean-architecture layers.
- After `mission-design-service` is proven end to end, the next step will be a
  minimal `api-gateway` proof that fronts that slice, validates tokens through
  `Keycloak`, and propagates authenticated actor context downstream.
- The gateway proof must stay intentionally narrow: one authenticated route,
  one downstream service, and no requirement to complete broad gateway route
  composition first.
- After the gateway-backed auth path is proven, the remaining services will be
  upgraded from shells to infrastructure-capable services even if their
  business features remain minimal at first.
- The order after the first slice will therefore be: minimal gateway auth
  proof, then `session-operations-service`, `scoring-monitoring-service`, and
  `identity-access-service` booting with their own persistence boundaries and
  shared infrastructure contracts before attempting richer service
  collaboration.
- The first cross-service proof should be added only after the services
  themselves are real. Suitable later proofs include a `MissionDesign`
  source-availability contract consumed by `SessionOperations` or one minimal
  broker-driven integration path.
- `api-gateway` is not the first implementation focus of this PRD, but it also
  should not wait until the very end. Gateway work should follow the first
  real service proof closely enough to close the auth-path gap early.
- Frontend and mobile integration are downstream consumers of this runtime
  baseline and should not be used to compensate for missing backend platform
  proof.

## Testing Decisions

- A good test validates observable behavior at the appropriate boundary, not
  internal implementation details or folder structure.
- Domain tests should validate bounded-context invariants and transitions of
  the first real `Mission` slice.
- Application tests should validate command/query orchestration, repository
  usage, and failure behavior for the first `MissionDesign` vertical slice.
- Persistence integration tests should validate that the owning service can
  persist and retrieve its aggregate correctly through its own infrastructure
  and mappings.
- Service-level integration tests should validate health/readiness behavior,
  configuration binding, and startup against real or containerized
  infrastructure dependencies.
- Platform-level verification should validate that the root local orchestration
  can bring up shared infrastructure and that each service can connect only to
  its own dependencies.
- Gateway integration tests should validate one authenticated route end to end:
  token validation against `Keycloak`, gateway admission/denial behavior, and
  trusted actor propagation into `mission-design-service`.
- Ownership tests should validate that each service points only to its own
  logical database and does not rely on direct access to another service’s
  tables.
- Containerization tests should focus on externally observable startup, build,
  and readiness behavior rather than Dockerfile internals.
- The highest-value initial test coverage belongs to:
  - the `Mission` domain model and its immediate application flow
  - the `mission-design-service` persistence module
  - the shared health/readiness behavior for service startup
  - the root local orchestration path for the first real slice
- After the first slice, the next highest-value platform proof is the narrow
  gateway-plus-`Keycloak` auth path because it closes a documented topology gap
  without expanding into full frontend integration.
- After the first slice is proven, each remaining service should receive
  startup/persistence integration tests as it is upgraded from shell to
  infrastructure-capable deployable.
- There is little executable prior art in the current codebase because the
  services are still skeletal. This PRD therefore establishes the reference
  testing shape for real bounded-context services in the repository.
- Existing repository references on backend architecture and testing should be
  treated as normative guidance for behavior-oriented validation, but this
  implementation will create the first concrete local platform prior art.

## Out of Scope

- Completing all business use cases across all four backend services in this
  PRD
- Full `MissionDesign` feature completion beyond the minimum real `Mission`
  slice needed to prove the service foundation
- Rich cross-service workflows before the individual services themselves are
  real
- Broad gateway composition across every backend service before one narrow
  authenticated route is proven
- Frontend and mobile runtime integration before the backend platform contract
  is proven
- Collapsing multiple bounded contexts into a single database model or a single
  executable for local convenience
- Direct table access between services
- Production deployment concerns beyond the local backend runtime baseline
- Advanced monitoring/telemetry stacks beyond the minimum health/readiness and
  local diagnosis needs
- Full broker-driven workflows across services in the first infrastructure
  proof slice
- Finalizing every domain model in `SessionOperations`, `ScoringMonitoring`, or
  `Identity` before those services can at least boot with owned infrastructure

## Further Notes

- This PRD complements rather than replaces `DES-62`. `DES-62` remains the
  product/domain implementation baseline for the first `MissionDesign` slice,
  while this PRD defines the broader local backend platform baseline needed to
  turn that slice into a real service.
- The current repository state strongly indicates that backend implementation
  should not continue as if the platform foundation were already proven. The
  next meaningful steps are to establish one executable bounded-context
  reference service, close the public auth path through the gateway, and then
  generalize that pattern.
- The most important architectural outcome of this PRD is not merely local
  convenience. It is preserving defendable ownership boundaries in code,
  persistence, configuration, and orchestration.
- Success for this PRD means the repository can demonstrate, with executable
  evidence, that its backend is becoming a real microservices platform rather
  than remaining a set of structural placeholders, including the accepted
  public auth entry path through `api-gateway` and `Keycloak`.
