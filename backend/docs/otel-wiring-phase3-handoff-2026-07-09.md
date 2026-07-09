# Handoff — OpenTelemetry wiring (issue #124), Phase 3 + code review

Date: 2026-07-09
Branch: `feat/otel-wiring-session-ops`, **one commit**, open as PR #125 against `develop`, unmerged.

Canonical spec: [`backend/plans/otel-wiring.md`](../plans/otel-wiring.md) — already updated in place with every
Phase 3 finding. Prior handoff: [`otel-wiring-phase01-handoff-2026-07-09.md`](./otel-wiring-phase01-handoff-2026-07-09.md),
whose status table now covers Phases 1–3.

**Read the plan doc and PR #125's body first.** This file records only what neither does: the code-review
findings, the two facts that are now wrong in already-written text, and what is still open.

> **Never trust a SHA written down here.** Each phase was amended into the one commit and force-pushed, so
> the branch SHA has now changed six times
> (`732a834` → `21c186e` → `fda18ce` → `82df757` → `fad3745` → `5c41e1c` → `feefa21`, the last being Phase 5).
> Every superseded SHA still resolves as a dangling object and returns stale content. Read `git rev-parse HEAD`.

---

## Status

| Item | State |
|---|---|
| Phases 1–3 (three services + api-gateway) | **Implemented**, all in one commit. Gates green; gateway build + `AuthPath.EndToEndTests` 3/3 |
| Phase 3's open question — `OpenTelemetry.Api` pin on the gateway | **Resolved: no pin needed.** Not added |
| Phase 3's real unknown — YARP forwarder span | **Resolved: `AddHttpClientInstrumentation()` covers it.** No `.AddSource()`, no duplicates |
| `structure.md` edit for the gateway | **Done** (Phase 2 needed none; Phase 3 did) |
| PR #125 body | **Updated** via REST `PATCH` to describe Phases 1–3 |
| `/code-review` on the diff | **Done — 7 findings; 1 fixed, 6 open.** See below |
| `/security-review` on the diff | **NOT RUN.** Explicitly deferred by the user this session |
| Phase 4 (Seq in compose) | **Implemented**, amended into the same commit. Acceptance test for the whole plan passes — gateway span, service span and the handler's `LogError` under one trace id, read back out of Seq's API |
| Phase 5 | **Implemented** (docs only). `plans/error-detail-leak-fix.md` now records the `00-…-01` id as real and produced (measured `00-7d7f1929d8de9ca4fa5fe0c4eeb29412-874052e02b8dd01f-01`), and that the hub emits a real W3C id (S1), not `ConnectionId` — fallback explicitly not called dead. Plan doc + PR #125 body updated to match. **This plan is complete.** |

---

## Two recorded facts that were WRONG — both now FIXED

Both were corrected in the amended commit (`fad3745`), in comments and prose only. Recorded here because the
reasoning is worth keeping, and because superseded revisions of this file said they were open.

### 1. ~~A stale comment sits in two service files~~ — FIXED

`services/mission-design-service/src/Api/ObservabilityExtensions.cs` and
`services/identity-access-service/src/Api/ObservabilityExtensions.cs` both carry, above `SetSampler`:

> `// AlwaysOn, not the default ParentBased: the gateway is still un-instrumented and forwards traceparent`
> `// with the sampled flag off, which ParentBased would honour by dropping every span here (spike S2).`

Phase 3 made that false — the gateway now records and forwards `-01`. Both comments now say instead that
AlwaysOn is the pinned Phase 0 dev decision and that a future `ParentBased` switch must instrument a trace's
root before its children. The hazard is retired; the ordering constraint that produced it is not.
`session-operations`' copy has no sampler comment and needed nothing.

### 2. ~~The commit message and PR body repeat a claim the code review disproved~~ — FIXED

Both say `OpenTelemetry.Api` is pinned to 1.16.0 in each service "because `Npgsql.OpenTelemetry` drags in
1.14.0". The pin is **not load-bearing.** Verified empirically this session: removing *both* the
`PackageVersion` pin and the `PackageReference` from `session-operations` — the service that *does* carry
`Npgsql.OpenTelemetry` — still resolves `OpenTelemetry.Api` to **1.16.0**, with nothing OpenTelemetry-related
in `dotnet list package --vulnerable --include-transitive`.

Mechanism: NuGet takes the **highest** transitive floor, and
`OpenTelemetry.Exporter.OpenTelemetryProtocol 1.16.0 → OpenTelemetry 1.16.0 → OpenTelemetry.Api.ProviderBuilderExtensions 1.16.0 → OpenTelemetry.Api >= 1.16.0`
always beats Npgsql's `>= 1.14.0`.

The comment in all three `src/Directory.Packages.props`, the commit message, and PR #125's Security section now
say instead that the pin is **defence-in-depth, not the mechanism**, kept so a future downgrade of the OTel
stack cannot silently let Npgsql's floor win. The `PackageVersion` and `PackageReference` lines were left
byte-identical — the pin stays, only the false explanation went. Corollary worth knowing: the gateway, which
has no pin, is protected by that same edge — it was never the weak link the old phrasing implied.

---

## `/code-review` findings (high effort) — 7 reported, 1 fixed, 6 open

Ranked as reported. Locations are on the current tree.

1. ~~**`services/*/src/Directory.Packages.props:24`** — the GHSA comment's claim is false.~~ **FIXED** — comment
   rewritten in all three files; the pin itself deliberately kept. See above. CONFIRMED.
2. **all four `ObservabilityExtensions.cs`, the guard line** — the guard checks `OTEL_EXPORTER_OTLP_ENDPOINT`
   but **not `OTEL_EXPORTER_OTLP_PROTOCOL`**. Endpoint-set-but-protocol-unset registers everything and then
   exports nothing, silently, because the exporter defaults to gRPC and Seq is HTTP-only. **This is a live trap
   for Phase 4**: one forgotten env var yields a fully "instrumented" stack shipping zero telemetry. CONFIRMED.
3. **`services/*/src/Api/ObservabilityExtensions.cs`, the guard line** — the guard reads
   `builder.Configuration`, which includes ambient process env vars. A developer or CI runner with
   `OTEL_EXPORTER_OTLP_ENDPOINT` exported in the shell silently breaks the XML doc's "byte-identical to a
   pre-OpenTelemetry boot" guarantee: every container-backed integration test then starts the OTLP batch
   processor. CONFIRMED.
4. **`api-gateway/src/ObservabilityExtensions.cs:51`** — `AlwaysOnSampler` at the internet-facing edge makes an
   unauthenticated caller the sole controller of telemetry volume; the default batch queue (2048) drops real
   traces once saturated. Correct for dev (Phase 0 pinned dev = AlwaysOn); a hazard the day this faces a public
   edge. PLAUSIBLE.
5. **`services/*/src/Directory.Packages.props:12`** — the GHSA mitigation is a hand-copied pin across three
   props files, absent on the gateway, with no central props, no `CentralPackageTransitivePinningEnabled`, and
   no CI. Four components, four places to forget. PLAUSIBLE.
6. **`session-operations/.../ObservabilityExtensions.cs:50`** — `AddAspNetCoreInstrumentation()` under
   `AlwaysOnSampler` opens a Server span at the SignalR WebSocket upgrade that stays open, unexported, for the
   whole connection lifetime. session-operations is the only service with a hub. PLAUSIBLE.
7. **`api-gateway/src/ObservabilityExtensions.cs:61`** — batch exporters flush on shutdown, so an unreachable
   Seq stalls every container's graceful shutdown up to the exporter timeout, on every redeploy. PLAUSIBLE.

**One claim was refuted, do not re-raise it.** Three finder angles independently asserted that the guard reads
`IConfiguration` while the exporter reads only `Environment.GetEnvironmentVariable` — a config-source
asymmetry that would silently point the exporter at `localhost:4317`. That is **wrong**: OTel 1.16.0 binds
`OtlpExporterOptions` from `IConfiguration` (`BindConfigurationToOptions`, `s_configKeys_IOtlpExporterOptions`,
a hard dependency on `Microsoft.Extensions.Configuration.Binder`). Same source, no asymmetry.

Also refuted: that missing `IncludeFormattedMessage`/`ParseStateValues` degrades exported log records. Read back
from Seq, records carry their template **and** full structured properties (`ElapsedMilliseconds`, `StatusCode`,
`TraceId`) plus scope properties like `ConnectionId`, so `IncludeScopes` works and Seq renders templates
natively.

---

## What is still open

1. **`/security-review` on the PR #125 diff.** Deferred, not done. It remains the more relevant of the two: this
   diff turns on inbound `traceparent` parsing at the actual internet-facing edge. Note the review harness
   excludes DoS, so GHSA-g94r-2vxg-569j (unbounded allocation) is out of its scope by construction — the
   dependency-pinning findings above are where that risk actually lives. A worthwhile angle it *would* cover:
   SignalR passes `?access_token=<JWT>` in the query string (see
   `api-gateway/src/Transforms/WebSocketTokenExtractionTransform.cs`), and the gateway now records
   `url.query` — check whether a JWT can reach exported telemetry.
2. ~~**Phase 4**~~ — **DONE.** `seq` (`datalust/seq:2025.2`) plus the three `OTEL_*` vars on all four app
   services, in `backend/docker-compose.yml`. Compose and docs only; no `.cs`, `.csproj`,
   `Directory.Packages.props` or `appsettings.json` touched. Finding 2 was headed off in compose, not in the
   guard. `depends_on: seq` (`service_started`) added so a cold `up` does not drop its first export batch.
   `docker-compose.override.yml` needed nothing — Compose merges `environment:` maps, verified with
   `docker compose config`. **Phase 5 is now the only phase left.**

   Two consequences worth carrying forward. `AuthPath.EndToEndTests` boots the very compose file Phase 4
   edits, so its containers now receive `OTEL_*` and export during the suite: **it no longer exercises the
   unset-guard in-container** (nothing does; the in-process `WebApplicationFactory` probes are the guard's
   real coverage). Still 3/3 green — `ComposeStackFixture`'s 2-minute timeouts all start after
   `up -d --build` returns, and `seq` is a *defined* service so `--remove-orphans` does not cull it. And
   `make test` / `make gate` are unaffected: they run in-process on the host and never read compose. Do not
   conflate the two.
3. **Code-review findings 2–7.** Finding 2's deadline has now passed harmlessly: Phase 4 sets
   `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` in compose, which is where the constraint belongs. It was
   **not** "fixed" by adding the variable to the guard — the exporter's gRPC default is correct against a
   real OTLP collector and wrong only against Seq, so gating on it would disable telemetry in a valid
   deployment. Findings 3–7 remain open and unfixed: the guard reading ambient env, `AlwaysOn` at the public
   edge, long-lived SignalR WebSocket spans, and shutdown flush stalling redeploys. All four are deliberate
   consequences of Phase 0 decisions, documented rather than fixed.
4. ~~**Phase 5** — pay off PR #123's doc debt.~~ **DONE (docs only).** `plans/error-detail-leak-fix.md` gained
   a status banner stating the `00-4bf92f…-01` example is now real and produced (tied to issue #124 landing;
   measured `00-7d7f1929d8de9ca4fa5fe0c4eeb29412-874052e02b8dd01f-01`), and its hub section now records the id
   as a **real W3C id, not `ConnectionId`** — spike S1 settled that. The `ConnectionId` fallback is explicitly
   **not** called dead: it survives at `DomainExceptionHubFilter.cs:63` and `DomainExceptionHubFilterTests.cs:73`
   still asserts it (that test constructs the filter directly with `Activity.Current = null`, never booting the
   factory). `plans/otel-wiring.md`'s Phase 5 section is marked IMPLEMENTED and PR #125's body updated. No
   `.cs`/`.csproj`/`Directory.Packages.props`/`appsettings.json`/compose touched; PR #123 is merged and its
   once-aspirational body was left as-is (now accurate in substance). **Phase 5 was the last phase — the plan
   is complete.**

---

## Verification actually performed this session

Do not re-derive; re-run only if you change the wiring.

- `dotnet build api-gateway/src/ApiGateway.csproj` — clean.
- `AuthPath.EndToEndTests` — **3/3 pass**, real counts confirmed in output (not the seccomp false-green below).
  This run had `OTEL_*` unset, so it also exercises the guard in the real container.
- **Forwarder question, answered from Seq rather than the handler chain.** A throwaway compose override plus a
  throwaway Seq; one `curl` through the gateway carrying **no** `traceparent` produced a single trace of **5
  spans**: gateway ASP.NET Core `Server`, gateway `System.Net.Http` `Client` → mission-design, mission-design
  `Server`, and two `Client` spans for the JwtBearer backchannel's Keycloak metadata + JWKS fetch (expected,
  not a leak). Mission-design's `GetDifficultyCatalogQuery` log record sits under the same trace id — the
  acceptance test Phase 3 asked for. Only `Microsoft.AspNetCore` and `System.Net.Http` produced spans; the
  `Yarp.ReverseProxy.Forwarder.HttpForwarder` entries are **ILogger records, not spans**.
- **Unset-guard proven both ways in the gateway's real container** (it has no `WebApplicationFactory` to probe):
  with `OTEL_*` set, the spans above; recreated with the endpoint unset (`0` OTEL vars confirmed via
  `docker exec … env`), the identical request produced **zero** gateway events while mission-design still
  recorded its own root trace. The one stray `api-gateway` event was the *previous* container's
  `Application is shutting down...` flush, timestamped 0.37 s before the new container started — check
  timestamps before calling that a leak.
- **Package graph checked both ways**, with and without the explicit reference, on both the gateway and
  session-operations. See "Two recorded facts" above.

Throwaway override and Seq container both deleted. A `seq-s3` container from an **earlier** session is still
present and exited; it was not created this session and was deliberately left alone.

---

## Gotchas that cost time

Most are already in the Phase 0/1 handoff — these are the ones that bit again or are new:

- **The bwrap sandbox fails on some Bash calls** with `apply-seccomp: write /proc/self/setgroups … Permission
  denied`, `echo` included. It hit twice this session on `python3` and `grep` calls that had worked moments
  before. Re-run with the sandbox disabled. A backgrounded `make` can exit **0 having run zero tests** with that
  line as its only output — never read a green exit code from a sandboxed `make` as a pass.
- **OTLP export failures are silent.** A passing `curl` proves the HTTP request succeeded, not that telemetry
  left the process. Assert by reading events back out of Seq's API
  (`GET http://localhost:8341/api/events?count=300`). For real errors, drop
  `{"LogDirectory":".","FileSize":32768,"LogLevel":"Warning"}` into `OTEL_DIAGNOSTICS.json` in the process
  working directory.
- **`docker compose down -v` is refused by the permission classifier** (it destroys the postgres/keycloak
  volumes). Not needed: `up -d --build` recreates exited containers in place. `AuthPath.EndToEndTests`'
  `ComposeStackFixture` runs its own `down -v` internally, which is fine.
- **Never a bare `docker compose up`.** `docker-compose.override.yml` exists and auto-loads; it bind-mounts
  `src/` and runs `dotnet watch` as root, leaving foreign-owned `bin/obj` that the Makefile pre-flight rejects
  (recovery needs `make clean-artifacts` + sudo). Explicit `-f` flags suppress it — which is exactly why the
  E2E fixture passes them.
- **`gh pr edit` / `gh issue view` fail in this repo** (Projects-classic GraphQL deprecation). Use
  `gh api repos/{owner}/{repo}/pulls/125 -X PATCH --input <json>`.
- **When separating spans from logs in Seq's API output**, spans carry `Elapsed`; log records do not. Seq labels
  plain log records `SpanKind: Internal`, which will mislead you. And `MessageTemplateTokens` mixes `Text` and
  `PropertyName` tokens — joining only the `Text` ones makes properties *look* missing when they are present.
- **`service.name` is not in `Properties`.** It lives in `Resource`, and Seq nests dotted keys, so it reads as
  `Resource[Name=service].Value.name` — not a flat `service.name`. Grepping `Properties` for it finds nothing
  and makes a working export look broken. Cost time in the Phase 4 session.
- **`ProblemDetailsExceptionHandler` passes the full `Activity.Current.Id` (`00-…-01`) as its `{TraceId}`
  property, but that name collides with Seq's reserved `TraceId` field**, so Seq renders the bare 32-hex trace
  id instead. Same identity, shadowed rendering. Not a bug; do not "fix" it.

---

## Constraints to honour (do not regress)

- `ProblemDetailsExceptionHandler.cs` and `DomainExceptionHubFilter.cs` stay **untouched**. That is the point of
  the issue: they already read `Activity.Current?.Id`.
- No Serilog. No `ILogger<T>` call site moves.
- No `RequestTransform`, no `DistributedContextPropagator` code — S2 is resolved, YARP already propagates.
- No `OTEL` entries in any `appsettings.json`. Config arrives as `OTEL_*` env vars (Phase 4).
- The branch stays at **exactly one commit**: `git commit --amend` then `git push --force-with-lease`. Never the
  GitHub squash button. No `Co-Authored-By` trailer. Stage paths explicitly; never `git add .`.
- **This file, the Phase 0/1 handoff, `docs/workflow_refactor.md`, `docs/*-handoff-2026-07-08.md` and
  `docs/tree-*.md` must stay unstaged.**

---

## Suggested skills

- `/security-review` — the outstanding item; see the query-string/JWT angle above.
- `/conventional-commits` — required by `AGENTS.md` before `git commit`.
- `/safe-pr-creator` — required by `AGENTS.md` before `gh pr create`.

Note the two skills `backend/AGENTS.md` mandates live in `backend/.agents/skills/`, **not**
`backend/.claude/skills/`, so they are not loadable via the Skill tool. Read the `SKILL.md` files directly and
apply them by hand.
