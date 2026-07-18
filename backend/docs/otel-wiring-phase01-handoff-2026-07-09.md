# Handoff — OpenTelemetry wiring (issue #124), Phase 0 + Phase 1

Date: 2026-07-09
Branch: `feat/otel-wiring-session-ops` (off `develop`). Phases 1, 2 and 3 plus the S2 findings are
**committed as a single commit, open as PR #125** against `develop` — unmerged. Do not redo items 1–3.

> **Stale SHA warning.** Each phase was amended into the one commit and force-pushed, so the branch SHA
> has changed three times: `732a834` → `21c186e` → `fda18ce` → current. Every superseded SHA still
> resolves as a dangling object and returns stale content — `git show 732a834` returns the **pre-S2**
> plan doc. Never trust a SHA written down here; read `git rev-parse HEAD` instead.

Canonical spec: [`backend/plans/otel-wiring.md`](../plans/otel-wiring.md) — untracked on `develop`,
lives on this branch, and has already been updated in place with every Phase 0 finding.
Issue: `gh api repos/{owner}/{repo}/issues/124` (plain `gh issue view 124` fails — see Gotchas).

Read the plan doc first. This handoff records only what the plan doc does *not*: the state of the
working tree, what was verified and how, and what is still open.

---

## Status

| Item | State |
|---|---|
| S1 — `Activity.Current` inside the hub filter | **Resolved: yes, W3C id.** No `AddSource` needed |
| S3 — Seq OTLP endpoint / pinned tag | **Resolved: `datalust/seq:2025.2`** |
| S2 — does YARP inject a gateway-originated `traceparent`? | **Resolved: yes.** Re-spiked with `curl`. No `RequestTransform` needed — the plan's proposed fix is dropped. Phase 3 unblocked |
| Phase 1 — session-operations-service | **Implemented, all three gates green** |
| Phase 2 — mission-design + identity-access | **Implemented, all six gates green.** Unset-guard proven both ways in each; `OpenTelemetry.Api` 1.16.0 confirmed non-vulnerable in both. `structure.md` needed no edit (its entry is in the generic `<service-name>` template tree) |
| Phase 3 — api-gateway | **Implemented.** A genuine variant, not a port: **no Npgsql, no `.AddNpgsql()`, and no explicit `OpenTelemetry.Api` pin** (nothing drags it below 1.16.0 once `Npgsql.OpenTelemetry` is absent — verified, `--vulnerable` reports nothing at all). `structure.md` **did** need an edit. Forwarder question resolved: `AddHttpClientInstrumentation()` records YARP's hop as a `System.Net.Http` `Client` span; **no `.AddSource()`, no duplicates**. Build + `AuthPath.EndToEndTests` 3/3 green; unset-guard proven both ways in the real container |
| Commit / squash / PR | **Done: PR #125, unmerged, still exactly one commit.** Phases 2 and 3 were each amended into it and force-pushed (`--force-with-lease`) |
| `/code-review` + `/security-review` on the PR #125 diff | **Still not run.** Now more relevant again: the diff turns on inbound `traceparent` parsing across four components, the outermost being the internet-facing edge |
| Phases 4–5 | Not started. Phase 4 (Seq in compose) is now the only thing standing between this wiring and a destination |

Phase 0's two resolved spikes and the corrections they forced are written up in the plan doc
(Phase 0 section, plus corrected Phase 1 and Phase 4 blocks). Do not re-derive them.

---

## What is in the commit `21c186e` (was: uncommitted working tree)

These six paths are now committed and pushed. Listed for reference — nothing here is left to stage:

- `services/session-operations-service/src/Api/ObservabilityExtensions.cs` (new, `[ExcludeFromCodeCoverage]`)
- `services/session-operations-service/src/Api/DependencyInjection.cs` (one line: `builder.AddObservability();`)
- `services/session-operations-service/src/Api/Api.csproj` (6 version-less `PackageReference`s)
- `services/session-operations-service/src/Directory.Packages.props` (the `PackageVersion` pins)
- `structure.md` (adds `ObservabilityExtensions.cs` to the Api tree)
- `plans/otel-wiring.md` (tracked on this branch; now also carries the resolved Phase 0 / S2 write-up)

**Stayed out of the commit, and still sit unstaged in the working tree:** `docs/workflow_refactor.md`
(modified), `docs/*-handoff-2026-07-08.md`, `../docs/tree-*.md`, and this file. Keep them out of any
follow-up commit too. Never `git add .`; stage paths explicitly.

---

## Constraints honoured (do not regress these)

- `ProblemDetailsExceptionHandler.cs` and `DomainExceptionHubFilter.cs` are **untouched**. That is the
  whole point of the issue: they already read `Activity.Current?.Id`.
- No Serilog anywhere. Logs leave via `builder.Logging.AddOpenTelemetry`; no `ILogger<T>` call site moved.
- `Npgsql.OpenTelemetry` → `.AddNpgsql()`. `OpenTelemetry.Instrumentation.EntityFrameworkCore` (prerelease) not used.
- If `OTEL_EXPORTER_OTLP_ENDPOINT` is unset, `AddObservability` returns before registering anything.
- Phase 1 touched session-operations-service only. Gateway and the other two services are untouched.

---

## Verification actually performed (not assumed)

All three checks below were **re-run against the Phase 1 code as committed** (then `732a834`, now
`21c186e` after the item-2 squash — the squash touched only `plans/otel-wiring.md`, no source file, so
the results still stand). Their results are recorded in the PR #125 body. Re-run before committing if
you change anything.

- Historical gates: `make -C backend test|gate|structure-guard SVC=session-operations-service` →
  **509 tests pass and structure-guard OK.** Rerun the current branch-coverage gate before relying on
  this handoff.
- **The unset-guard, proven both ways.** A throwaway xUnit probe (since deleted) booted the real factory
  twice and asserted `Services.GetService<TracerProvider>()` is `null` when `OTEL_EXPORTER_OTLP_ENDPOINT`
  is unset and non-`null` when set. Both directions matter: a probe asserting only the null case passes
  even if `AddObservability` is dead code. Do not "verify" this by checking for absent exporter error
  output — OTLP export failures are **silent** (see Gotchas).
- **No startup delay.** Baseline on pristine `develop`: 509 tests, 118.5s wall. After: 72.6s wall. No regression.
- **Real service → live Seq** (`datalust/seq:2025.2`, OTLP over `http/protobuf`). A throwaway probe booted
  `SessionOperationsApiWebApplicationFactory` with OTLP pointed at a real Seq container and issued one
  authenticated `GET /api/sessions`. **Assert by reading the events back out of Seq's own API
  (`GET /api/events?count=300`), not by the probe's green tick** — a passing test only proves the HTTP
  request succeeded, not that any telemetry left the process. 52 events ingested; under the request's
  single trace id sat 13 spans: 1 ASP.NET Core `Server` span (`GET api/sessions`), 1 `Client` span
  carrying `db.namespace=postgres` and `db.query.text` (so `AddNpgsql()` works), and 11 `ILogger`
  records — all sharing one `TraceId`. `OTEL_SERVICE_NAME` reached both the tracer and logger providers
  (visible as `Resource.service.name`) with no `ConfigureResource` call, which is why the code hardcodes
  no service name.
  - Expect **9 further `Client` db spans, each its own root trace.** Those are the migration and
    `ResetDatabaseAsync` queries, which run outside any HTTP request and so have no parent activity.
    Correct behaviour, not a propagation leak. Do not go hunting for a bug here.
- **Vulnerability pin verified**, not assumed: `dotnet list services/session-operations-service/src/Api/Api.csproj
  package --vulnerable --include-transitive` resolves `OpenTelemetry.Api` to 1.16.0 and shows it absent
  from the vulnerable list. The only entry is the pre-existing `Microsoft.OpenApi` one (see Gotchas).
- `DomainExceptionHubFilterTests.cs:73` (the `ConnectionId` fallback assertion) is **not at risk** —
  it constructs the filter directly and sets `Activity.Current = null` itself; it never boots the
  factory. The issue body's stated risk was wrong. Recorded in the plan doc.

All throwaway probes were deleted and the Seq container removed. `find services -name 'ZzTemp*'` should
return nothing, and `docker ps -a --filter name=zztemp-seq` should be empty.

---

## What is left

### 1. ~~Commit, squash, PR~~ — DONE

Commit `21c186e`, PR #125 against `develop`, unmerged. Six paths staged explicitly, no `Co-Authored-By`
trailer. At the time it was first pushed the branch was one commit ahead and zero behind `develop`, so
the rebase was a no-op; item 2 later squashed its plan-doc edit into that commit and force-pushed with
`--force-with-lease`, which is what turned `732a834` into `21c186e`. **Nothing to do here.**

Still outstanding on that PR: `/code-review` and `/security-review` on the diff. Neither was run. The
security one is the more relevant — this diff is what switches on inbound `traceparent` parsing at the edge.

Note: the two skills `backend/AGENTS.md` mandates live in `backend/.agents/skills/`, **not**
`backend/.claude/skills/`, so they are not loadable via the Skill tool. Read the `SKILL.md` files
directly and apply them by hand.

### 2. ~~S2~~ — DONE. **Answer: yes, YARP propagates. Verdict is conclusive.**

Re-spiked as prescribed (throwaway net10.0 app, `Yarp.ReverseProxy` 2.3.0 + OTel 1.16.0, both cases
driven with `curl`). No repo changes; spike deleted, ports 5080/5081 free. Full write-up with the
mechanism is in the plan doc's Phase 0 / S2 block — **read that, not this summary**.

The headline: **the plan's hypothesis was wrong and no `RequestTransform` is needed.** Case A (no
inbound header) reached the backend carrying the gateway's own trace id; Case B preserved the client's
trace id across the hop. A control `curl` straight to the backend saw no `traceparent`, which is what
retires the old confound.

Why the hypothesis failed: it assumed YARP's self-constructed `SocketsHttpHandler` has no
`DiagnosticsHandler`. It has one. Two behaviours were being conflated — YARP forwards inbound headers
verbatim, and `DiagnosticsHandler` separately overwrites `traceparent` from `Activity.Current` at
**send time, after `RequestTransform`s run**. Both were isolated by re-running with
`DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION=0`, which made Case A's header vanish entirely and
left Case B's byte-for-byte intact.

Two findings that outlived the question itself, both recorded in the plan doc:

- Injection **does not depend on OpenTelemetry** — it still happened with OTel unregistered. OTel only
  decides the sampled flag (`-00` unregistered vs `-01` under `AlwaysOnSampler`).
- Therefore **do not replace `AlwaysOnSampler` with the OTel default `ParentBased`** while the gateway
  is un-instrumented: today's un-instrumented gateway forwards `-00`, which `ParentBased` would treat as
  "don't record", silently dropping every downstream span.

Phase 3 is now a packages + `AddObservability()` change only.

### 3. ~~Phase 2~~ — DONE

Both services wired and amended into `21c186e`. Details in the plan doc's Phase 2 block. Two things
worth carrying forward:

- The plan's "port verbatim, minus the SignalR source" was **vacuous** — `AddAspNetCoreInstrumentation()`
  registers that source itself (S1), so there was never an `AddSource` line to remove.
- `backend/structure.md` needed **no edit**. Its `ObservabilityExtensions.cs` line lives in the generic
  `<service-name>/` template tree, so it already covered all three services; and `structure-guard.sh`
  reads only `services/*/src/Application`, never `structure.md`. The Phase-1 handoff's implication that
  structure-guard would fail without it was wrong.

### 4. ~~Phase 3~~ — DONE

Gateway wired and amended into the same commit. Details in the plan doc's Phase 3 block. Two things the
plan asserted and this phase disproved, both corrected in place:

- **Not "the same four packages."** No Npgsql anywhere in the gateway, so `Npgsql.OpenTelemetry`,
  `using Npgsql;` and `.AddNpgsql()` are all absent. And the explicit `OpenTelemetry.Api` pin the three
  services carry is **redundant here** — with `Npgsql.OpenTelemetry` gone, the only edge into it is
  `ProviderBuilderExtensions 1.16.0 → >= 1.16.0`. It was not added.
- **`structure.md` did need an edit**, unlike Phase 2 — the `api-gateway/src/` block lists files
  individually. `structure-guard.sh` reads only `services/*/src/Application` and would not have caught it.

The one real unknown is answered: `AddHttpClientInstrumentation()` **does** record YARP's forwarded
request (as a `System.Net.Http` `Client` span). YARP's `Yarp.ReverseProxy.Forwarder.HttpForwarder`
entries in Seq are `ILogger` records, **not** spans, so no `.AddSource()` is needed and nothing is
duplicated. Confirmed by reading events back out of Seq, not by reading the handler chain.

### 5. Phases 4–5

Per the plan doc. Phase 4 (Seq in compose) is straightforward now that S1/S3 are resolved and all four
components export; the plan's Phase 4 compose block has been corrected in place. Nothing has a
destination until it lands.

---

## Gotchas that cost time

- **OTLP failures are silent.** A misconfigured exporter logs nothing and telemetry just never arrives.
  To see the real error, drop `{"LogDirectory":".","FileSize":32768,"LogLevel":"Warning"}` into an
  `OTEL_DIAGNOSTICS.json` in the process **working directory** (for `dotnet run`, the project dir — not `bin/`).
  Never read "no error output" as "it worked".
- **A sandboxed `make` can exit 0 having run nothing.** The bwrap sandbox failed mid-session with
  `apply-seccomp: write /proc/self/setgroups (nested userns is capability-restricted...): Permission denied`
  on *every* Bash call, `echo hello` included — while `kernel.apparmor_restrict_unprivileged_userns` was
  correctly `0`, so the `AGENTS.md` fix was not the cause. The trap: a backgrounded `make test` **reported
  exit code 0 with its output file containing only that seccomp line, having run zero tests.** Never read a
  green exit code from a sandboxed `make` as a pass — open the output and confirm the test counts are there.
  Recovery: re-run with the sandbox disabled. Same class of trap as silent OTLP failures, one line up.
- **Seq is HTTP-only.** The .NET exporter defaults to gRPC, which dies with `HTTP_1_1_REQUIRED`. Phase 4
  must set `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`. Working dev invocation used for the E2E check:
  `docker run -d --name zztemp-seq -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_NOAUTHENTICATION=true -p 8341:80 -p 5341:5341 datalust/seq:2025.2`
  → ingest at `http://localhost:5341/ingest/otlp`, query at `http://localhost:8341/api/events?count=300`.
- **Seq 2025.2 will not boot on `ACCEPT_EULA=Y` alone** — it needs `SEQ_FIRSTRUN_NOAUTHENTICATION=true`
  (dev) or an admin password.
- **`docker compose up` poisons the build.** The override bind-mounts `src/` and runs `dotnet watch` as
  root, leaving foreign-owned `bin/obj` that the Makefile pre-flight then rejects (recovery needs
  `make clean-artifacts` + sudo). Use `docker compose -f docker-compose.yml up -d --build` — the
  production-like form, no override, no bind mounts. That form brought the full stack up healthy.
- **`gh issue view` / `gh pr edit` fail in this repo** (Projects-classic GraphQL deprecation). Use
  `gh api repos/{owner}/{repo}/issues/124` and REST `PATCH` instead.
- The plan claimed no central package management exists. **It does** — one `src/Directory.Packages.props`
  per service. Versions go there; `.csproj` carries version-less `PackageReference`.
- `OpenTelemetry.Api` <1.15.3 is vulnerable (GHSA-g94r-2vxg-569j, unbounded allocation parsing inbound
  `traceparent`/`baggage` — exactly the path this work switches on). `Npgsql.OpenTelemetry` drags in
  1.14.0, hence the explicit `OpenTelemetry.Api` 1.16.0 pin. Verify with
  `dotnet list <Api.csproj> package --vulnerable --include-transitive`.
- Unrelated and **pre-existing on `develop`**: `Microsoft.OpenApi` 2.0.0 carries a *high* advisory
  transitively via `Microsoft.AspNetCore.OpenApi`. Not introduced here; deserves its own issue.

---

## Environment notes

At the time of writing, the compose stack and the Seq container used for verification are **down**, and
the scratchpad holding the S1/S2 spikes is gone (session-scoped). No repo state was lost and
`bin/obj` is not foreign-owned, so `make` runs clean without `clean-artifacts`.

The bwrap sandbox was failing at the end of the session (see Gotchas); every `make`/`dotnet`/`docker`
command quoted above was run with the sandbox disabled. If Bash calls start failing with `apply-seccomp`,
that is the sandbox, not your command — and check any "passing" background job's actual output.

---

## Suggested skills

- `/conventional-commits` — required by `AGENTS.md` before `git commit`.
- `/safe-pr-creator` — required by `AGENTS.md` before `gh pr create`.
- `/code-review` — on the diff before opening the PR.
- `/security-review` — the diff turns on inbound `traceparent` parsing at the edge; cheap to run.
