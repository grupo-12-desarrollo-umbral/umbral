# Issue #191 — scoring-monitoring: scaffold + MassTransit/RabbitMQ + consume `AnswerRegistered`

> Linear/PRD: DES-85 · Sister slice to #164, downstream of #165 · First slice of HU-37 (DES-51)
> Plan authored: 2026-07-12

## Goal

Stand up the first runnable slice of the greenfield `scoring-monitoring-service` and prove
it consumes `AnswerRegisteredIntegrationEvent` end-to-end over MassTransit + RabbitMQ —
the full **broker → consumer → handler** path — before any real scoring logic is added.

`#165` has **landed** on this branch: `session-operations-service` publishes
`AnswerRegisteredIntegrationEvent` (a `sealed record` with `[EntityName("session-answer-registered")]`)
via MassTransit `IPublishEndpoint`. This slice makes scoring-monitoring the intended consumer.

## Scope decisions (confirmed)

- **Thin handler.** Consumer is a thin transport adapter (ADR-0017): map → dispatch an
  Application MediatR command → thin handler that logs receipt + calls a seam port.
  **No `ScoreEntry` write** (deferred to HU-37). **No `IIntegrationEventPublisher`** (ADR-0017 bans it).
- **Full infra parity.** Provision the complete conventional service (DbContext + EF/Postgres,
  OTEL, trusted-header auth, ProblemDetails error handling, health controller) now, matching
  `session-operations-service`, to avoid a second wiring pass.
- **Full convention kit + its unit tests.** Copy the area-agnostic kit (4 pipeline behaviours,
  `AuthorizeAttribute`, domain base classes, `IErrorMetadata`/`DomainException`/`ProblemDetails`
  error system) *with* the unit tests needed to hold ≥93% line+branch coverage.
- **Enroll in `CONVERGED_SERVICES` now** — structure-guard validates the vertical slice on every build.

## Governing constraints (ADRs)

- **ADR-0017 (messaging):** Application may reference `MassTransit.Abstractions` (`[EntityName]`,
  `IPublishEndpoint`, `IConsumer`). RabbitMQ transport / `UsingRabbitMq` / credentials / topology
  stay in `Infrastructure/Messaging`. Consumers are **thin adapters** that dispatch an Application
  command/query — scoring business rules never live in the consumer body. Do **not** reintroduce
  `IIntegrationEventPublisher`.
- **ADR-0005 (coverage):** `coverlet.msbuild`; `scripts/cover-gate.sh` chains
  `Application.UnitTests → Api.UnitTests → Infrastructure.IntegrationTests`; **93% line AND branch**.
  Adding `tests/IntegrationTests` auto-enrolls the service in `make gate-all`.
- **Coverage-exclusion rule (`.agents/backend-agent.md:207`, not ADR-0005):** `[ExcludeFromCodeCoverage]`
  allowed **only** on `Program.cs`, DI extension methods, and generated migrations — never
  Domain/Application (and, per session-operations precedent, not on Api service classes like
  `CurrentUser`/`CurrentUserContext`, which are covered by tests instead).
- **ADR-0011 (structure):** `Application/<Area>/{Commands|Queries}/<UseCase>/` holds request +
  handler + validator; response DTOs in central `Application/Dtos/<Area>/`; outbound contracts
  (integration events) in `<Area>/Common/`. No `Handlers/`/`Facades/` buckets. Enforced by
  `make structure-guard`.
- **ADR-0008 (test fixtures):** one shared Postgres Testcontainer via
  `ICollectionFixture<PostgreSqlFixture>` + `[Collection(...)]`; reset schema per test.
- **ADR-0001 (auth):** trust `X-User-*` headers from the gateway; do not validate JWT in the service.
- **ADR-0014 (no cross-layer reflection):** inner layers never resolve an Api type by
  reflection/type-name; enforced by `make layer-guard`.

## Namespace / project conventions

- Every `.csproj`: `RootNamespace`/`AssemblyName` = `umbral_backend.<Layer>` (reused per service).
- DI extension classes live in `namespace Microsoft.Extensions.DependencyInjection`.
- `TargetFramework` = `net10.0` (inherited from `src/Directory.Build.props`).
- Reference graph: `Domain` (← `MediatR.Contracts` only) ← `Application` ← `Infrastructure` ← `Api`;
  `ProjectReference … SkipGetTargetFrameworkProperties="true"`. Infrastructure adds
  `FrameworkReference Microsoft.AspNetCore.App` + `InternalsVisibleTo` the integration-test assembly.
- Test csproj do **not** use central package management — pin `Version=` inline.

## Consume path (the demonstrated flow)

```
RabbitMQ exchange "session-answer-registered"
  → AnswerRegisteredConsumer : IConsumer<AnswerRegisteredIntegrationEvent>   (Infrastructure/Messaging/Consumers)
      → ISender.Send(new RecordAnswerReceiptCommand(...))                    (maps contract → command)
          → RecordAnswerReceiptCommandHandler                               (Application; thin: logs + probe)
              → IAnswerReceiptProbe.Record(...)                             (seam; prod = logging no-op, test = TCS)
```

The service-local contract `AnswerRegisteredIntegrationEvent` (its own `[EntityName("session-answer-registered")]`
record in `Application/Scores/Common/`) binds to the same exchange as the publisher; MassTransit routes by name.

## File plan by layer

Coverage legend: `[X]` = `[ExcludeFromCodeCoverage]` (boot/DI/generated) · `[T]` = needs an accompanying test.

### Packages — `src/Directory.Packages.props`
Re-add (versions matching session-operations): `MassTransit`, `MassTransit.Abstractions`,
`MassTransit.RabbitMQ` = 8.4.1; `RabbitMQ.Client` = 7.1.2; `Microsoft.EntityFrameworkCore`,
`Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL` = 10.0.0;
`Npgsql.OpenTelemetry` = 10.0.0; `OpenTelemetry.Api/.Exporter.OpenTelemetryProtocol/.Extensions.Hosting/.Instrumentation.AspNetCore/.Instrumentation.Http` = 1.16.0.
Skip `Microsoft.AspNetCore.SignalR` (no hub). Keep the `Microsoft.OpenApi` 2.7.5 security pin.

### Domain — `src/Domain/`  (deps: `MediatR.Contracts`)
- `Common/{BaseEntity,BaseAuditableEntity,BaseEvent,ValueObject}.cs` `[T]`
- `Exceptions/{DomainException,ErrorCategory,IErrorMetadata}.cs` `[T]`
- `GlobalUsings.cs`
- *(No entities yet — `ScoreEntry` is HU-37.)*

### Application — `src/Application/`  (deps: Domain + `MediatR`, `FluentValidation`, `MassTransit.Abstractions`)
- `Common/Behaviours/{UnhandledException,Authorization,Validation,Performance}Behaviour.cs` `[T]`
- `Common/Security/AuthorizeAttribute.cs` `[T]`
- `Common/Exceptions/{Validation,NotFound,ForbiddenAccess}Exception.cs` `[T]`
- `Common/Interfaces/IAnswerReceiptProbe.cs` (test seam), `Common/Interfaces/IDatabaseHealthCheck.cs`
- `Scores/Common/AnswerRegisteredIntegrationEvent.cs` — `sealed record` + `[EntityName("session-answer-registered")]` `[T]`
- `Scores/Commands/RecordAnswerReceipt/{RecordAnswerReceiptCommand,…CommandHandler,…CommandValidator}.cs` `[T]`
- `DependencyInjection.cs` `[X]` (validators + MediatR + 4 open behaviours, in order:
  UnhandledException → Authorization → Validation → Performance)
- `GlobalUsings.cs`

### Infrastructure — `src/Infrastructure/`  (deps: Application + EF/Npgsql + MassTransit + RabbitMQ.Client)
- `Persistence/ApplicationDbContext.cs` (empty model), `ApplicationDbContextFactory.cs`
  (env var `SCORING_MONITORING_SERVICE_CONNECTION_STRING`, `Database=scoring_monitoring_service`),
  `Persistence/DependencyInjection.cs` `[X]`
- `Persistence/Interceptors/{AuditableEntity,DispatchDomainEvents}Interceptor.cs` `[T]`
- `Persistence/DatabaseHealthCheck.cs` (`IDatabaseHealthCheck` impl) `[T]`
- `Messaging/RabbitMqOptions.cs` (`SectionName="RabbitMq"`; Exchange default `umbral.scoring-monitoring`)
- `Messaging/MassTransitMessagingRegistration.cs` `[X]`
  (`AddMassTransit`: `bus.AddConsumer<AnswerRegisteredConsumer>()` + `UsingRabbitMq` + `ConfigureEndpoints`)
- `Messaging/Consumers/AnswerRegisteredConsumer.cs` — thin adapter → `ISender.Send` `[T]`
- `Messaging/LoggingAnswerReceiptProbe.cs` (`IAnswerReceiptProbe` prod impl) `[T]`
- `DependencyInjection.cs` `[X]` (persistence + `TimeProvider.System` + `Configure<RabbitMqOptions>` +
  `AddMassTransitMessaging`)
- `Migrations/` — generated via `make ef … "migrations add InitScoringMonitoring"` `[X]`
- `GlobalUsings.cs`

### Api — `src/Api/`  (`Microsoft.NET.Sdk.Web`; deps: Application + Infrastructure + OTEL stack)
- `Program.cs` `[X]` — Application → Infrastructure → Web; `MigrateAsync` on boot; `UseHsts` (non-dev);
  `UseExceptionHandler(_ => {})`; `UseAuthentication/Authorization`; `MapOpenApi`; `MapControllers`;
  `[ExcludeFromCodeCoverage] partial class Program`. (No `MapHub`.)
- `DependencyInjection.cs` (`AddWebServices`) `[X]` — observability, dev EF page filter,
  HttpContextAccessor, `CurrentUser`/`CurrentUserContext`, TrustedHeaders auth, authorization
  policies, `AddExceptionHandler<ProblemDetailsExceptionHandler>`, controllers, OpenAPI.
- `ObservabilityExtensions.cs` `[X]` — OTEL traces+logs, gated on `OTEL_EXPORTER_OTLP_ENDPOINT`;
  `AddAspNetCoreInstrumentation/AddHttpClientInstrumentation/AddNpgsql/AddOtlpExporter`.
- `Services/ProblemDetailsExceptionHandler.cs` `[T]` — RFC 7807 switch keyed off `IErrorMetadata`.
- `Services/AuthorizationPolicies.cs` — scoring roles (Operator, Administrator, combinations).
- `Services/{CurrentUser,CurrentUserContext}.cs` `[T]` — covered by `tests/IntegrationTests/Api/CurrentUserTests.cs` (matches session-operations; per `.agents/backend-agent.md:207` only Program/DI/migrations may be `[X]`).
- `TrustedHeaders` auth handler (file-scoped, inside `Api/DependencyInjection.cs`) `[X]`.
- `Controllers/HealthController.cs` `[T]` — `GET /health` (DB check), `GET /alive`.
- `appsettings.json` — `RabbitMq` + `Logging` sections only.
- `GlobalUsings.cs`
- **Excluded:** no `SessionsHub`/broadcasters (SignalR is session-operations-only), no JWT.

### Tests  (inline-pinned packages)
- `tests/UnitTests` (Domain): base classes + error-metadata tests.
- `tests/Application.UnitTests`: behaviours (incl. branch cases), `AuthorizeAttribute`,
  exceptions, `RecordAnswerReceipt` handler + validator.
- `tests/IntegrationTests`:
  - Copy `DockerAvailability.cs` (ping Docker → `SkipException` on `HttpRequestException{SocketException}`),
    `integration.runsettings` (`TESTCONTAINERS_RYUK_DISABLED=true`), `PostgreSqlFixture`/`PostgreSqlCollection`.
  - `Messaging/AnswerRegisteredConsumeTests.cs`: `RabbitMqBuilder` (`guest`/`guest`) guarded by
    `DockerAvailability.StartOrSkipAsync`; boot the **real** service bus registration with a
    TCS-completing `IAnswerReceiptProbe` fake; publish `AnswerRegisteredIntegrationEvent` from a
    separate bus; assert the probe fires within timeout → proves the real consumer ran end-to-end.
    Skips gracefully when Docker is unavailable.

### Repo wiring
- `backend/docker-compose.yml`: add `scoring-monitoring-service` block — build context + `Dockerfile`;
  `depends_on` postgres (`service_healthy`), rabbitmq (`service_started`), seq (`service_started`);
  env `ASPNETCORE_ENVIRONMENT/URLS`, `ConnectionStrings__umbral_backendDb=Host=postgres;…;Database=scoring_monitoring;…`,
  `RabbitMq__HostName/Port/VirtualHost/UserName/Password`, `OTEL_EXPORTER_OTLP_ENDPOINT/PROTOCOL`,
  `OTEL_SERVICE_NAME=scoring-monitoring-service`; host port `5004:8080`.
- `backend/docker-compose.override.yml`: add the `x-dotnet-watch` hot-reload block.
- `Dockerfile`: copy verbatim (ENTRYPOINT `umbral_backend.Api.dll`).
- `backend/deploy/postgres/init-dbs.sql`: register database `scoring_monitoring`.
- `backend/Makefile`: add `scoring-monitoring-service` to `CONVERGED_SERVICES`.
- Expand `CONTEXT.md` (ScoringMonitoring ubiquitous language) in the established style; leave
  `structure.md`/`README.md` as one-line stubs.

## Build order (phased per `.agents/backend-agent.md`)

1. Packages — re-add pins to `src/Directory.Packages.props`.
2. Domain — base classes + error system + unit tests → `make build`/unit gate.
3. Application — behaviours, security, exceptions, seam, contract, `RecordAnswerReceipt` slice, DI + tests.
4. Infrastructure — DbContext/factory/persistence DI/interceptors/health; messaging (options, registration,
   consumer, probe); `make ef migrations add InitScoringMonitoring`.
5. Api — Program, AddWebServices, observability, ProblemDetails, auth/policies/current-user, health, appsettings.
6. tests/IntegrationTests — DockerAvailability, runsettings, Postgres fixture, RabbitMq consume test.
7. Repo wiring — compose base+override, Dockerfile, init-dbs.sql, CONVERGED_SERVICES.
8. Verify — `make build/test/gate SVC=scoring-monitoring-service` + `make structure-guard` + `layer-guard`;
   confirm other services untouched.

## Acceptance-criteria trace (#191)

- [ ] Minimal runnable backend skeleton boots under standard workflow → Phases 2–5 (`Program.cs` + host).
- [ ] MassTransit + MassTransit.RabbitMQ pinned in central package file → Phase 1.
- [ ] Bus registered `AddMassTransit(...).UsingRabbitMq(...)`, starts/stops with Generic Host → Phase 4.
- [ ] RabbitMQ host/port/vhost/creds from config + env; compose passes broker settings → Phases 4, 7.
- [ ] `IConsumer<AnswerRegisteredIntegrationEvent>` registered → Phase 4.
- [ ] Service-local contract bound via `[EntityName("session-answer-registered")]` → Phase 3.
- [ ] Testcontainers.RabbitMq test publishes `AnswerRegistered`, verifies scoring consumer receives → Phase 6.
- [ ] Messaging test skips gracefully when Docker unavailable → Phase 6 (`DockerAvailability`).
- [ ] Build and existing tests pass → Phase 8.

## Risks / notes

- **Coverage gate (93% line+branch)** binds how much unexercised code exists — every non-`[X]` file
  ships with a test. Verify `make gate SVC=scoring-monitoring-service` passes before PR.
- **DB deferred content:** DbContext ships with an empty model; the initial migration creates an empty
  schema + `__EFMigrationsHistory`. First entity (`ScoreEntry`) arrives in HU-37.
- **ADR citation hygiene:** `0001`–`0005` collide between `backend/adr/` and `backend/docs/adr/` — cite by path.
- Do not add SignalR or JWT — both are out of scope for this service per AGENTS/ADR-0001.
