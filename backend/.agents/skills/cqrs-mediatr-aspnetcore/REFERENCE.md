# CQRS with MediatR in ASP.NET Core

This reference targets modern ASP.NET Core applications using MVC controllers with feature folders, layered architectures, Clean Architecture, or modular monoliths. This codebase exposes its HTTP surface through controllers only — not minimal-API endpoint groups.

## Authoritative References

Primary sources used for this skill:

- Microsoft CQRS pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs
- Microsoft .NET microservices architecture guidance for CQRS and DDD: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/
- Microsoft guidance for CQRS reads: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads
- ASP.NET Core dependency injection: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0
- ASP.NET Core web APIs: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0
- .NET dependency injection guidelines: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines
- MediatR official repository and README: https://github.com/LuckyPennySoftware/MediatR

## Outcome

When using this skill, produce code that:

- matches the host application's existing architectural style before introducing new structure
- treats CQRS as a boundary and modeling choice, not a requirement to split every project or database
- uses MediatR for application-level request dispatch and cross-cutting concerns, not as a substitute for domain modeling
- keeps ASP.NET Core endpoints thin and handlers focused
- preserves testability and explicit dependencies

## When CQRS Is Actually Worth It

Use CQRS when at least one of these is true:

- reads and writes have meaningfully different shapes
- business workflows are task-based, not CRUD-shaped
- validation, authorization, logging, transactions, and idempotency need consistent cross-cutting treatment
- the write side has domain complexity that should not leak into read models
- the read side needs optimized projections or a different persistence/query strategy

Prefer simpler application services or direct CRUD when:

- the domain is simple
- the endpoint mostly forwards data to one table
- the team would be adding handlers only because "that is the pattern"

## Core Mental Model

Treat the layers like this:

- ASP.NET Core endpoint/controller:
  - translate HTTP into an application request
  - handle transport concerns such as route values, status codes, and auth metadata
- MediatR request:
  - define one application use case
- Handler:
  - orchestrate domain objects, repositories, external services, and persistence
- Domain model:
  - protect invariants and business rules
- Query path:
  - read optimized data with minimal ceremony

MediatR is not the architecture. It is a dispatch mechanism for the application layer.

## Request Taxonomy

Use the request types deliberately:

- `IRequest<TResponse>`
  - standard command or query with a response
- `IRequest`
  - command with no meaningful payload result
- `INotification`
  - in-process publish/fan-out where multiple handlers may react
- `IStreamRequest<T>`
  - stream response scenarios only when the app genuinely needs them

Recommended semantics:

- command:
  - changes state
  - should express business intent
  - may return identifiers or a result DTO
- query:
  - does not change state
  - returns DTOs/read models
  - should be safe to repeat
- notification:
  - use for post-action reactions inside the same process
  - do not use as a hidden replacement for a required synchronous workflow step

## Contract Design

Prefer request contracts that are explicit and small.

Good patterns:

- `CreateOrderCommand`
- `CancelSubscriptionCommand`
- `GetInvoiceByIdQuery`
- `SearchCustomersQuery`

Avoid:

- `UpdateOrderCommand` with 40 optional properties
- `SaveCustomerCommand`
- requests that mirror a table instead of a business task

Practical guidance:

- use immutable request types where possible, usually records
- keep transport-only concerns out of the handler contract unless they are part of the use case
- return typed results rather than `object` or unstructured dictionaries
- if the contracts must live in a separate assembly, MediatR provides a contracts-only package; use it only when the architectural boundary is real

## ASP.NET Core Registration

Current MediatR guidance registers handlers by assembly scanning through `AddMediatR(...)`.

Recommended pattern:

- register MediatR once in the composition root
- scan only the assemblies that contain application handlers
- register pipeline behaviors explicitly
- keep endpoint/controller classes depending on `ISender` unless they truly need publish semantics

Why `ISender` by default:

- it narrows the dependency to request/response use cases
- it avoids handing every endpoint publish capabilities it does not need

Reserve `IPublisher` for notifications only.

## Handler Design

A good handler usually:

- loads only the state it needs
- coordinates one use case
- delegates invariant enforcement to the domain
- calls repository or DbContext methods explicitly
- persists once per logical unit when possible
- returns a response shaped for the caller

Handler anti-patterns:

- huge handlers that contain all business logic inline
- handlers that only forward to another handler
- handlers that call `mediator.Send(...)` for required internal steps instead of collaborating through normal dependencies
- handlers that return EF-tracked entities directly to the API

## Commands

Commands should model intent, not persistence mechanics.

Good command characteristics:

- named after business actions
- contain only the data required for that action
- validated before state changes
- executed within a clear consistency boundary

For write-side persistence:

- using EF Core directly in handlers is acceptable in many codebases
- using repositories is acceptable when the project already standardizes on them or the domain boundary benefits from them
- do not add repository abstractions solely because MediatR is present

For transactional behavior:

- commands often need a transaction boundary
- keep that boundary explicit
- a pipeline behavior is a good place for transaction orchestration if the application wants one consistent rule for command handlers

## Queries

Queries should optimize for read clarity and caller needs.

Microsoft's CQRS guidance is explicit that the read model can differ substantially from the write model. Apply that directly:

- return DTOs/read models, not aggregates
- feel free to join across tables if the read scenario needs it
- use a query technology appropriate to the read path

Practical defaults:

- use EF Core projections if the queries are simple and the codebase already uses EF Core comfortably
- use Dapper or raw SQL when the query is performance-sensitive, complex, or projection-heavy
- keep query handlers side-effect free

Do not force the read side through the same abstractions as the write side if that makes queries worse.

## Pipeline Behaviors

Pipeline behaviors are the main reason MediatR remains useful in ASP.NET Core applications. Use them for cross-cutting concerns that should apply consistently to many requests.

Good candidates:

- validation
- logging
- tracing/telemetry
- transactions for commands
- idempotency checks for externally repeated commands
- performance timing

Usually poor candidates:

- core business branching
- hidden retries for non-idempotent commands
- anything that makes handler execution order difficult to reason about

### Validation Behavior

Validation behavior is often the first behavior to add.

Recommended pattern:

- run validation before the handler
- aggregate validation failures into one exception/result
- keep validators separate from handlers

What not to do:

- duplicate the same validation in endpoint, behavior, and handler unless each layer validates a different concern
- let handlers assume invalid requests are impossible if the app still accepts unvalidated entry points elsewhere

### Logging and Telemetry Behavior

Use behavior-based logging to capture:

- request name
- correlation or trace identifiers
- elapsed time
- success/failure outcome

Do not log entire request payloads blindly if they may contain secrets, credentials, tokens, or personal data.

### Transaction Behavior

A transaction behavior can work well when:

- most commands should run in one transaction
- query handlers must remain outside that transaction behavior

Recommended rule:

- apply transaction behaviors to commands only

If the project uses EF Core:

- keep one `DbContext` scope per request
- begin/commit transactions only when the use case truly needs one beyond the provider's normal save semantics
- avoid nested transaction complexity unless the persistence strategy demands it

## Notifications and Domain Events

Use precise language:

- domain event:
  - a business fact raised by the domain
- MediatR notification:
  - an in-process dispatch mechanism

They are related but not identical.

Practical guidance:

- it is reasonable to adapt domain events into MediatR notifications for in-process reactions
- do not confuse MediatR notifications with durable integration events
- if an event must leave the process or survive failure, use messaging infrastructure or an outbox pattern, not bare MediatR publish

Avoid command chaining through notifications for required business steps. If step B must happen for command A to succeed, keep that orchestration explicit.

## Error Handling

Keep transport and application concerns separate.

Preferred pattern:

- handlers throw meaningful domain/application exceptions or return typed results according to the project's conventions
- ASP.NET Core exception handling or endpoint filters translate failures to HTTP responses

MediatR also supports exception actions and exception handlers. Use them sparingly:

- good for consistent mapping or side-effects around known exception types
- bad when they make the control flow implicit and surprising

## Dependency Injection Guidance

Follow the standard .NET DI guidance even when using MediatR:

- prefer constructor injection
- avoid service locator patterns
- avoid resolving services manually from `IServiceProvider` inside handlers
- keep service lifetimes compatible with their dependencies

Practical lifetimes:

- handlers are registered by MediatR as transient
- DbContexts are commonly scoped
- a transient handler depending on scoped services is fine inside the ASP.NET Core request scope

Be careful with singleton services used by handlers. They must be thread-safe and must not capture scoped dependencies.

## Folder and Assembly Organization

Choose the lightest structure that fits the codebase.

Common workable options:

1. Feature folders
   - `Features/Orders/CreateOrder`
   - keeps request, handler, validator, endpoint mapping, and tests close together
2. Application folders by type
   - `Application/Commands`
   - `Application/Queries`
   - simpler at first, can become noisy at scale
3. Separate application assembly
   - useful when the codebase already has architectural boundaries

Do not split into many projects just because CQRS exists. Split when the boundary provides real value.

## Testing Strategy

Test at three levels:

1. Handler unit tests
   - business orchestration and edge cases
2. Integration tests
   - persistence, transactions, pipeline behaviors, and endpoint wiring
3. End-to-end tests where the workflow risk justifies them

Good tests verify:

- commands change the intended state
- queries return the expected projection
- validation behavior fails invalid requests
- transactions or idempotency rules behave correctly

Do not over-invest in tests that only prove MediatR can dispatch a request.

## Recommended Defaults

Use these defaults unless the existing codebase has stronger conventions:

- `ISender` injected into controllers/endpoints
- task-based command and query names
- immutable request records
- one handler per request
- thin endpoints/controllers
- validation behavior
- logging/tracing behavior
- command-only transaction behavior where justified
- read DTOs for queries
- explicit persistence in handlers

## Anti-Patterns to Push Back On

Push back when the implementation is drifting into these patterns:

- "every endpoint must have MediatR even when a direct application service is clearer"
- handlers calling handlers as a way to compose workflows
- one generic repository plus one generic handler base for everything
- queries returning aggregates and commands returning tracked entities
- using notifications as a hidden workflow engine
- putting all business logic into pipeline behaviors
- introducing CQRS and separate databases at the same time without operational need

## Practical Build Sequence

When implementing in a real project, prefer this order:

1. map one endpoint to one request
2. add one handler
3. make persistence explicit
4. add validation
5. add one cross-cutting behavior at a time
6. add tests around the full slice
7. only then generalize repeated patterns

This keeps the architecture honest and prevents abstraction-first design.

## Notes on Current Documentation

The ASP.NET Core links above target the current `.NET 10` documentation set.

The MediatR official README currently documents:

- DI registration through `AddMediatR`
- handler and mediator service registration through assembly scanning
- behavior registration via `AddBehavior`, `AddOpenBehavior`, pre-processors, and post-processors
- a contracts-only package for contract-sharing scenarios

Because MediatR packaging and operational details can evolve, verify the current README in the target project before adding package-specific configuration such as licensing or specialized registration options.
