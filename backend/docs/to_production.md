# Deploying the Umbral backend to Railway — findings & fixes

**Date:** 2026-07-16 · **Scope:** `backend/` (api-gateway + 4 services + infra)

The backend is currently wired for local `docker compose` only. It is a 5-service stack —
`api-gateway` (YARP reverse proxy, public entry) plus `identity-access-service`,
`mission-design-service`, `session-operations-service`, `scoring-monitoring-service` — backed by
Postgres, Keycloak, RabbitMQ, and Seq. Several things **will not boot or will silently break auth**
on Railway unless changed. Findings are ordered worst-first.

> Legend: 🔴 blocker (deploy fails or auth breaks) · 🟠 required config/provisioning ·
> "repo" = code/compose change I can make · "dashboard" = Railway-side action.

---

## 🔴 1. Port binding is hardcoded to 8080  — ✅ DONE (repo)

**Finding.** Railway injects a dynamic `$PORT` and expects the app to listen on it. Every service
hardcodes `8080` — compose sets `ASPNETCORE_URLS: http://+:8080`, Dockerfiles `EXPOSE 8080`. On
Railway the container will bind the wrong port and the healthcheck/router never connects.

**Fix (applied — all 5 Dockerfiles).** The entrypoint now derives the bind URL, keeping `+` so both
IPv4 and IPv6 are covered:

```dockerfile
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=\"${ASPNETCORE_URLS:-http://+:${PORT:-8080}}\"; exec dotnet <Assembly>.dll"]
```

- **Local/compose/tests:** compose sets `ASPNETCORE_URLS=http://+:8080`, so the `:-` default is never
  taken → binds 8080 exactly as before. **No behaviour change.**
- **Railway:** `ASPNETCORE_URLS` unset → falls to `http://+:${PORT}` (or 8080 if `$PORT` absent).

Do **not** switch to `http://0.0.0.0:...` alone — Railway private networking is IPv6-only (see #2),
and `0.0.0.0` binds IPv4 only, breaking service-to-service calls. `+` binds both.

---

## 🔴 2. Inter-service addresses use compose DNS

**Finding.** Services reach each other by compose service name on port 8080, e.g.
`ReverseProxy__Clusters__mission-design__Destinations__d1__Address=http://mission-design-service:8080/`,
`IdentityAccess__BaseAddress`, `PublishedTriviaQuizSource/MissionReadinessSource/MissionRuntimeSource__BaseAddress`,
`RabbitMq__HostName=rabbitmq`. None of these hostnames resolve on Railway.

**Fix (dashboard env).** Use Railway's private DNS (`<service>.railway.internal`), which is
**IPv6-only** — keep the app bound to `+` (#1). Internal calls target the app's own port (8080),
not `$PORT`:

```
http://mission-design-service.railway.internal:8080/
http://identity-access-service.railway.internal:8080/
RabbitMq__HostName=rabbitmq.railway.internal
```

Only `api-gateway` needs a **public** domain; the other four services stay private-only.

---

## 🔴 3. One Railway service per Dockerfile

**Finding.** `docker compose` builds five images from five contexts. Railway has no compose;
each app is a separate service.

**Fix (dashboard).** Create 5 Railway services, each with **Root Directory** set to its folder
(`api-gateway`, `services/identity-access-service`, `services/mission-design-service`,
`services/session-operations-service`, `services/scoring-monitoring-service`) using the Dockerfile
in that directory. Public domain on `api-gateway` only.

---

## 🔴 4. JWT issuers are hardcoded to dev URLs (auth returns 401)  — ✅ DONE (repo)

> **Implemented** in `api-gateway/src/DependencyInjection.cs`: `ValidIssuers` now binds from
> `Keycloak:ValidIssuers`, falling back to the two dev issuers when unset, and always folds in the
> configured `Keycloak:Authority` (a realm's issuer *is* its realm URL) via `.Append(...).Distinct()`.
> Local/compose/tests are unchanged (no key set → dev fallback, Authority already `localhost:8080`).
> **Production:** just set `Keycloak__Authority=https://<keycloak-domain>/realms/umbral` and the
> matching issuer is accepted automatically — no separate issuer list needed. Original finding:


**Finding.** `api-gateway/src/DependencyInjection.cs:63-67` only accepts:

```csharp
ValidIssuers = new[] {
    "http://localhost:8080/realms/umbral",
    "http://keycloak:8080/realms/umbral"
};
```

A token minted by a production Keycloak has `iss = https://<keycloak-domain>/realms/umbral`, which
is not in the list — **every authenticated request 401s**. Related: `Keycloak__Authority` /
`Keycloak__Audience` and `KeycloakOptions.AdminAuthority`
(`services/identity-access-service/.../Keycloak/KeycloakOptions.cs`, defaults to
`http://keycloak:8080`) also point at dev.

**Fix (repo + dashboard).**
- Make `ValidIssuers` config-driven (bind from `Keycloak:ValidIssuers` or reuse `Keycloak:Authority`)
  and add the public issuer.
- Set `Keycloak__Authority=https://<keycloak-domain>/realms/umbral` (public HTTPS URL) so the OIDC
  metadata address, `RequireHttpsMetadata` (already `!IsDevelopment()`), and issuer all agree.
- Set `Keycloak__AdminAuthority` to the Keycloak URL (internal or public) instead of the dev default.

---

## 🔴 5. Keycloak realm client redirect URIs / web origins are dev localhost

**Finding.** The imported realm's clients (`umbral-web`, `umbral-mobile`) carry dev localhost
redirect URIs / web origins. In production, logins bounce with an invalid-redirect error and CORS
fails.

**Fix (dashboard/admin).** Add the production frontend/mobile URLs to each client's redirect URIs
and web origins (admin console or realm JSON before first import).

---

## 🟠 6. Managed infrastructure replaces the compose infra

**Finding.** `postgres` (with **4 databases**: `mission_design`, `identity_access`,
`session_operations`, `scoring_monitoring`), `rabbitmq`, `keycloak`, and `seq` are all compose
images. Railway provides none of them implicitly.

**Fix (dashboard).**

| Compose service | Railway action |
|---|---|
| `postgres` (4 DBs) | Provision Railway Postgres; create the 4 databases (or 4 Postgres services). Update all 4 `ConnectionStrings__umbral_backendDb` to the Railway host **with SSL**. |
| `rabbitmq` | Deploy a RabbitMQ service; update `RabbitMq__HostName/Port/UserName/Password`. |
| `keycloak` | **`start-dev` + H2 will not fly** — see #7. |
| `seq` | Optional. Drop it, or point `OTEL_EXPORTER_OTLP_ENDPOINT` at a hosted OTLP collector. Services still boot without it. |

Migrations are handled: each service calls `Migrate()` on startup (`Api/Program.cs`), so schemas
self-apply against Railway Postgres on first boot. Watch first-boot ordering.

---

## 🟠 7. Keycloak must run in production mode, not `start-dev`

**Finding.** `docker-compose.yml:22-24` runs `start-dev --import-realm` with an **ephemeral
in-container H2 database** and plaintext `admin/admin`. `start-dev` disables HTTPS enforcement and
hostname strictness — unsuitable for production, and the H2 store is lost on every redeploy.

**Fix (dashboard).** Deploy Keycloak as its own Railway service in production mode:

```
command: start
KC_DB=postgres
KC_DB_URL / KC_DB_USERNAME / KC_DB_PASSWORD   # persistent Railway Postgres
KC_HOSTNAME=https://<keycloak-domain>
KC_PROXY_HEADERS=xforwarded                    # Railway edge terminates TLS
KC_HTTP_ENABLED=true
KC_HEALTH_ENABLED=true
KEYCLOAK_ADMIN / KEYCLOAK_ADMIN_PASSWORD       # real secrets, not admin/admin
```

**Realm-lifetime lesson (carried from the refresh-token work):** with Keycloak now on a *persistent*
DB, `--import-realm` seeds `umbral-realm.json` **once**. It uses `IMPORT_STRATEGY=IGNORE_EXISTING`,
so later edits to `umbral-realm.json` (e.g. `ssoSessionIdleTimeout`) are **skipped** on redeploy —
exactly the stale-realm bug seen locally. Apply subsequent realm changes via the **admin REST API**
(`PUT /admin/realms/umbral`) or a deliberate realm delete + re-import, never a plain restart.

Verify session lifetimes live after any change:

```bash
curl -s -X POST https://<keycloak-domain>/realms/umbral/protocol/openid-connect/token \
  -d client_id=umbral-mobile -d grant_type=password -d username=<u> -d password=<p> \
  | grep -o '"refresh_expires_in":[0-9]*'   # expect 28800 (8h)
```

---

## 🟠 8. Environment + secrets hygiene

**Finding.** Every service sets `ASPNETCORE_ENVIRONMENT=Development`, and secrets use dev defaults:
DB `postgres/postgres`, RabbitMQ `guest/guest`, `KEYCLOAK_BACKEND_CLIENT_SECRET` falls back to
`umbral-backend-dev-secret`, Keycloak `admin/admin`.

**Fix (dashboard).**
- `ASPNETCORE_ENVIRONMENT=Production` on all five services. (This also flips
  `RequireHttpsMetadata` to `true` and disables the dev-only localhost backchannel rewrite.)
- Replace every dev default with a Railway secret: the 4 DB connection strings,
  `KEYCLOAK_BACKEND_CLIENT_SECRET`, RabbitMQ credentials, Keycloak admin credentials.

---

## Repo changes I can make now (vs. dashboard-only)

| # | Change | Type |
|---|--------|------|
| 1 | Parameterize `ASPNETCORE_URLS` to `$PORT` | repo (Dockerfile/compose) + dashboard |
| 2 | Internal `.railway.internal` addresses | dashboard env |
| 3 | 5 services, root dirs, public domain on gateway | dashboard |
| 4 | Config-driven `ValidIssuers` / authority | **repo** |
| 5 | Realm client redirect URIs / web origins | realm JSON / admin |
| 6 | Provision Postgres/RabbitMQ + connection strings | dashboard |
| 7 | Keycloak production mode | dashboard |
| 8 | `Production` env + real secrets | dashboard |

The repo-side items (#1 port param, #4 configurable issuers/authority, plus a Railway env template)
can be implemented in code; the rest are Railway dashboard / Keycloak-admin actions.
