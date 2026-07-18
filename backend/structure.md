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

For bounded context ownership, aggregate boundaries, domain events, and cross-context contracts see `docs/ddd_solution_model.md`.

---

## Monorepo Tree

```text
umbral-backend/
├── services/
│   ├── <service-name>/
│   │   ├── src/
│   │   │   ├── Api/                                             # Service entry point and transport concerns
│   │   │   │   ├── Controllers/                                 # MVC controllers ([ApiController] + attribute routing)
│   │   │   │   │   ├── HealthController.cs
│   │   │   │   │   ├── <Entity>Controller.cs
│   │   │   │   │   └── WebhookController.cs                     # Optional inbound webhook controller
│   │   │   │   ├── Hubs/                                        # SignalR hubs
│   │   │   │   │   └── WebhookHub.cs
│   │   │   │   ├── Services/                                    # Adapters for request context
│   │   │   │   ├── DependencyInjection.cs
│   │   │   │   ├── ObservabilityExtensions.cs                  # OpenTelemetry tracing + log export wiring
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
│   │   │   │   ├── Dtos/                                         # central home for ALL response DTOs, sub-grouped by area (ADR-0011 §1/§2)
│   │   │   │   │   └── <Area>/
│   │   │   │   │       ├── <Entity>DetailsDto.cs
│   │   │   │   │       └── <Entity>ListItemDto.cs
│   │   │   │   │
│   │   │   │   ├── <Entity>/                                     # Example: Orders, Customers, Billing
│   │   │   │   │   ├── Commands/
│   │   │   │   │   │   ├── Create<Entity>/
│   │   │   │   │   │   │   ├── Create<Entity>Command.cs
│   │   │   │   │   │   │   ├── Create<Entity>CommandHandler.cs    # pipeline files only; handler realizes a single-consumer Facade inline (ADR-0011/0012)
│   │   │   │   │   │   │   └── Create<Entity>CommandValidator.cs
│   │   │   │   │   │   ├── Update<Entity>/
│   │   │   │   │   │   │   ├── Update<Entity>Command.cs
│   │   │   │   │   │   │   ├── Update<Entity>CommandHandler.cs
│   │   │   │   │   │   │   └── Update<Entity>CommandValidator.cs
│   │   │   │   │   │   └── Delete<Entity>/
│   │   │   │   │   │       ├── Delete<Entity>Command.cs
│   │   │   │   │   │       └── Delete<Entity>CommandHandler.cs
│   │   │   │   │   ├── Queries/
│   │   │   │   │   │   ├── Get<Entity>ById/
│   │   │   │   │   │   │   ├── Get<Entity>ByIdQuery.cs
│   │   │   │   │   │   │   └── Get<Entity>ByIdQueryHandler.cs     # response DTO lives in Application/Dtos/<Area>/, not here
│   │   │   │   │   │   └── Get<Entity>List/
│   │   │   │   │   │       ├── Get<Entity>ListQuery.cs
│   │   │   │   │   │       └── Get<Entity>ListQueryHandler.cs
│   │   │   │   │   ├── Common/                                   # shared-by-≥2-slices mappers/guards + mandated patterns for this area (Proxy, shared Facade, Template Method/CoR) — see ADR-0004; Common/Authorization/ holds relocated *AuthorizationProxy decorators
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
│   │   │   │   ├── Identity/                                    # users-service only — see ADR-0001
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
│   │   │   │   ├── Realtime/                                    # session-operations-service only
│   │   │   │   │   ├── SignalRNotifier.cs
│   │   │   │   │   └── DependencyInjection.cs
│   │   │   │   ├── Integrations/                                # Optional — only when service has outbound contracts
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
- Exposes HTTP endpoints via MVC controllers (`[ApiController]` + attribute routing)
- Exposes SignalR hubs
- Maps request context into application abstractions
- Configures middleware and composition root concerns (including the global
  `ProblemDetailsExceptionHandler` — controllers carry no per-action try/catch)

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

**`users-service` only** — see ADR-0001.

- Keycloak admin-client configuration for user provisioning
- Claims translation
- JWT event hooks
- Identity service adapters

Other services do not carry `Infrastructure/Identity/Keycloak/`. They read actor identity from the trusted headers forwarded by the `api-gateway` (`X-User-Id`, `X-User-Role`, `X-User-Email`) via `CurrentUserService`.

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
- Controller files should be named `<Entity>Controller.cs` and live in `Api/Controllers/`.
- Handler files should be named after the request they handle, for example `CreateOrderCommandHandler.cs`.
- Validators should live next to their command when applicable.

### Commands and Queries

- Commands mutate state.
- Queries read state.
- One handler should handle one command or one query.
- Each use case is a self-contained vertical slice holding **only its pipeline files**: the request,
  its handler, and (commands) its validator, together in one `Commands/<UseCase>/` or
  `Queries/<UseCase>/` folder. No `Handlers/` type-bucket (ADR-0011). A single-consumer `Facade` is
  realized **inline in the handler**; a single-consumer `Proxy` stays a decorator, relocated to
  `Application/<Entity>/Common/Authorization/` to keep the slice pipeline-pure (ADR-0011 §2/§4).

### DTOs

- Every response DTO (command result AND query response) lives in the central `Application/Dtos/<Area>/`
  root, sub-grouped by area — **not** in the slice.
- A command returning `Guid`/`Unit` carries no result DTO at all.
- No per-area `DTOs/`/`Dtos/` type-bucket, and no DTO co-located in a slice (ADR-0011 §1/§2). Mandated-pattern
  implementations for the area live in `Application/<Entity>/Common/` — see ADR-0004.

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

## Concrete Service Layout

```text
umbral-backend/
├── services/
│   ├── users-service/
│   │   ├── src/
│   │   │   ├── Api/
│   │   │   ├── Application/
│   │   │   │   ├── Common/
│   │   │   │   ├── Users/           # User provisioning, role assignment, access management
│   │   │   │   └── JoinTokens/      # Token issuance and validation for participant entry
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
│   │   │   │   ├── Missions/        # Mission CRUD, node management, activation
│   │   │   │   └── TriviaQuizzes/   # Quiz authoring, question management, publication
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
│   │   │   │   ├── LiveSessions/    # Session lifecycle commands and session-level queries
│   │   │   │   ├── Teams/           # Team registration and team-level queries
│   │   │   │   ├── Participants/    # Participant join and reconnect flows
│   │   │   │   └── Evidence/        # Evidence submission, review, and target resolution
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
│       │   │   ├── ScoreEntries/    # Score recording commands and history queries
│       │   │   ├── Penalties/       # Penalty apply and revert commands
│       │   │   └── Rankings/        # Ranking recalculation and snapshot queries
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
├── api-gateway/
│   ├── src/
│   │   ├── Program.cs
│   │   ├── DependencyInjection.cs
│   │   ├── ObservabilityExtensions.cs                # OpenTelemetry tracing + log export wiring
│   │   ├── Logging/
│   │   │   ├── SensitiveQueryLogRedactor.cs          # Redacts ?access_token & friends from log text
│   │   │   └── RedactingLoggerFactory.cs             # ILoggerFactory decorator applying that redaction
│   │   ├── Transforms/
│   │   │   ├── WebSocketTokenExtractionTransform.cs  # Extracts ?access_token for SignalR upgrades — see ADR-0002
│   │   │   └── TrustedHeadersTransform.cs            # Injects X-User-* headers; strips the token from the forwarded request
│   │   ├── appsettings.json
│   │   └── ApiGateway.csproj
│   └── README.md
├── docs/
├── deploy/
│   ├── keycloak/import/                             # Realm import consumed by the keycloak container
│   └── postgres/init-dbs.sql                        # Per-service database bootstrap
├── docker-compose.yml                               # postgres, keycloak, rabbitmq, seq, api-gateway,
│                                                    # mission-design, users, session-operations
├── docker-compose.override.yml                      # DEV-ONLY hot-reload loop; auto-loads on bare `up`
├── .gitignore
└── README.md
```

