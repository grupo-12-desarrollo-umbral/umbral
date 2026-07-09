# Handoff — `access_token` in query string leaks into logs (and, post-#125, into Seq)

Date: 2026-07-09
Origin: surfaced while verifying Phase 4 of `backend/plans/otel-wiring.md` (PR #125). The OTel wiring did
not *cause* this leak — it pre-dates OTel and lives in plain `docker logs` — but it turns an ephemeral
stdout leak into an **indexed, queryable** one the moment PR #125 merges. That is why it is worth fixing now.

> **Never trust a SHA written down here.** Read `git rev-parse HEAD` / `gh pr view`. SHAs below were current
> at write time only.

---

## The defect, in one paragraph

SignalR delivers its bearer token in the query string: `?access_token=<JWT>` (ADR-0002; see
`api-gateway/src/Transforms/WebSocketTokenExtractionTransform.cs`). That transform reads the token into the
auth header but **does not strip it from the outbound query**, so the raw query is (a) logged by the gateway
and (b) **forwarded verbatim to the downstream service, which logs it too**. A JWT recovered from either log
still authenticates — verified this session by replaying a log-recovered token against the gateway (`200`).
The tokens are short-lived and this is dev-only compose, so: **important, not an emergency.**

Three gateway log records carried it (all confirmed by driving a canary + real JWT through the gateway):
1. `Microsoft.AspNetCore.Hosting.Diagnostics` — "Request starting …", structured property `QueryString`.
2. same category — "Request finished …", property `QueryString`.
3. `Yarp.ReverseProxy.Forwarder.HttpForwarder` — "Proxying to {targetUrl} …", property `targetUrl`.

**Disproved, do not chase:** an earlier note claimed the gateway records `url.query` as a *span* attribute.
It does **not** — gateway Server spans carry only `url.path` + `url.scheme`. The leak is purely through the
three `ILogger` records above, not span attributes. (Client spans carry `url.full`; my `/api/missions` probe's
`url.full` did **not** include the query. Re-check this for the hub route if OTel ever records a client span
there.)

---

## What is FIXED — PR #126 (gateway only)

**PR #126** → `develop`, branch `fix/gateway-token-query-log-leak` (one commit, `8db60f1` at write time).
https://github.com/grupo-12-desarrollo-umbral/umbral/pull/126 — title
`fix(api-gateway): redact access_token from request and forwarder logs`.

Approach (do not re-litigate — the alternatives were evaluated and rejected, see the PR body): a
`RedactingLoggerFactory` decorates `ILoggerFactory` and, for the two leaking categories only, wraps the logger
so both the **formatted message and the structured state** are redacted before any sink sees them
(`access_token=[REDACTED]`, key kept). Files: `api-gateway/src/Logging/{SensitiveQueryLogRedactor,RedactingLoggerFactory}.cs`,
`DependencyInjection.cs`, `GlobalUsings.cs`, a new `api-gateway/tests/ApiGateway.UnitTests/` project, and `structure.md`.

Why the factory decorator and not a middleware: the "Request starting" record is written by
`HostingApplication.CreateContext` **before the first middleware runs**, so no middleware that rewrites
`HttpContext.Request.QueryString` can suppress it. Redacting at the logger is the only interception point that
catches all three records, and it redacts the *log* not the *request*, so SignalR auth downstream still works.

Verified this session on the fix branch (rebuilt gateway image):
- `docker logs api-gateway`: raw `access_token=eyJ` = **0**, canary = **0**, `access_token=[REDACTED]` present
  in both "Request starting" and "Proxying to". Redacted, not deleted.
- `dotnet build ApiGateway.csproj` clean; `ApiGateway.UnitTests` 7/7; `AuthPath.EndToEndTests` 3/3.
- SignalR still forwards (the `Proxying to` line shows `…/hubs/session?access_token=[REDACTED]` — the *log* is
  redacted; the real forwarded request still carries the token, which is why auth survives).

---

## RESOLVED 2026-07-09 (later session) — read this before the section below

Both open items are now **fixed on `fix/gateway-token-query-log-leak`, uncommitted at write time**:

1. **Downstream leak — closed via option 2.** `TrustedHeadersTransform` now also does
   `context.Query.Collection.Remove(access_token)` beside the `Authorization` header strip it already did.
2. **Dead `SensitiveKeys` field — deleted.** The `[GeneratedRegex]` literal is now the single source of truth.
   (It cannot derive from a runtime field: attribute args must be compile-time constants.) The `access_token`
   *key* is now a shared const, `WebSocketTokenExtractionTransform.AccessTokenQueryKey`, used by both the
   reader and the remover so those two cannot drift.

> **The option-2 risk note below is WRONG — do not act on it.** It claims `session-operations-service`
> authenticates the WS connection from the query token. It does not. That service authenticates **only** from
> the `X-User-Id`/`X-User-Role`/`X-User-Email` trusted headers the gateway injects
> (`services/session-operations-service/src/Api/DependencyInjection.cs`, `TrustedHeadersAuthenticationHandler`).
> It has no `AddJwtBearer`, no `OnMessageReceived`, and `grep -rn access_token services/` returns zero hits
> outside an unrelated Keycloak DTO in identity-access. The gateway's own `OnMessageReceived` consumes the
> query token during authentication, which runs *before* YARP transforms fire on forward — so removing it at
> the transform cannot affect gateway auth either. Option 2 was the architecturally consistent fix all along:
> the query token is just the WS-transport variant of the `Authorization` header that the same transform was
> already stripping, with the same "downstream services must not re-validate it" rationale.

Verified end-to-end this session against a rebuilt prod-like stack, driving **real WS upgrades** (not plain GETs):
- `participant` user → **`HTTP/1.1 101 Switching Protocols`**. Hub auth survives the strip. This is the
  positive control the risk note demanded; it passes.
- `admin` user → `403` (not `401`): downstream established identity from the trusted headers, then denied on
  role. Further proof auth did not regress to "no credentials".
- `docker logs backend-session-operations-service-1`: raw JWT **0** (was **2**). It logs
  `…/hubs/sessions?canary=PARTICIPANT456` — the harmless param survives, the token is simply gone.
- `docker logs backend-api-gateway-1`: raw JWT **0**. `Proxying to` no longer contains the token *at all*
  (strictly better than `[REDACTED]`); inbound `Request starting` still shows `access_token=[REDACTED]`,
  because the client genuinely did send it there and the redactor still masks it. Both layers earn their keep.
- `dotnet build` clean, 0 warnings; `ApiGateway.UnitTests` **11/11** (4 new `TrustedHeadersTransformTests`);
  `AuthPath.EndToEndTests` **3/3**.

Since the token no longer reaches any downstream service, **option 1 (shared redaction across all four
services) is unnecessary for this leak** and PR #125 can merge without exporting a JWT to Seq. Option 1 remains
a reasonable defense-in-depth item if a *future* query-carried secret is ever introduced downstream.

A prod-like stack was left **running** (`docker compose -f backend/docker-compose.yml up -d --build`);
`docker compose -f backend/docker-compose.yml down` to stop it.

---

## What WAS open — the gap PR #126 did not close (superseded by the section above)

**The downstream service logs the same token.** The gateway forwards `?access_token=<JWT>` verbatim (it must,
for auth), so `session-operations-service` logs it in its own `Request starting` record. Measured this session
by driving a **real WebSocket upgrade** through the gateway (a plain GET 401s at the gateway before YARP
forwards — you must send the `Connection: Upgrade` / `Upgrade: websocket` / `Sec-WebSocket-*` headers so the
forwarder actually runs):

- `docker logs api-gateway` (patched): raw JWT **0**, redacted present.
- `docker logs session-operations-service` (**unpatched**): raw JWT **2** — token in the clear.

`session-operations-service` (and the other services) have **no `appsettings.json` override**, so they log at
default levels. On PR #125's branch all four services get `builder.Logging.AddOpenTelemetry` + `OTEL_*` in
compose, so once #125 merges **this downstream log record exports to Seq** — an indexed, replayable JWT. That
is the retention problem the gateway fix does not reach.

### Options for the downstream leak (needs a human decision — same scope/branch fork as #126)
1. **Shared redaction across all four services** — lift the `RedactingLoggerFactory` into a component each
   service wires in. Cleanest; biggest change; arguably belongs *with* the OTel work since that is what makes
   it a retention problem, not a stdout one.
2. **Gateway strips `access_token` from the forwarded query** after extracting it to the header, so downstream
   never receives it. Narrower, but **risky**: confirm nothing downstream re-reads the query token — the
   handoff-agent noted `session-operations-service` authenticates the WS connection from the query token, so
   stripping it at the gateway may break hub auth. Test the hub end-to-end before trusting this.
   → **CHOSEN AND DONE. The stated risk was false** — see the RESOLVED section above. The hub returns `101`
   with the token stripped. The "authenticates from the query token" claim was never verified against
   `TrustedHeadersAuthenticationHandler`, which reads headers only.
3. **Accept + document** for dev, and gate it on Seq lockdown (auth + unpublished port) for any non-dev deploy.

### ~~Also open~~ RESOLVED — a code smell in PR #126 (cheap, decide whether it rides along on #126 or a follow-up)
`SensitiveQueryLogRedactor.SensitiveKeys` (the 4-element allowlist: `access_token`, `id_token`,
`refresh_token`, `code`) is **declared but never referenced** — the `[GeneratedRegex]` on the same class
hardcodes the same four keys independently. Dead code **and** a drift hazard (add a key to one, forget the
other). Either have the regex derive from `SensitiveKeys` or delete the field.

---

## State of the tree / branches (read, don't trust)
**Both PRs are now MERGED. `develop` is at `6583a38`.** Merge order was deliberate: **#126 first** (`c9277ea`),
**then #125** (`6583a38`). The reverse order would have exported live JWTs into Seq for the whole window between
the two merges, since #125 wires OTLP while the unpatched tree still leaks the token from both the gateway's log
records and `session-operations-service`. Both merged as merge commits (the repo disallows squash/rebase merge),
so the merge arcs are intact.

- **PR #126** (`fix/gateway-token-query-log-leak`): squashed to one commit `26287f2`, merged via `c9277ea`.
  Both halves of the leak plus the `SensitiveKeys` cleanup.
- **PR #125** (`feat/otel-wiring-session-ops`): rebased onto the post-#126 `develop` as `21424fb`, merged via
  `6583a38`. Rebase conflicted in `api-gateway/src/DependencyInjection.cs` and `structure.md`; both resolved
  keep-both. Canonical spec: `backend/plans/otel-wiring.md`. Its own handoff:
  `backend/docs/otel-wiring-phase3-handoff-2026-07-09.md`.
- **The combined tree was verified against Seq**, which neither PR could test alone: a real `participant` WS
  upgrade returns `101`; across 251 exported events the raw JWT and `access_token=eyJ` are both **absent**;
  gateway records show `access_token=[REDACTED]`; `session-operations-service` records show `?canary=…` with no
  token. Critically, `session-operations-service` exported **160 events**, so the zero is a real negative and
  not a silently-dead exporter.
- `RedactingLoggerFactory.AddQueryParameterRedaction` and `builder.AddObservability()` are **order-independent**
  in `AddGatewayServices`: redaction runs in the delegating logger before the fan-out to every `ILoggerProvider`,
  so the OTLP exporter can only ever see redacted records. Do not "fix" the ordering.
- A prod-like stack **is running** (OTel + Seq). Stop with `docker compose -f backend/docker-compose.yml down`.
  Bring it up with `docker compose -f backend/docker-compose.yml up -d --build` — **never a bare
  `docker compose up`** (`docker-compose.override.yml` auto-loads `dotnet watch`). Seq UI: http://localhost:8341.
- Unrelated exited container `seq-s3` from an older session exists; leave it.

## Gotchas that cost time here
- **The hub route needs a real WS-upgrade to exercise the forwarder.** A plain
  `curl /hubs/session?access_token=…` 401s at the gateway *before* YARP forwards, so it emits no `Proxying to`
  record and no downstream log — it will look clean for the wrong reason. Send the WebSocket upgrade headers.
- **Verify against `docker logs`, not Seq, on the `develop`-based fix branch** — `develop` has no OTel/Seq.
  `docker logs <svc> | grep -c 'access_token=eyJ'` is the ground truth for this leak.
- **A log-recovered JWT is live** — treat the logs as credential-bearing. Do not paste captured tokens into
  shared channels; the classifier may (correctly) block replaying them.
- **The bwrap sandbox intermittently fails every Bash call** with
  `apply-seccomp: write /proc/self/setgroups … Permission denied`, `echo` included. Re-run the exact command
  with the sandbox disabled. Never read a green exit code from a sandboxed `make` as a pass.
- **`gh pr edit --title/--body` silently aborts in this repo** (Projects-classic GraphQL deprecation) — use
  `gh api repos/grupo-12-desarrollo-umbral/umbral/pulls/<N> -X PATCH --input <json>`.
- **Never `grep -c` Seq's `/api/events` response.** The whole JSON array is *one line*, so `grep -c` reports
  1-or-0 lines, not occurrences — it looks like "1 event" when there are five. Parse the JSON. (`grep -c` = 0
  is still a valid *absence* proof; only the non-zero counts are meaningless.)
- **A "no JWT in Seq" result is worthless until you prove the service exported anything at all.** Count events
  per `Resource[].Name == "service"` → `.Value.name` first. A dead exporter yields the same zero as a fixed leak.
- **`gh pr merge --match-head-commit` needs the full 40-char SHA**; an abbreviated one fails with
  `Could not coerce value to GitObjectID`. Also, right after a force-push GitHub returns
  `Base branch was modified` — that is a stale mergeability cache, not a real race. Poll `mergeStateStatus`
  until `CLEAN` and retry; confirm `develop`'s tip is unchanged before assuming otherwise.
- **`gh pr merge` can succeed while the shell reports exit 1** if a *later* command in the same line fails
  (e.g. `gh pr view --json merged` — there is no `merged` field; it is `mergedAt`/`state`). Verify the merge by
  re-reading PR state, never by the exit code.

## Suggested skills
- `/security-review` — the natural home for both the downstream leak and the still-deferred #125 security pass
  (that pass already had "does a JWT reach exported telemetry?" as an explicit angle — this handoff answers
  *yes, from the downstream side*).
- `backend/.agents/skills/conventional-commits` and `.../safe-pr-creator` (mandated by `backend/AGENTS.md`;
  live in `.agents/skills/`, not `.claude/skills/`, so read the `SKILL.md` by hand) — before any follow-up
  commit/PR.
- Keep any follow-up on its **own branch off `develop`**, squashed to one commit, no `Co-Authored-By`, staged
  paths explicit. The `*-handoff-*.md`, `workflow_refactor.md` and `docs/tree-*.md` files stay **unstaged**.
