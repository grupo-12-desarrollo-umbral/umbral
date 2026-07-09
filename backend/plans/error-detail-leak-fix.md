# Fix: unclassified errors leak internals and log nothing

> **Status — the `traceId` is now a real W3C trace id (issue #124, landed 2026-07-09).** When this
> plan shipped (PR #123), no `ActivitySource` had a listener, so `Activity.Current` was null on every
> request and the services actually emitted the per-process fallback (`0HN7ABC123XYZ:00000001` for
> REST, the SignalR `ConnectionId` for the hub). The `00-4bf92f…-01` examples below were therefore
> *aspirational* at the time. The OpenTelemetry wiring (`backend/plans/otel-wiring.md`, Phases 1–4)
> registered that listener, so `Activity.Current` is now non-null and the id is real.
>
> The examples below are also the wrong *shape*. They show `Activity.Current.Id` — the full
> `traceparent`, `00-<trace-id>-<span-id>-01` — which is what the services first emitted. Log
> backends index the bare 32-hex trace-id, so a client pasting that value into Seq matched nothing;
> you had to hand-extract the middle segment. Both handlers now emit `Activity.Current.TraceId`
> instead, so the value is pasteable as-is. Measured behind the gateway on a genuinely unclassified
> 500: `"traceId":"7d7f1929d8de9ca4fa5fe0c4eeb29412"`. The hub emits the same, with the SignalR
> `ConnectionId` still the fallback when no `Activity` is current.

## Why this is needed

PR #53 unified exception→ProblemDetails mapping and added a reflection test per
service proving no `DomainException` can map to a 500. That guarantee holds —
the tests are real and they pass (mission-design 69/69, identity-access 22/22).

But "no domain exception maps to 500" is a different claim from "the 500 path is
safe", and the 500 path is not safe.

### 1. Four sites echo `exception.Message` on the unclassified arm

```csharp
_ => Problem(StatusCodes.Status500InternalServerError,
             "internal-error",
             "An unexpected error occurred.",   // Title: generic, correct
             exception.Message)                 // Detail: raw, straight to the client
```

- `identity-access-service/src/Api/Services/ProblemDetailsExceptionHandler.cs`
- `mission-design-service/src/Api/Services/ProblemDetailsExceptionHandler.cs`
- `session-operations-service/src/Api/Services/ProblemDetailsExceptionHandler.cs`
- `session-operations-service/src/Api/Hubs/DomainExceptionHubFilter.cs`
  (`BuildPayload`)

The `UnauthorizedAccessException` arm does the same.

This arm exists precisely to catch what you didn't anticipate —
`NpgsqlException`, `KeyNotFoundException`, `NullReferenceException`. Those
messages carry constraint names, column names, connection strings. Echoing
`.Message` is safe for the classified 4xx arms (those strings are authored by us
and meant to be read) and unsafe for exactly this one.

The hub filter is the worst of the four: it catches _every_ exception and
serializes the message into a `HubException` delivered to a mobile client.

### 2. The same path logs nothing

`TryHandleAsync` always returns `true`, so `ExceptionHandlerMiddleware` never
logs — this is stated in `b029e36`'s own commit message. That leaves
`UnhandledExceptionBehaviour` as the sole recorder, and it is a **MediatR
pipeline behaviour**: it only wraps exceptions thrown inside `next()`.

Anything thrown outside the pipeline — model binding, endpoint filters, auth
middleware, DI resolution, SignalR paths that don't route through MediatR —
reaches the handler with no log written anywhere. So on that surface: the client
sees the Npgsql message, and we see nothing. Inverse of what we want.

No `ILogger` is injected into any of the four files.

### Fix, in one sentence

Swap the leak for a correlation id: the client gets a generic `Detail` plus a
`traceId`, the server logs the real exception against that same id.

## Explicitly not doing

- **No shared `Umbral.ErrorHandling` package.** identity-access's extra
  `ErrorCategory.ServiceUnavailable` is thrown by
  `IdentityProviderRoleSyncException` and
  `IdentityProviderUserStateSyncException` — Keycloak sync failures. Only
  identity-access talks to Keycloak. The enum divergence is a real domain
  difference, not drift. Extracting a shared enum would force a category the
  other two services can never throw.
- **No change to the classified 4xx arms.** They are correct.
- **No `Type`-as-URI cleanup.** RFC 7807 accepts a relative reference; bare
  slugs are fine and they are already a published contract for the frontend.

## Phases

Phase 0 is serial because it fixes the shape every other phase copies. Phases
1a–1d touch four files in three services with no shared code between them —
genuinely independent, no merge conflicts, no ordering constraint. Phases 2a–2c
likewise.

### Phase 0 — pin the wire contract (serial, blocks everything) — **AGREED**

The four files copy this shape verbatim. Nothing else in the plan is safe to
start until they do — that's how they drifted last time.

#### REST: the two rewritten arms

```jsonc
// 500 — the `_ =>` arm
{
  "type": "internal-error",
  "title": "An unexpected error occurred.",
  "detail": "An unexpected error occurred.",   // was: exception.Message
  "status": 500,
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"  // new
}

// 401 — the UnauthorizedAccessException arm
{
  "type": "unauthorized",
  "title": "Unauthorized.",
  "detail": "Unauthorized.",                   // was: exception.Message
  "status": 401,
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"  // new
}
```

`traceId` goes on the 500 and 401 arms only. The classified 4xx arms keep their
authored `Detail` and get no `traceId` — the client already knows what went
wrong, and there is nothing to correlate against because nothing is logged.

Emit it as `problem.Extensions["traceId"]`, not a new `Problem()` parameter, so
the classified arms are untouched. Source, in this order:

```csharp
// TraceId, not Id: the bare 32-hex trace-id is what log backends index.
Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier   // never null: TraceIdentifier is always set
```

#### Hub: the `"ERROR"` arm only

```jsonc
{
  "code": "ERROR",
  "message": "An unexpected error occurred.",
  "traceId": "...",
}
```

The five curated `UPPER_SNAKE` codes and the category-derived codes keep
`exception.Message` and emit no `traceId` — same reasoning as the 4xx arms, same
published mobile contract. `traceId` is therefore nullable on `HubErrorPayload`
and must be omitted from the JSON when null
(`JsonIgnoreCondition.WhenWritingNull`) so the existing five payloads serialize
byte-identical to today.

There is no `HttpContext` inside a hub invocation, so the fallback differs:

```csharp
Activity.Current?.TraceId.ToString() ?? invocationContext.Context.ConnectionId
```

**Which side wins is settled: a real W3C id, not `ConnectionId`.** Spike S1
(`backend/plans/otel-wiring.md`) resolved this against a real WebSocket client. With OpenTelemetry
wired, `AddAspNetCoreInstrumentation()` registers the `Microsoft.AspNetCore.SignalR.Server`
`ActivitySource` itself (no explicit `AddSource` is needed), each hub invocation opens its own
activity carrying a real W3C trace id — a **new root trace**, not a child of the connection's HTTP
request activity — so `Activity.Current` is non-null and the hub emits the same bare 32-hex trace id
as the REST path. The `ConnectionId` fallback is **not** dead: it still runs whenever no listener is
registered, which is exactly what `DomainExceptionHubFilterTests`' `…_WithoutActivity_FallsBackToConnectionId`
case (line 73) exercises by constructing the filter directly with `Activity.Current = null` — it never
boots the `WebApplicationFactory`, so no OTel registration reaches it. In a real running hub, the W3C
id wins.

`BuildPayload` takes the code as a parameter (it already computes it) and only
the `"ERROR"` case substitutes the generic message.

#### Logging

One line, in the `_ =>` / `"ERROR"` arm, at `Error`:

```csharp
_logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
```

**The 401 arm does not log.** The draft said it should; it shouldn't. Every
`UnauthorizedAccessException` in the codebase is thrown deliberately by
`AuthorizationBehaviour`, `CurrentActor`, or a handler guard — nine of the ten
throw sites are parameterless, so the "real exception" is a framework default
string with no stack of interest. It is an expected client-side condition on a
hot path, and `LogError` on it is a noise generator that trains people to ignore
the error stream. It still gets the generic `Detail` and a `traceId`, because
uniformity across the two non-classified arms is worth more than the framework's
message.

#### Wiring

- The three handlers: `AddExceptionHandler<T>` resolves from DI, so
  ctor-injecting `ILogger<ProblemDetailsExceptionHandler>` needs no registration
  change.
- The hub filter: `options.AddFilter<DomainExceptionHubFilter>()`
  (`session-operations-service/src/Api/DependencyInjection.cs:24`, not `:70`)
  resolves through `ActivatorUtilities.GetServiceOrCreateInstance`, which
  ctor-injects from DI. `ILogger<DomainExceptionHubFilter>` works with no
  registration change either. Its members drop `static`; the class stays
  `sealed`.

### Phase 1 — apply (four agents, fully parallel)

Each: inject `ILogger<ProblemDetailsExceptionHandler>` via ctor, set `traceId`
in `Extensions` on the `_ =>` and `UnauthorizedAccessException` arms, make both
emit the generic `Detail`, and `LogError` the real exception on `_ =>` only.
Phase 0 is the contract; where this table and Phase 0 disagree, Phase 0 wins.

|        | File                                                   | Note                                                                                                                                                                                                                              |
| ------ | ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1a** | identity-access `ProblemDetailsExceptionHandler.cs`    | keep `ServiceUnavailable` arms untouched                                                                                                                                                                                          |
| **1b** | mission-design `ProblemDetailsExceptionHandler.cs`     | —                                                                                                                                                                                                                                 |
| **1c** | session-operations `ProblemDetailsExceptionHandler.cs` | —                                                                                                                                                                                                                                 |
| **1d** | session-operations `DomainExceptionHubFilter.cs`       | `BuildPayload` takes the code; generic message only on `"ERROR"`. The five curated `UPPER_SNAKE` codes and their messages are a published mobile contract — do not touch. Filter is `static`, so it needs `ILogger` injected too. |

No registration change is needed for any of the four — confirmed in Phase 0 §
Wiring.

### Phase 2 — test (three agents, parallel; each depends only on its own Phase 1)

Per service, add to the existing `ProblemDetailsExceptionHandlerTests`:

1. `TryHandleAsync_UnclassifiedException_DetailDoesNotEchoMessage` — throw
   `new Exception("SECRET-CONNECTION-STRING")`, assert `Detail` does not contain
   it.
2. `TryHandleAsync_UnclassifiedException_LogsTheException` — assert `LogError`
   called once with the exception.
3. `TryHandleAsync_UnclassifiedException_EmitsTraceId`.

Plus, for session-operations only, the hub-filter equivalents.

The existing reflection coverage test must stay green untouched — it asserts the
4xx arms, which we are not changing. If it goes red, Phase 1 overreached.

**Mutation-check each test**, per the standard set in `b029e36`: delete the
`LogError` call, confirm the test goes red; restore. A logging test that passes
without the logger is the exact failure mode that commit was written to kill.

### Phase 3 — verify and ship (serial)

- `dotnet test` per service, all suites, not just the filtered handler tests.
- Grep for stragglers: `grep -rn 'exception.Message' services/*/src/Api/` should
  return only the classified-arm uses.
- Squash the branch to one commit, then PR (per repo convention) using
  safe-pr-creator skill.

## Effort

Four small files, three test files. Phase 0 is a paragraph. The parallelism is
real but the whole thing is under an hour — the value is in Phase 0 keeping the
four copies identical, and in Phase 2's mutation check making the logging
assertion mean something.
