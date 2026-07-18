# Plan — OpenTelemetry wiring + Seq

Status: Phase 0 fully resolved (S1, S2, S3); Phases 1, 2, 3, 4 and 5 implemented. All four components emit
traces and logs into a Seq container, the plan's acceptance test passes end-to-end, and Phase 5 has paid
off PR #123's doc debt. **This plan is complete.**
Depends on: PR #123 (`423dc80`) merged — this plan makes its `traceId` real.

## Why

`ProblemDetailsExceptionHandler.cs:72` and `DomainExceptionHubFilter.cs:63` both read
`Activity.Current?.Id ?? <fallback>`. There is no `ActivitySource` listener anywhere in this
backend, so ASP.NET Core never creates a per-request `Activity`, so the fallback always wins.
PR #123 therefore emits `0HN7ABC123XYZ:00000001` — an id that is per-process, per-connection,
and greppable only via `docker logs`.

This plan registers the listener. The handler and hub filter are **not modified**: they were
written to upgrade for free the day tracing exists. That is the whole point.

Three concrete gains, in priority order:

1. `Activity.Current` goes non-null → `traceId` becomes a W3C trace id, matching what the
   PR #123 body and `plans/error-detail-leak-fix.md` already (incorrectly) advertise.
2. Every service-to-service call already goes through a typed `AddHttpClient` (8 of them in
   `session-operations-service/src/Infrastructure/DependencyInjection.cs`), so `IHttpClientFactory`'s
   `DiagnosticsHandler` injects `traceparent` automatically. One trace id spans the whole call
   chain with **zero call-site changes**.
3. Logs exported over OTLP carry `TraceId`/`SpanId` as structured fields, so `traceId` becomes
   queryable instead of greppable — closing the gap HANDOFF names as "greppable, not queryable".

## Non-goals

- Metrics. Traces + logs only. `PerformanceBehaviour` stays as-is.
- RabbitMQ trace context. `RabbitMqIntegrationEventPublisher` uses the raw client; propagating
  context across the broker needs manual header injection. Out of scope — the published events
  are best-effort secondary facts (HU-33B, D-3), not on a debugging critical path.
- Replacing `ILogger<T>`. No Serilog. Call sites are untouched everywhere.

## Decision: OTel, not Serilog+Seq

Serilog+Seq would fix gain #3 and neither #1 nor #2 — `Activity.Current` stays null, so Seq would
faithfully index `0HN7ABC123XYZ:00000001`. Serilog enrichers that read trace context (e.g.
`Serilog.Enrichers.Span`) read `Activity.Current` too, so they inherit the same null.

.NET's `ILogger` exports over OTLP natively (`builder.Logging.AddOpenTelemetry`), and Seq ingests
OTLP directly. So Seq stays as the UI **without** Serilog in the dependency graph.

---

## Phase 0 — spikes (no code shipped)

These three are genuinely unknown. Resolve before writing wiring code; each one changes a later phase.

**S1 — Does `Activity.Current` become non-null inside `DomainExceptionHubFilter`? → YES. RESOLVED.**
Method: a net10.0 spike mirroring `AddSignalR(o => o.AddFilter<T>())`, a real WebSocket client, and a
hub method that throws; the filter read `Activity.Current` exactly where the real one does.

- With no OTel registered, `Activity.Current` is null and the `ConnectionId` fallback wins — the
  status quo this plan exists to fix.
- With `AddAspNetCoreInstrumentation()` alone, `Activity.Current` is **non-null**, its source is
  `Microsoft.AspNetCore.SignalR.Server`, its display name is `<Hub>/<Method>`, and each invocation
  carries its own W3C trace id (`00-…-01`).
- **`ObservabilityExtensions` must NOT call `.AddSource("Microsoft.AspNetCore.SignalR.Server")`.**
  `OpenTelemetry.Instrumentation.AspNetCore` registers that source itself — confirmed both empirically
  and by the source name being a literal in the shipped assembly. An explicit `AddSource` is redundant.

So the hub emits a real W3C trace id, same as the REST path. Phase 5's doc update should say exactly
that, rather than the `ConnectionId` caveat the plan hedged for. Note the hub-invocation activity is a
**new root trace**, not a child of the connection's HTTP request activity.

`DomainExceptionHubFilterTests.cs:73` — the `ConnectionId` fallback assertion — is **not** at risk:
that test constructs the filter directly with a fake `HubCallerContext` and sets `Activity.Current = null`
itself. It never boots `SessionOperationsApiWebApplicationFactory`, so no OTel registration can reach it.
(The issue body assumed it did.) The env-var guard below still matters, for exporter startup cost.

**S2 — Does YARP propagate `traceparent` when the *gateway* originates the trace? → RESOLVED: YES.
No `RequestTransform` is needed. Do not add one.**

The hypothesis below was **wrong**, and the first spike that appeared to confirm it was confounded
(it drove requests with an OTel-instrumented in-process `HttpClient`, which stamped its own
`traceparent` on the way *into* the proxy). Re-run driving both cases with `curl`, whose control run
straight to the backend confirms it sends no `traceparent` of its own.

> ~~YARP's forwarder uses a `SocketsHttpHandler` it constructs itself, not an `IHttpClientFactory`
> client, so it likely has no `DiagnosticsHandler` and will not inject a `traceparent` the gateway
> created.~~

Spike: net10.0 web app, `Yarp.ReverseProxy` 2.3.0 + OTel 1.16.0 (`AddAspNetCoreInstrumentation`,
`AddHttpClientInstrumentation`, `AlwaysOnSampler`), proxying to a bare echo endpoint that reports the
raw `traceparent` it received. Throwaway; deleted.

- **Case A — no inbound `traceparent`.** The backend received one, and its trace id **equalled the
  gateway's `Activity.Current.TraceId`**. The gateway-originated trace does reach the backend.
- **Case B — client-supplied `traceparent`.** The trace id was **preserved** across the hop; the
  parent-id field was replaced with the gateway's own outbound span id, which is correct W3C
  behaviour. `tracestate` (`vendor=abc,x=1`) passed through intact.

**Mechanism** — two distinct behaviours the original hypothesis conflated. Isolated by re-running with
`DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION=0`:

- YARP copies inbound headers **verbatim** — with propagation disabled, Case B's header arrived
  byte-for-byte (parent-id `2222…` unchanged) and Case A's arrived **not at all**.
- The runtime's `DiagnosticsHandler`, inside `SocketsHttpHandler`, is what actually **creates and
  overwrites** `traceparent` from `Activity.Current` — and it *is* in YARP's handler chain, contrary
  to the hypothesis. It runs at **send time, after `RequestTransform`s**: a transform mirroring the real
  `TrustedHeadersTransform` observed `<none>` on `ProxyRequest` in Case A, yet the backend still
  received the gateway's trace id.

Consequences for Phase 3:

- **Injection does not depend on OTel.** It still happened with OTel entirely unregistered. What OTel
  changes is the sampled flag: unregistered, Case A's activity is not recorded and the header goes out
  `-00`; with `AlwaysOnSampler` it goes out `-01`.
- The plan's proposed `DistributedContextPropagator.Current.Inject(...)` `RequestTransform` would be
  **redundant, and its header would be overwritten anyway** by `DiagnosticsHandler` at send time.
- The real `TrustedHeadersTransform` only adds `X-User-*` and removes `Authorization`; it never touches
  `traceparent`. Verified against a lookalike in the spike — injection unaffected.
- Two caveats worth knowing. Propagation is off if `DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION=0`
  or the `System.Net.Http.EnableActivityPropagation` `AppContext` switch is false (both default to on;
  neither is set in this repo). And the **gateway's sampler overrides the caller's sampling decision** —
  an inbound `-00` left the gateway as `-01` under `AlwaysOnSampler`.
- **A `ParentBased` switch has to happen gateway-first.** Measured while the gateway was still
  un-instrumented: its activity was not recorded, so it forwarded Case A's header with the sampled flag
  `-00`. Every service pins `AlwaysOnSampler`, so they recorded anyway; under `ParentBased` that remote
  `-00` parent would have silently dropped every downstream span — a failure looking exactly like "OTel
  isn't wired". **Phase 3 retires that specific hazard**: the gateway now records under `AlwaysOnSampler`
  and forwards `-01` (verified end-to-end — its span and mission-design's share one trace id). The
  ordering constraint is what survives: whoever changes the sampler must instrument the trace's root
  first, or re-create exactly this failure. `AlwaysOnSampler` stays regardless — Phase 0 pinned dev =
  AlwaysOn. (The `-00` and the `-01` are both measured; the `ParentBased` consequence is derived from
  OTel's documented sampler semantics, not exercised here.)

**S3 — Seq's OTLP ingest endpoint and minimum version. → RESOLVED. Pin `datalust/seq:2025.2`.**
Verified against a live `2025.2` container by exporting from a real app and reading the events back:

- `POST /ingest/otlp/v1/traces` and `POST /ingest/otlp/v1/logs` both ingest. Both are served on the
  ingestion port (5341) and on the UI port. `…/v1/metrics` is **404** — Seq does not ingest OTLP
  metrics at all, which happens to match the metrics non-goal.
- So `OTEL_EXPORTER_OTLP_ENDPOINT=http://seq:5341/ingest/otlp` (base; the exporter appends `/v1/<signal>`).
- **`OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` is mandatory.** The .NET exporter defaults to gRPC,
  and Seq speaks OTLP over HTTP only: gRPC fails with `HTTP_1_1_REQUIRED` and **fails silently** — the
  app logs nothing, telemetry just never arrives. Omitting this variable in Phase 4 is a silent no-op,
  which is the exact failure mode this plan warns about for a Seq container with nothing pointed at it.
- **`ACCEPT_EULA=Y` alone is not enough to boot 2025.2.** It aborts with *"No default admin password was
  supplied"*. Phase 4's compose block needs `SEQ_FIRSTRUN_NOAUTHENTICATION=true` (dev) or
  `SEQ_FIRSTRUN_ADMINPASSWORD`. The snippet in Phase 4 below is corrected accordingly.
- Minimum version deliberately not pinned down: an empty-body probe returns success on `2024.1` too,
  which proves nothing about real ingest. We pin an explicit tag and verified *that* tag end-to-end.

Note OTLP export errors are invisible unless OTel self-diagnostics are enabled (an `OTEL_DIAGNOSTICS.json`
in the process working directory). Do not read "no error output" as "telemetry arrived".

Phase 0 also pins one contract question:

- **Sampling.** Dev = `AlwaysOnSampler`. Anything else is a later decision; do not build a
  config surface for it now.

---

## Phase 1 — session-operations-service (the reference implementation)

Chosen first because it is the only service with all three surfaces: SignalR hub, typed
`HttpClient`s, and RabbitMQ.

Packages (all four on `src/Api/Api.csproj`):

    OpenTelemetry.Extensions.Hosting
    OpenTelemetry.Instrumentation.AspNetCore
    OpenTelemetry.Instrumentation.Http
    OpenTelemetry.Exporter.OpenTelemetryProtocol

**Correction: central package management already exists** — each service has its own
`src/Directory.Packages.props` with `ManagePackageVersionsCentrally=true`. Versions go there as
`PackageVersion`; the `.csproj` carries a version-less `PackageReference`. No new-CPM PR is needed.

Pin `1.16.0` for the OpenTelemetry packages, and add an explicit `OpenTelemetry.Api` `1.16.0`:
`Npgsql.OpenTelemetry` drags in `OpenTelemetry.Api` `1.14.0`, which is vulnerable to
**GHSA-g94r-2vxg-569j** — unbounded allocation while parsing inbound `traceparent`/`baggage` headers,
i.e. precisely the code path this plan switches on, on an internet-facing edge. Patched in `1.15.3`.
(Unrelated and pre-existing on `develop`: `Microsoft.OpenApi` `2.0.0` carries a *high* advisory,
transitively via `Microsoft.AspNetCore.OpenApi`. Out of scope here; worth its own issue.)

New file `src/Api/ObservabilityExtensions.cs`, marked `[ExcludeFromCodeCoverage]`:

- `builder.Services.AddOpenTelemetry().WithTracing(t => t
      .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql().AddOtlpExporter())`
- `builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.AddOtlpExporter(); })`
- No SignalR `AddSource` — S1 shows `AddAspNetCoreInstrumentation()` already registers it.
- No `ConfigureResource`/hardcoded service name: `OTEL_SERVICE_NAME` reaches both the tracer and the
  logger provider on its own (verified — log records and spans carry the same `service.name`).
- Endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT`; **if unset, register nothing at all** so the
  existing `make test` / `gate` runs and the integration-test `WebApplicationFactory` boot are
  byte-identical to today.

One call added to `AddWebServices` in `src/Api/DependencyInjection.cs` (already
`[ExcludeFromCodeCoverage]`, so the ADR-0005 branch-coverage gate is unaffected).

Also add `Npgsql.OpenTelemetry` → `.AddNpgsql()` for DB spans. Prefer this over
`OpenTelemetry.Instrumentation.EntityFrameworkCore`, which is still prerelease.

### Test impact — verify, do not assume

The four `traceId` tests from PR #123 all do `using var activity = new Activity(...).Start()` and
assert `traceId == activity.Id`. They construct an `Activity` themselves, so **they already exercise
the non-null path and will not break.**

The one to check is `DomainExceptionHubFilterTests.cs:73`, which asserts the `ConnectionId`
fallback. If OTel is registered in the `WebApplicationFactory` that boots that integration test,
`Activity.Current` becomes non-null and that assertion flips. The `OTEL_EXPORTER_OTLP_ENDPOINT`
guard above is what keeps it green — **confirm empirically**, do not trust the guard by reading it.

Then: `make -C backend test SVC=session-operations-service`, `make gate SVC=session-operations-service`,
`make structure-guard SVC=session-operations-service`. There is no CI; every gate is manual.

---

## Phase 2 — mission-design-service, identity-access-service — IMPLEMENTED

`ObservabilityExtensions.cs` ported to both, alongside the same six version-less `PackageReference`s
and matching `PackageVersion` pins in each service's own `src/Directory.Packages.props` (central
package management is per-service). `builder.AddObservability()` is the first line of `AddWebServices`
in both.

The instruction to port it "minus the SignalR source" was **vacuous**: S1 established that
`AddAspNetCoreInstrumentation()` registers that `ActivitySource` itself, so the reference file never
had an `AddSource` line to remove. Only the doc comments differ per service — the reference names
session-ops' `DomainExceptionHubFilter`, and neither of these two has a hub filter (mission-design's
`WebhookHub` is never registered; identity-access has no hubs).

Both services use Npgsql/EF Core, so `.AddNpgsql()` transferred unchanged. Both keep the
`OTEL_EXPORTER_OTLP_ENDPOINT` unset-guard. `AddHttpClientInstrumentation()` is kept in both:
identity-access has one typed client (`KeycloakAdminService`); mission-design has none today, and the
instrumentation costs nothing until it does.

The wrinkles, as they landed:

- **Neither `identity-access-service` nor `session-operations-service` has an `appsettings.json`
  at all.** Only mission-design does. No appsettings entry was added anywhere — configuration is
  `OTEL_*` env vars in compose (Phase 4).
- `mission-design-service/src/Api/DependencyInjection.cs` is **not** `[ExcludeFromCodeCoverage]`,
  unlike the other two, so it is the only gate that could move. Both new files are marked
  `[ExcludeFromCodeCoverage]`, and the added `AddObservability()` call is covered by the integration
  tests that boot the factory (34 hits in the cobertura report — confirmed, not assumed).

Gates re-run for both: mission-design 475 tests, line coverage **96.6%** (was 96.6%);
identity-access 261 tests. `structure-guard` was OK for both. Those historical
results must be rerun against the current aggregate branch-coverage gate. The unset-guard was
proven **both ways** in each service with a throwaway probe (`TracerProvider` null when the endpoint
is unset, non-null when set) — asserting only the null case would pass even if `AddObservability` were
dead code. Probes deleted. `OpenTelemetry.Api` resolves to `1.16.0` in both, absent from
`dotnet list package --vulnerable --include-transitive`.

Note `backend/structure.md` needed **no edit**: its `ObservabilityExtensions.cs` entry sits in the
generic `<service-name>/` template tree, which already covers all three services. `structure-guard.sh`
reads only `services/*/src/Application`, never `structure.md`.

Note `mission-design`'s `LoggingBehaviour` logs `{@Request}` at Information. Commands are `record`s,
so their `ToString()` already prints every property to console today — OTLP export changes the
format and adds retention, not the exposure. Flag for a retention decision; not a blocker here.

---

## Phase 3 — api-gateway — IMPLEMENTED

**Correction: not "the same four packages" as Phase 1, and not a verbatim port.** The gateway owns no
database, so `Npgsql.OpenTelemetry`, the `using Npgsql;` and the `.AddNpgsql()` line are all dropped.
Four `PackageReference`s only: `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `.Extensions.Hosting`,
`.Instrumentation.AspNetCore`, `.Instrumentation.Http`.

**No explicit `OpenTelemetry.Api` pin here, deliberately.** The pin exists in the three services solely
because `Npgsql.OpenTelemetry` drags in `OpenTelemetry.Api` `1.14.0` (GHSA-g94r-2vxg-569j). Without that
package nothing pulls it downward: the gateway's only edge into it is
`OpenTelemetry.Api.ProviderBuilderExtensions 1.16.0 → OpenTelemetry.Api >= 1.16.0`. Verified, not
assumed — `dotnet list api-gateway/src/ApiGateway.csproj package --include-transitive` resolves
`OpenTelemetry.Api` to `1.16.0`, and `--vulnerable --include-transitive` reports **no vulnerable packages
at all** (the `Microsoft.OpenApi` advisory that the services carry does not reach the gateway). Adding
the reference would have been redundant, so it was not added.

S2 resolved: YARP already propagates, so **this phase is packages + `AddObservability()` only — no
`RequestTransform`, no propagation code**. `builder.AddObservability()` is the first line of
`AddGatewayServices`. `AddGatewayServices` is **not** `[ExcludeFromCodeCoverage]`, but the gateway is
absent from `GATEABLE_SERVICES` (`Makefile:55`), so no coverage threshold moves.

`backend/structure.md` **did** need an edit here, unlike Phase 2: its `api-gateway/src/` block enumerates
files individually rather than reusing the generic `<service-name>/` template tree. `structure-guard.sh`
reads only `services/*/src/Application`, so it would not have caught the omission.

### What the forwarder actually emits — resolved empirically

Open question was whether `AddHttpClientInstrumentation()` records YARP's forwarded request, and whether
YARP exposes its own `ActivitySource` needing an explicit `.AddSource()`. Answered by exporting to a live
Seq and reading the events back, not by reading the handler chain:

- **`AddHttpClientInstrumentation()` covers the forwarded request.** One `Client` span,
  `GET http://mission-design-service:8080/api/missions/difficulties`, `ActivitySource = System.Net.Http`.
  YARP's self-constructed `SocketsHttpHandler` carries the runtime `DiagnosticsHandler`, which is the
  source that instrumentation listens to.
- **No `.AddSource()` is needed and nothing is duplicated.** Only two sources produced spans:
  `Microsoft.AspNetCore` and `System.Net.Http`. The `Yarp.ReverseProxy.Forwarder.HttpForwarder` entries
  in Seq are **`ILogger` log records, not spans** — the distinction that S2's first spike blurred.
- **Acceptance test passes.** A `curl` carrying no `traceparent` produced one trace holding 5 spans: the
  gateway's `Server` span, its `Client` span to mission-design, mission-design's `Server` span, and two
  `Client` spans for the JwtBearer backchannel's Keycloak metadata + JWKS fetch (expected, not a leak).
  Mission-design's `GetDifficultyCatalogQuery` log record sits under the same trace id.
- **The unset-guard proven both ways in the real container**, since the gateway has no
  `WebApplicationFactory`: with `OTEL_*` set, the spans above; with the gateway recreated and
  `OTEL_EXPORTER_OTLP_ENDPOINT` unset (`0` OTEL vars in `docker exec … env`), the same `curl` produced
  **zero** gateway events while mission-design still recorded its own root trace. The one stray
  `api-gateway` event was the *previous* container's `Application is shutting down...` flush, timestamped
  0.37 s before the new container started.

Verified with a throwaway, uncommitted compose override plus a throwaway Seq; both deleted. The gateway
has no `make gate` target, so the standing checks are `dotnet build api-gateway/src/ApiGateway.csproj`
and `AuthPath.EndToEndTests` (3/3 pass).

---

## Phase 4 — Seq in docker-compose — IMPLEMENTED

`seq` added to `backend/docker-compose.yml` exactly as the block below specifies, and the three
`OTEL_*` variables added to the four app services (**not** to postgres/keycloak/rabbitmq, and not to
the `auth-probe`, which stays un-instrumented). No `.cs`, `.csproj`, `Directory.Packages.props` or
`appsettings.json` was touched — the whole phase is compose plus docs, as designed.

    seq:
      image: datalust/seq:2025.2         # pinned per S3; never `latest`
      environment:
        ACCEPT_EULA: Y
        SEQ_FIRSTRUN_NOAUTHENTICATION: "true"   # 2025.2 refuses to boot without this (or an admin password)
      ports:
        - "8341:80"                      # UI + query API
        - "5341:5341"                    # OTLP/ingest

Then, on all four app services:

      OTEL_EXPORTER_OTLP_ENDPOINT: http://seq:5341/ingest/otlp
      OTEL_EXPORTER_OTLP_PROTOCOL: http/protobuf   # Seq is HTTP-only; the gRPC default fails silently
      OTEL_SERVICE_NAME: <service-name>

`OTEL_SERVICE_NAME` is what separates the four streams in Seq; without it everything lands as
`unknown_service`. Confirmed reaching **both** the tracer and the logger provider with no
`ConfigureResource` call: spans and log records from all four services carry their own `service.name`.

**`depends_on: seq` (`condition: service_started`) was added to all four services.** Not required — the
exporter tolerates a missing collector — but on a cold `up` the first export batch would otherwise hit a
container that does not yet exist, and a failed batch is *dropped*, not retried. `service_started` is
cheap (Seq boots in ~1 s) and costs the E2E suite nothing. A `service_healthy` gate was rejected: it would
block `up -d` on a healthcheck while still not proving OTLP ingest readiness, which is all the retry
already buys.

`docker-compose.override.yml` needed **no entry**. Seq is an image, not a build, like postgres/keycloak/
rabbitmq; and Compose merges `environment:` maps across files, so the dev `dotnet watch` loop inherits all
three `OTEL_*` vars from the base file for free. Verified with `docker compose config`, not assumed.

### Interaction with `AuthPath.EndToEndTests` — the thing this plan did not anticipate

That suite boots `docker compose -f docker-compose.yml -f api-gateway/tests/docker-compose.auth-tests.yml`.
The first file is the one this phase edits, so the E2E run now starts a Seq container and all four app
containers export during the test. Checked, not assumed:

- **Timeouts are unaffected.** `ComposeStackFixture`'s three 2-minute `WaitUntilAsync` budgets all start
  *after* `up -d --build` returns, and none of them polls a Seq-dependent surface. **3/3 pass.**
- **`seq` is not culled by `down -v --remove-orphans`.** It is a *defined* service in the merged config
  (`docker compose … config --services` lists it), so it is removed as a normal service, not an orphan.
  It declares no named volume, so `-v` still wipes only `postgres-data` — no change in blast radius.
- **The unset-guard is no longer exercised in-container by that suite**, because `api-gateway` now receives
  `OTEL_*` from the base compose file. It was never exercised there before by design either — the guard's
  real coverage is the in-process `WebApplicationFactory` probes of Phases 1–2 and the Phase 3 container
  recreation, both recorded above. Do not claim this suite proves the guard.

`make test` / `make gate` are untouched: they run the in-process `WebApplicationFactory` on the host, which
never reads compose. Only the E2E container run changes.

### Acceptance test for the whole plan — PASSES

Trigger: `docker compose stop postgres`, then `GET /api/missions` through the gateway with a real Keycloak
admin bearer token. Mission-design's Npgsql connection fails, nothing classifies it, and
`ProblemDetailsExceptionHandler.Unhandled` fires. The client received:

    {"type":"internal-error","title":"An unexpected error occurred.","status":500,
     "detail":"An unexpected error occurred.",
     "traceId":"00-7d7f1929d8de9ca4fa5fe0c4eeb29412-874052e02b8dd01f-01"}

A real W3C id with the sampled flag on — the format PR #123's body already advertised but could not
produce. Reading events back out of Seq's own API (`GET /api/events?count=1000`), **15 events sit under
trace `7d7f1929d8de9ca4fa5fe0c4eeb29412`**, spanning both processes:

| | |
|---|---|
| api-gateway `Server` span | `GET /api/missions/{**catch-all}` |
| api-gateway `Client` span | the YARP forwarded request |
| mission-design `Server` span | `GET api/missions` |
| mission-design `Client` span | `postgresql` — the failing DB call |
| mission-design `ERROR` log | `Unhandled exception. TraceId: {TraceId}`, carrying the exception |

That last record is `ProblemDetailsExceptionHandler.cs:60`, and its OTLP `TraceId`/`SpanId`
(`7d7f1929…` / `874052e02b8dd01f`) are exactly the trace and span the client was handed. Gateway span,
service span, and the handler's `LogError` under one trace id — the acceptance test as written.

All four `service.name` values were confirmed present in Seq (`api-gateway`, `mission-design-service`,
`identity-access-service`, `session-operations-service`), on spans and on log records.

Two parsing traps, both real, both hit this session:

- **Spans carry `Elapsed`; log records do not.** Seq labels plain log records `SpanKind: Internal`, which
  will make you count them as spans.
- **`service.name` lives in `Resource`, not `Properties`**, and Seq nests dotted keys — it is
  `Resource[Name=service].Value.name`, not a flat `service.name`.

Worth knowing: the handler's `{TraceId}` log property collides with Seq's reserved `TraceId` field, so Seq
renders its own bare 32-hex trace id rather than whatever the handler logged. That shadowing is why the
handler originally passing the full `Activity.Current.Id` (`00-…-01`) went unnoticed — the log looked right
while the id returned to clients was unsearchable. Both handlers now emit `Activity.Current.TraceId`, so
the logged value, the returned value and Seq's index agree.

---

## Phase 5 — pay off PR #123's doc debt — IMPLEMENTED

Phases 1–4 landed, so the services now genuinely produce the W3C id PR #123's body and
`plans/error-detail-leak-fix.md` advertised. The debt is therefore paid by **recording that it is now
true**, not by amending the docs down — exactly the branch the two items below hoped for.

- `plans/error-detail-leak-fix.md` gained a status banner tying its `traceId` examples to
  issue #124 having landed: they were aspirational when PR #123 shipped (no listener → `Activity.Current`
  null → the per-process `0HN7…:00000001` / `ConnectionId` fallbacks actually emitted), and the id is now
  real. The banner also corrects their *shape*: the handlers emit `Activity.Current.TraceId`, the bare
  32-hex `7d7f1929d8de9ca4fa5fe0c4eeb29412`, not the full `traceparent` those examples showed — a client
  has to be able to paste the value into a log search unedited.
- The hub-id question S1 left open is now settled **in that doc**: the SignalR hub emits a **real W3C
  id, not `ConnectionId`**. `AddAspNetCoreInstrumentation()` registers the
  `Microsoft.AspNetCore.SignalR.Server` source, each invocation is a new root trace with its own W3C id,
  so `Activity.Current` is non-null in a running hub. The `ConnectionId` fallback is explicitly **not**
  called dead — it still exists in `DomainExceptionHubFilter` and `DomainExceptionHubFilterTests`
  still asserts it, because that test constructs the filter directly with `Activity.Current = null` and
  never boots the factory.
- **DOCS ONLY.** No `.cs`, `.csproj`, `Directory.Packages.props`, `appsettings.json` or compose file was
  touched. `ProblemDetailsExceptionHandler.cs` and `DomainExceptionHubFilter.cs` stay untouched — the
  point of issue #124. PR #123 is already merged, so its once-aspirational body was left as-is (now
  accurate in substance); only PR #125's body was updated.

---

## Sequencing note

Phases 1–3 are independently shippable and each is a small PR. Phase 4 is what makes any of it
useful. Do **not** ship Phase 4 first: a Seq container with no exporters pointed at it is a
container that looks like observability and isn't.

Per repo rules, squash each branch to one commit before opening its PR.
