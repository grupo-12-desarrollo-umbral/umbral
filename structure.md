# Project Structure

## Purpose

This document defines the baseline folder structure for a .NET microservices monorepo using:

- Clean Architecture
- DDD boundaries per service
- CQRS with MediatR
- Entity Framework Core
- Keycloak integration for authentication/identity
- SignalR for real-time notifications

The goal is to preserve a consistent structure without carrying template/sample domain boilerplate.

---

## Monorepo Tree

```text
umbral-backend/
├── services/
│   ├── <service-name>/
│   │   ├── src/
│   │   │   ├── Api/                                             # Service entry point and transport concerns
│   │   │   │   ├── Endpoints/                                   # Minimal API endpoint groups
│   │   │   │   │   ├── HealthEndpoints.cs
│   │   │   │   │   ├── <Entity>Endpoints.cs
│   │   │   │   │   └── WebhookEndpoints.cs                      # Optional inbound webhook endpoints
│   │   │   │   ├── Hubs/                                        # SignalR hubs
│   │   │   │   │   └── WebhookHub.cs
│   │   │   │   ├── Services/                                    # Adapters for request context
│   │   │   │   ├── DependencyInjection.cs
│   │   │   │   ├── Program.cs
│   │   │   │   ├── GlobalUsings.cs
│   │   │   │
│   │   │   ├── Application/                                     # Use cases and application policies
│   │   │   │   ├── Common/
│   │   │   │   │   ├── Behaviours/
│   │   │   │   │   │   ├── AuthorizationBehaviour.cs
│   │   │   │   │   │   ├── ValidationBehaviour.cs
│   │   │   │   │   │   ├── LoggingBehaviour.cs
│   │   │   │   │   │   ├── PerformanceBehaviour.cs
│   │   │   │   │   │   └── UnhandledExceptionBehaviour.cs
│   │   │   │   │   ├── Exceptions/
│   │   │   │   │   │   ├── ForbiddenAccessException.cs
│   │   │   │   │   │   ├── NotFoundException.cs
│   │   │   │   │   │   └── ValidationException.cs
│   │   │   │   │   ├── Interfaces/
│   │   │   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   │   │   ├── ICurrentUser.cs
│   │   │   │   │   │   ├── IIdentityService.cs
│   │   │   │   │   │   ├── IClock.cs
│   │   │   │   │   │   ├── INotifier.cs
│   │   │   │   │   │   └── IWebhookDispatcher.cs
│   │   │   │   │   ├── Models/
│   │   │   │   │   │   ├── Result.cs
│   │   │   │   │   │   ├── PagedResult.cs
│   │   │   │   │   │   └── LookupDto.cs
│   │   │   │   │   ├── Security/
│   │   │   │   │   │   ├── AuthorizeAttribute.cs
│   │   │   │   │   └── Mappings/
│   │   │   │   │       └── MappingProfile.cs                     # Only if AutoMapper is kept
│   │   │   │   │
│   │   │   │   ├── <Entity>/                                     # Example: Orders, Customers, Billing
│   │   │   │   │   ├── Commands/
│   │   │   │   │   │   ├── Create<Entity>/
│   │   │   │   │   │   │   ├── Create<Entity>Command.cs
│   │   │   │   │   │   │   └── Create<Entity>CommandValidator.cs
│   │   │   │   │   │   ├── Update<Entity>/
│   │   │   │   │   │   │   ├── Update<Entity>Command.cs
│   │   │   │   │   │   │   └── Update<Entity>CommandValidator.cs
│   │   │   │   │   │   └── Delete<Entity>/
│   │   │   │   │   │       └── Delete<Entity>Command.cs
│   │   │   │   │   ├── Queries/
│   │   │   │   │   │   ├── Get<Entity>ById/
│   │   │   │   │   │   │   ├── Get<Entity>ByIdQuery.cs
│   │   │   │   │   │   │   └── <Entity>DetailsDto.cs
│   │   │   │   │   │   └── Get<Entity>List/
│   │   │   │   │   │       ├── Get<Entity>ListQuery.cs
│   │   │   │   │   │       └── <Entity>ListItemDto.cs
│   │   │   │   │   ├── Handlers/
│   │   │   │   │   │   ├── Create<Entity>CommandHandler.cs
│   │   │   │   │   │   ├── Update<Entity>CommandHandler.cs
│   │   │   │   │   │   ├── Delete<Entity>CommandHandler.cs
│   │   │   │   │   │   ├── Get<Entity>ByIdQueryHandler.cs
│   │   │   │   │   │   └── Get<Entity>ListQueryHandler.cs
│   │   │   │   │   ├── DTOs/
│   │   │   │   │   │   ├── <Entity>Dto.cs
│   │   │   │   │   │   └── <Entity>SummaryDto.cs
│   │   │   │   │   ├── Events/                                   # Optional application events
│   │   │   │   │   │   └── <Entity>CreatedEvent.cs
│   │   │   │   │   └── EventHandlers/                            # Optional application event handlers
│   │   │   │   │       └── <Entity>CreatedEventHandler.cs
│   │   │   │   │
│   │   │   │   ├── DependencyInjection.cs
│   │   │   │   ├── GlobalUsings.cs
│   │   │   │   └── Application.csproj
│   │   │   │
│   │   │   ├── Domain/                                          # Core domain model for this service only
│   │   │   │   ├── Common/
│   │   │   │   │   ├── BaseEntity.cs
│   │   │   │   │   ├── BaseAuditableEntity.cs
│   │   │   │   │   ├── BaseEvent.cs
│   │   │   │   │   └── ValueObject.cs
│   │   │   │   ├── Constants/
│   │   │   │   │   └── Roles.cs
│   │   │   │   ├── Entities/
│   │   │   │   │   └── <Entity>.cs
│   │   │   │   ├── Events/
│   │   │   │   │   └── <DomainEvent>.cs
│   │   │   │   ├── ValueObjects/
│   │   │   │   │   └── <ValueObject>.cs
│   │   │   │   ├── Enums/
│   │   │   │   │   └── <Enum>.cs
│   │   │   │   ├── Exceptions/
│   │   │   │   │   └── <DomainException>.cs
│   │   │   │   ├── Services/                                    # domain services
│   │   │   │   │   └── <DomainService>.cs
│   │   │   │   ├── GlobalUsings.cs
│   │   │   │   └── Domain.csproj
│   │   │   │
│   │   │   ├── Infrastructure/                                  # External implementations
│   │   │   │   ├── Identity/
│   │   │   │   │   ├── Keycloak/
│   │   │   │   │   │   ├── KeycloakOptions.cs
│   │   │   │   │   │   ├── KeycloakClaimMapper.cs
│   │   │   │   │   │   ├── KeycloakJwtEvents.cs
│   │   │   │   │   │   └── KeycloakRoleTranslator.cs
│   │   │   │   │   └── DependencyInjection.cs
│   │   │   │   ├── Persistence/
│   │   │   │   │   ├── ApplicationDbContext.cs
│   │   │   │   │   ├── ApplicationDbContextInitialiser.cs       # Optional
│   │   │   │   │   ├── Configurations/
│   │   │   │   │   │   └── <Entity>Configuration.cs
│   │   │   │   │   ├── Interceptors/
│   │   │   │   │   │   ├── AuditableEntityInterceptor.cs
│   │   │   │   │   │   └── DispatchDomainEventsInterceptor.cs
│   │   │   │   │   ├── Migrations/
│   │   │   │   │   ├── Repositories/                           # Optional
│   │   │   │   │   │   └── <Entity>Repository.cs
│   │   │   │   │   └── DependencyInjection.cs
│   │   │   │   ├── Realtime/
│   │   │   │   │   ├── SignalRNotifier.cs
│   │   │   │   │   └── DependencyInjection.cs
│   │   │   │   ├── Integrations/
│   │   │   │   │   └── Webhooks/
│   │   │   │   │       ├── WebhookDispatcher.cs
│   │   │   │   │       ├── WebhookPayloadFactory.cs
│   │   │   │   │       └── WebhookOptions.cs
│   │   │   │   ├── DependencyInjection.cs
│   │   │   │   ├── GlobalUsings.cs
│   │   │   │   └── Infrastructure.csproj
│   │   │   │
│   │   │   ├── Directory.Build.props
│   │   │   └── Directory.Packages.props
│   │   │
│   │   ├── tests/
│   │   │   ├── UnitTests/
│   │   │   ├── IntegrationTests/
│   │   │   └── EndToEndTests/
│   │   │
│   │   ├── README.md
│   │   └── structure.md
│   │
│   └── <another-service>/
│       └── ...
│
├── docs/
├── deploy/
├── .gitignore
└── README.md
```

---

## DDD and Boundary Rules

### Bounded Contexts

- Each folder under `services/` is a separate microservice and a separate bounded context.
- Every service owns its own `Domain`, `Application`, `Infrastructure`, and `Api`.
- Do not create a shared domain across services.
- Cross-service collaboration must happen through explicit integration boundaries, not by referencing another service's domain model.

### Domain Layer

- `Domain` contains the business model of the service.
- Entities, value objects, domain events, enums, and domain exceptions belong here.
- Domain code must not depend on `Application`, `Infrastructure`, or `Api`.
- Domain services should exist only when domain logic does not naturally belong to a single entity or value object.
- `Constants/` is allowed in `Domain` when the constants are part of the service's business language.

### Application Layer

- `Application` orchestrates use cases.
- Authorization decisions for business operations belong here, even when authentication is handled externally.
- `Commands`, `Queries`, and `Handlers` are separated by design in this baseline.
- Handlers depend on abstractions from `Application/Common/Interfaces`, never on transport concerns.
- Application events and their handlers are optional and should be used only when they help coordinate internal workflows.

### Infrastructure Layer

- `Infrastructure` contains implementations for persistence, identity integration, notifications, and external integrations.
- `Infrastructure/Identity/Keycloak` is for integration with Keycloak or an external identity service, not for business rules.
- `Infrastructure/Persistence` contains EF Core concerns.
- Repositories are optional. If EF Core with `IApplicationDbContext` is sufficient, do not create repository abstractions without a clear reason.

### Api Layer

- `Api` is the transport layer for HTTP and SignalR.
- Endpoints should delegate to application use cases and avoid embedding business rules.
- `CurrentUser` is an adapter from HTTP context to application abstractions.
- Inbound webhooks belong in `Api/Endpoints`; outbound webhooks belong in `Infrastructure/Integrations/Webhooks`.

---

## Dependency Direction

```text
Domain
  ^
Application
  ^
Infrastructure
  ^
Api
```

Rules:

- `Application` depends on `Domain`
- `Infrastructure` depends on `Application`
- `Api` depends on `Application` and `Infrastructure`
- `Domain` depends on nothing inside the solution

---

## Folder Responsibilities

### `Api`

- Hosts the service
- Exposes HTTP endpoints
- Exposes SignalR hubs
- Maps request context into application abstractions
- Configures middleware and composition root concerns

### `Application/Common`

- Cross-cutting pipeline behaviors
- Application exceptions
- Interfaces required by use cases
- Shared result models
- Authorization metadata and permissions
- Mapping profiles if object mappers are used

### `Application/<Entity>`

- Commands for state changes
- Queries for reads
- Request handlers
- DTOs for use-case outputs
- Optional application events and handlers

This structure is entity-oriented by convention, but the folder name should still reflect a meaningful business concept inside the service.

### `Domain`

- Core service model
- Business invariants
- Domain events
- Business language

### `Infrastructure/Identity`

- Keycloak configuration
- Claims translation
- JWT event hooks
- Identity service adapters

### `Infrastructure/Persistence`

- EF Core `DbContext`
- Entity configurations
- Interceptors
- Migrations
- Optional repository implementations

### `Infrastructure/Realtime`

- Notification implementations backed by SignalR

### `Infrastructure/Integrations`

- Outbound webhook delivery
- Payload translation
- Other external clients if needed later

---

## Conventions

### Naming

- Use singular or plural entity folders consistently per service.
- Endpoint files should be named `<Entity>Endpoints.cs`.
- Handler files should be named after the request they handle, for example `CreateOrderCommandHandler.cs`.
- Validators should live next to their command when applicable.

### Commands and Queries

- Commands mutate state.
- Queries read state.
- One handler should handle one command or one query.
- Request and handler files are intentionally separated in this baseline.

### DTOs

- Shared application DTOs for an entity can live in `Application/<Entity>/DTOs/`.
- Query-specific DTOs can live next to the query that owns them.
- Keep whichever approach is chosen consistent inside each service.

### Events

- Domain events belong in `Domain/Events`.
- Application events belong in `Application/<Entity>/Events` only when a use case needs internal workflow fan-out after completion.
- Event handlers for application events belong in `Application/<Entity>/EventHandlers` only when those application events exist.
- Do not create these folders by default just because the service uses MediatR, CQRS, or SignalR.

---

## What This Baseline Avoids

The baseline should not include sample/template artifacts such as:

- `Todo*` entities, endpoints, handlers, and tests
- `WeatherForecast*`
- template seed data
- generated OpenAPI output committed as boilerplate
- SPA fallback or frontend publish steps unless a service actually owns a frontend
- stale Aspire/AppHost-specific files if the monorepo is not using them

It should also avoid DDD-hostile shortcuts such as:

- sharing one domain model across services
- coupling one service directly to another service's persistence model
- putting business authorization rules only in the identity provider
- pushing transport concerns into `Application` or `Domain`

---

## Notes

- This document is a reference structure, not a rule that every optional folder must always exist.
- Folders such as `EventHandlers`, `Repositories`, `Mappings`, `Realtime`, or `Webhooks` should exist only when the service actually needs them.
- Keep the service boundary small and explicit. Add folders because the domain requires them, not because a template once generated them.

---

## Alignment Plan

This section turns the baseline into a concrete migration target for the current Umbral services.

### Repository-Level Changes

- Create a `services/` folder at the repository root.
- Move each service into its own bounded-context folder under `services/`.
- Stop using root-level `src/` and `tests/` as the long-term home for service code.
- Keep shared documentation in root `README.md` and cross-cutting operational material in `docs/` and `deploy/`.

### Service Targets

#### `identity-access-service`

- Owns authentication and authorization integration concerns for the platform.
- Expected application concepts: `Users`, `Roles`, `Permissions`.
- Expected infrastructure emphasis: `Identity/Keycloak`, persistence for local identity-access state, optional realtime/admin notifications if needed.
- Tests should live under `tests/UnitTests`, `tests/IntegrationTests`, and `tests/EndToEndTests`.

#### `mission-design-service`

- Owns mission-definition and planning concerns.
- Expected application concepts: `Missions`, `MissionPlans`, `Constraints`.
- The current root-level codebase most closely resembles the starting point for this service and should be migrated here first.
- Replace template/sample artifacts such as `Todo*`, `Counter`, and `Weather` with mission-design concepts.
- Tests should live under `tests/UnitTests`, `tests/IntegrationTests`, and `tests/EndToEndTests`.

#### `session-operations-service`

- Owns execution/session lifecycle concerns.
- Expected application concepts: `Sessions`, `SessionRuns`, `SessionAssignments`.
- Realtime support is likely to be relevant here when operational state changes need to reach clients immediately.
- Tests should live under `tests/UnitTests`, `tests/IntegrationTests`, and `tests/EndToEndTests`.

#### `scoring-monitoring-service`

- Owns scoring, telemetry, monitoring, and alerting concerns.
- Expected application concepts: `Scores`, `Metrics`, `Alerts`.
- Outbound integrations and notifications are likely to be more important here than in some other services.
- Tests should live under `tests/UnitTests`, `tests/IntegrationTests`, and `tests/EndToEndTests`.

### Concrete Target Tree

```text
umbral-backend/
├── services/
│   ├── identity-access-service/
│   │   ├── src/
│   │   │   ├── Api/
│   │   │   ├── Application/
│   │   │   │   ├── Common/
│   │   │   │   ├── Users/
│   │   │   │   ├── Roles/
│   │   │   │   └── Permissions/
│   │   │   ├── Domain/
│   │   │   ├── Infrastructure/
│   │   │   ├── Directory.Build.props
│   │   │   └── Directory.Packages.props
│   │   ├── tests/
│   │   │   ├── UnitTests/
│   │   │   ├── IntegrationTests/
│   │   │   └── EndToEndTests/
│   │   ├── README.md
│   │   └── structure.md
│   ├── mission-design-service/
│   │   ├── src/
│   │   │   ├── Api/
│   │   │   ├── Application/
│   │   │   │   ├── Common/
│   │   │   │   ├── Missions/
│   │   │   │   ├── MissionPlans/
│   │   │   │   └── Constraints/
│   │   │   ├── Domain/
│   │   │   ├── Infrastructure/
│   │   │   ├── Directory.Build.props
│   │   │   └── Directory.Packages.props
│   │   ├── tests/
│   │   │   ├── UnitTests/
│   │   │   ├── IntegrationTests/
│   │   │   └── EndToEndTests/
│   │   ├── README.md
│   │   └── structure.md
│   ├── session-operations-service/
│   │   ├── src/
│   │   │   ├── Api/
│   │   │   ├── Application/
│   │   │   │   ├── Common/
│   │   │   │   ├── Sessions/
│   │   │   │   ├── SessionRuns/
│   │   │   │   └── SessionAssignments/
│   │   │   ├── Domain/
│   │   │   ├── Infrastructure/
│   │   │   ├── Directory.Build.props
│   │   │   └── Directory.Packages.props
│   │   ├── tests/
│   │   │   ├── UnitTests/
│   │   │   ├── IntegrationTests/
│   │   │   └── EndToEndTests/
│   │   ├── README.md
│   │   └── structure.md
│   └── scoring-monitoring-service/
│       ├── src/
│       │   ├── Api/
│       │   ├── Application/
│       │   │   ├── Common/
│       │   │   ├── Scores/
│       │   │   ├── Metrics/
│       │   │   └── Alerts/
│       │   ├── Domain/
│       │   ├── Infrastructure/
│       │   ├── Directory.Build.props
│       │   └── Directory.Packages.props
│       ├── tests/
│       │   ├── UnitTests/
│       │   ├── IntegrationTests/
│       │   └── EndToEndTests/
│       ├── README.md
│       └── structure.md
├── docs/
├── deploy/
├── .gitignore
└── README.md
```

### Findings This Plan Resolves

- It fixes the root boundary mismatch by moving service code under `services/<service-name>/`.
- It fixes the test taxonomy mismatch by standardizing on `UnitTests`, `IntegrationTests`, and `EndToEndTests`.
- It fixes the application-feature mismatch by defining concrete business feature folders per service instead of leaving `Application` as only `Common`.
- It removes template/sample residue by replacing generic sample concepts with real Umbral service concepts.
