# 0018 — Centralized ProblemDetails handling is the try/catch, not per-controller catch blocks

## Status

Accepted.

Resolves [#139](https://github.com/grupo-12-desarrollo-umbral/umbral/issues/139)
(*"Decide: controller try/catch vs global ProblemDetails handler"*). Outcome 1 of
that issue: the existing centralized handler **is** the exception handling the course
requirement asks for — it has been moved to one place instead of copied into every
action. No literal `try`/`catch` is added to controllers, so no follow-up issue for
rethrow-only catch blocks is opened.

## Context

A course requirement asks for `try`/`catch` in controllers. This project already
handles exceptions globally, and the two approaches conflict.

**What exists today.** No controller in any service contains a `catch` (verified across
`services/*/src/Api/**/*Controller.cs` and `api-gateway`). Each application service
registers `ProblemDetailsExceptionHandler` (`src/Api/Services/ProblemDetailsExceptionHandler.cs`)
through `AddExceptionHandler<ProblemDetailsExceptionHandler>()` in `Api/DependencyInjection.cs`
and `app.UseExceptionHandler()` in `Program.cs`. Four services carry it:
identity-access, mission-design, session-operations, and scoring-monitoring. It emits
RFC 7807 `application/problem+json` and dispatches on type rather than enumerating
concrete exceptions:

- FluentValidation-derived `ValidationException` → **400**, with the structured
  per-field failures exposed as a `problem.Extensions["errors"]` map so clients map
  errors to fields instead of parsing the flattened `Detail`.
- anything implementing **`IErrorMetadata`** → status derived from its `ErrorCategory`
  (see mapping below). The `Detail` is the exception's curated, identifier-free
  `PublicDetail` when it opts in, otherwise a generic per-category sentence — the raw
  exception message is never echoed, because domain messages routinely interpolate ids
  (team ids, participant ids, user ids). The stable `ErrorCode` slug is the RFC 7807
  `type`.
- `UnauthorizedAccessException` → **401**, generic detail, correlated by `traceId`.
- everything else → generic **500** whose `Detail` is *"An unexpected error occurred."*
  and which is correlated to the logged exception through a `traceId` extension. The
  message is never echoed, because an unanticipated exception may carry constraint
  names, column names, or connection strings.

**Category → HTTP status mapping** (`StatusFor` in the handler):

| `ErrorCategory` | HTTP status |
| --- | --- |
| `NotFound` | 404 |
| `Validation` | 400 |
| `Conflict` | 409 |
| `Forbidden` | 403 |
| `Unauthorized` | 401 |
| `Unprocessable` | 422 |

The enum has exactly these six members; anything unmapped falls to 500 by design.

**Domain vs application exceptions are separated.** `DomainException`
(`src/Domain/Exceptions/DomainException.cs`) is abstract with an abstract
`ErrorCategory Category`, so every concrete domain exception is *forced at compile time*
to classify itself — a new domain exception cannot silently fall through to an unmapped
500. `ErrorCode` defaults to a kebab-case slug derived from the type name
(`TeamNotFoundException` → `team-not-found`) and `PublicDetail` defaults to `null`
(message suppressed). Application-layer exceptions opt into `IErrorMetadata`
*explicitly* (e.g. `NotFoundException`, `ForbiddenAccessException`,
`ValidationException` under `src/Application/Common/Exceptions/`).

**Two more layers reinforce the same vocabulary.** A MediatR
`UnhandledExceptionBehaviour` logs any exception escaping a handler and rethrows it
(it builds no response). SignalR has its own `DomainExceptionHubFilter`
(`src/Api/Hubs/DomainExceptionHubFilter.cs`), which maps the *same*
`IErrorMetadata`/`ErrorCategory` vocabulary to a stable machine-readable `code` carried
in a `HubException` message — plus a handful of curated codes finer-grained than their
category (`LATE_JOIN_NOT_ALLOWED`, `PARTICIPANT_REMOVED`, `WRONG_TEAM`,
`ALREADY_CONNECTED`, `TEAM_UNAVAILABLE`) that the mobile reconnect policy branches on,
so they are contractual and must stay stable. Its `ERROR` fallback substitutes a
generic message and correlates by `traceId`, exactly like the REST 500 arm. The REST
handler and the hub filter share one classification source of truth.

**The conflict.** Adding per-controller try/catch would duplicate the
exception-to-status-code mapping in every action, and the two copies would drift. It
would also lose the uniform `problem+json` shape and the `traceId` on 500s. Catch
blocks that build their own `ObjectResult`/status codes are exactly the drift scenario
and are prohibited.

## Decision

1. **Centralized `ProblemDetailsExceptionHandler` is the exception handling.** The
   course's "try/catch in controllers" requirement is satisfied by handling that has
   been *moved to one place* — the framework's `IExceptionHandler` pipeline — rather
   than copied into every action. This ADR is the record that centralized handling
   **is** the try/catch.

2. **Controllers stay catch-free.** No `try`/`catch` is added to any controller action.
   Controllers throw (directly or via the MediatR pipeline); the global handler
   produces the response.

3. **No drifting catch blocks, ever.** A catch block that constructs its own
   `ObjectResult`, sets its own status code, or re-implements any part of the
   category→status mapping is prohibited. That is the two-copies-drift failure this
   decision exists to prevent.

4. **Classification lives on the exception, not the endpoint.** New errors classify
   themselves by extending `DomainException` (compile-time-forced `ErrorCategory`) or
   by implementing `IErrorMetadata` in the application layer. The handler is never
   edited to add a concrete exception type.

## Consequences

- One exception-to-HTTP mapping exists, in one file per service; there is nothing to
  drift out of sync.
- Every error response is uniform RFC 7807 `application/problem+json`; 401/500 and any
  classified error carry a `traceId` that is pasteable into a log search, and raw
  exception messages never reach clients.
- The requirement is met without ceremonial rethrow-only catch blocks, so no follow-up
  issue (outcome 2 of #139) is opened.
- A reviewer expecting literal `try`/`catch` in a controller will not find one; this ADR
  is the answer to that question.
- **Out of scope / already resolved:** the one genuine gap found while investigating
  #139 — `api-gateway` having no exception handler — was filed and fixed separately as
  [#147](https://github.com/grupo-12-desarrollo-umbral/umbral/issues/147) (now closed);
  `api-gateway` registers its own `ProblemDetailsExceptionHandler` via
  `UseExceptionHandler`.
