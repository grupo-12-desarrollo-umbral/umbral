# Plan: SignalR Auth Through The Gateway

## Context

The frontend SignalR client connects to the backend hub at `/hubs/sessions`, but it does not
provide a bearer token during negotiate or WebSocket upgrade. The api-gateway requires an
authenticated Keycloak JWT and then injects trusted `X-User-*` headers for downstream services.

The current frontend login flow receives a Keycloak `access_token` during the callback and uses it
only for bootstrap. The app session cookie is a separate signed JWT that stores identity claims, not
the Keycloak token, and it is `httpOnly`, same-site to the Next origin, and not accepted by the
gateway. As a result, browser SignalR cannot authenticate through the gateway.

There is also broader frontend drift from `CONTEXT-MAP.md`: several frontend server-side libraries
call services directly and manually send `X-User-*`. That is a separate REST unification problem.
This plan scopes the first PR to SignalR auth, while documenting the follow-up work needed to fully
restore the gateway boundary.

## Guardrails

- Never store Keycloak tokens in the existing app session JWT. `frontend/app/lib/session.ts` uses
  `SignJWT`, so the cookie value is readable even though it is signed.
- Store Keycloak tokens in either a server-side store keyed by a session id, or a separate
  encrypted/sealed `httpOnly` cookie such as `kc_session`.
- For this plan, use a sealed `kc_session` cookie unless a Redis or database store is introduced.
- The browser may receive only the short-lived Keycloak access token, only in memory, only for the
  SignalR client.
- No new direct-to-service frontend calls are allowed. Browser and BFF traffic to backend APIs must
  target the api-gateway.
- `KC_TOKEN_SECRET` is required in production. In development only, token sealing may fall back to a
  domain-separated derivation from `SESSION_SECRET`.
- Rotating `KC_TOKEN_SECRET` invalidates all existing `kc_session` cookies and forces re-login. This
  is acceptable, but must be documented.
- Next.js version is `16.2.6`; read the local Next docs before implementation:
  `15-route-handlers.mdx`, `cookies.mdx`, `02-guides/authentication.mdx`, and
  `02-guides/backend-for-frontend.mdx`.

---

## PR 1: Authenticated SignalR

**Goal:** make the browser SignalR client authenticate to `/hubs/sessions` through the api-gateway.

**Non-goal:** do not migrate all REST clients in this PR. REST-through-gateway remains a follow-up.

### Phase 0: Recon And Decisions

- Read the relevant Next 16 docs and confirm cookie read/write semantics in route handlers.
- Confirm Keycloak client mode:
  - Public PKCE client: refresh grant sends `client_id` and `refresh_token`.
  - Confidential client: refresh grant also sends `client_secret`.
- Confirm the real `umbral-web` login token has the expected gateway audience. The gateway validates
  `aud`, so a missing Keycloak audience mapper will break SignalR even if the token exists.
- Confirm local origins from config. The example has frontend on `http://localhost:3000` and gateway
  on `http://localhost:8000`, so browser-to-gateway SignalR is cross-origin and requires CORS.
- Decide and document that `kc_session` expiry is bounded by Keycloak refresh expiry.

**Exit criteria:** implementation assumptions are documented before code is written.

#### Phase 0 Findings (2026-06-04)

- Next.js 16.2.6 local docs confirm the implementation model this plan relies on:
  - Route Handlers are public HTTP endpoints and must be treated like API endpoints.
  - `GET` Route Handlers become request-time when they touch request properties or `cookies()`.
  - `cookies()` is async in Next 16, and `.set()` / `.delete()` are only supported in Server Functions or Route Handlers.
  - The auth guide still recommends `httpOnly` cookie storage and explicitly calls out refresh-token-backed session renewal as a valid pattern.
- The current frontend session cookie is a signed JWT, not an encrypted one. `frontend/app/lib/session.ts` uses `SignJWT`, so its claims are integrity-protected but readable by anyone who has the cookie value. Keycloak tokens must therefore stay out of `session`.
- The configured web client is currently a public PKCE client:
  - `frontend/.env.local` and `.env.local.example` use `KEYCLOAK_CLIENT_ID=umbral-web`.
  - `backend/deploy/keycloak/import/umbral-realm.json` defines `umbral-web` with `"publicClient": true` and PKCE S256 enabled.
  - Phase 1 should therefore implement refresh with `client_id` + `refresh_token` by default, and only append `client_secret` when `KEYCLOAK_CLIENT_SECRET` is actually configured for a confidential deployment.
- Gateway audience expectations are already aligned in config:
  - `backend/api-gateway/src/appsettings*.json` validates `Keycloak:Audience = "umbral-web"`.
  - The realm import includes an `oidc-audience-mapper` named `umbral-web-audience` on the `umbral-web` client with `"access.token.claim": "true"`.
  - A live token check against `http://localhost:8080` could not be completed on 2026-06-04 because no local Keycloak instance was reachable. Treat the real-token audience check as a required runtime verification step once the backend stack is up.
- Local origin assumptions are cross-origin, matching the problem statement:
  - Frontend auth code defaults `NEXT_PUBLIC_APP_URL` to `http://localhost:3000`.
  - Frontend server-side API calls use `API_GATEWAY_URL=http://localhost:8000` in `frontend/.env.local.example`.
  - The monorepo context map requires the frontend to communicate through the gateway only.
  - Browser SignalR therefore needs gateway CORS for `http://localhost:3000 -> http://localhost:8000`.
- The current realtime client has an additional config gap to account for in later phases:
  - `frontend/app/lib/realtime/session-state-client.ts` reads `NEXT_PUBLIC_API_GATEWAY_URL`.
  - The checked-in `frontend/.env.local` currently sets `API_GATEWAY_URL` but not `NEXT_PUBLIC_API_GATEWAY_URL`.
  - Without that public env var, the browser falls back to same-origin `/hubs/sessions`, which does not match the intended gateway-origin deployment model. Phase 6 or accompanying setup docs must make this env requirement explicit.
- `kc_session` expiry decision for PR 1: bound the cookie lifetime to Keycloak refresh-token expiry, never longer. If refresh expiry passes or refresh fails, clear `kc_session` and force a fresh login for realtime access.

### Phase 1: Capture Keycloak Tokens And Refresh

Update `frontend/app/lib/keycloak.ts`.

- Extend `exchangeCode` to return:
  - `accessToken`
  - `refreshToken`
  - `expiresIn`
  - `refreshExpiresIn`
  - existing identity fields
- Add `refreshAccessToken(refreshToken)` using Keycloak's refresh-token grant.
- Include `client_secret` only when `KEYCLOAK_CLIENT_SECRET` is configured.
- Add a typed `KeycloakAuthError` or equivalent error type for token exchange and refresh failures.
- Add pure expiry helpers, for example:
  - `toExpiresAtMs(expiresInSeconds, nowMs)`
  - `isExpiredOrNearExpiry(expiresAtMs, skewMs, nowMs)`

**Exit criteria:** token exchange and refresh compile and can be unit-tested without being wired
into login.

### Phase 2: Add Sealed Keycloak Token Cookie

Create `frontend/app/lib/keycloak-tokens.ts`.

- Mark the module `server-only`.
- Use `jose` `EncryptJWT` with direct encryption (`dir`) and `A256GCM`.
- Derive the content-encryption key as SHA-256 of the configured seal secret.
- Production secret rules:
  - If `NODE_ENV === 'production'`, require `KC_TOKEN_SECRET`.
  - Throw fail-closed if `KC_TOKEN_SECRET` is missing in production.
- Development fallback rules:
  - Outside production, allow fallback to a domain-separated derivation from `SESSION_SECRET`.
  - Example source string: `umbral:kc-session:v1:${SESSION_SECRET}`.
- Store these encrypted claims:
  - `accessToken`
  - `refreshToken`
  - `accessExpiresAt`
  - `refreshExpiresAt`
- Set `kc_session` with:
  - `httpOnly: true`
  - `secure: process.env.NODE_ENV === 'production'`
  - `sameSite: 'lax'`
  - `path: '/'`
  - `expires` no later than `refreshExpiresAt`
- Implement:
  - `storeKeycloakTokens(tokens)`
  - `getValidAccessToken()`
  - `clearKeycloakTokens()`
  - `sealKeycloakTokens()` and `unsealKeycloakTokens()` if useful for testability
- `getValidAccessToken()` behavior:
  - If access token is fresh, return it.
  - If access token is expired or near expiry, refresh it.
  - If Keycloak returns a rotated refresh token, persist the rotated token.
  - If refresh fails or refresh expiry has passed, clear `kc_session` and throw an auth error.

**Exit criteria:** sealed token roundtrip works; login can persist a `kc_session`; failed refresh
clears token state.

### Phase 3: Wire Login And Logout Lifecycle

Update `frontend/app/api/auth/callback/route.ts`.

- Keep using `accessToken` for `bootstrapUser`.
- After successful bootstrap and app-session creation, store Keycloak token metadata in `kc_session`.
- Ensure rejected/deactivated login paths do not leave `kc_session` behind.

Update logout/session cleanup.

- Ensure the logout path clears both:
  - `session`
  - `kc_session`
- If this requires changing `deleteSession()`, keep the function server-only and avoid importing
  token logic into client code.

**Exit criteria:** successful login creates both app session and sealed Keycloak token cookie; logout
removes both.

### Phase 4: Add Gateway CORS For Browser SignalR

Browser SignalR is the first real browser-to-gateway request. Existing frontend calls are mostly BFF
server-side calls, so they did not require gateway CORS. SignalR negotiate from `localhost:3000` to
`localhost:8000` with an `Authorization` header triggers an `OPTIONS` preflight. Without gateway
CORS, the browser blocks before authentication runs.

Update `backend/api-gateway`.

- Add a scoped CORS policy for configured frontend origins.
- Do not use `AllowAnyOrigin()` with credentials.
- Use configured origins, for example `Frontend:AllowedOrigins`.
- Allow credentials.
- Allow `Authorization`, `Content-Type`, and any SignalR headers required by the client.
- Allow `GET`, `POST`, and `OPTIONS`.
- Place CORS middleware before authentication/authorization for the current minimal pipeline, or in
  the documented ASP.NET Core order if routing is introduced.
- Keep the policy narrow to frontend origins, not all origins.

**Exit criteria:** browser preflight to gateway hub negotiate succeeds from the frontend origin.

### Phase 5: Add Hub Token Route

Create `frontend/app/api/realtime/hub-token/route.ts`.

- Export `dynamic = 'force-dynamic'`.
- Implement `GET`.
- Set `Cache-Control: no-store`.
- Decrypt the app `session` cookie inline instead of using `verifySession`, because `verifySession`
  redirects to `/login`; this route should return HTTP status.
- Return `401` JSON for:
  - missing app session
  - invalid app session
  - inactive user
  - missing `kc_session`
  - failed Keycloak refresh
- Return `200` JSON with `{ "accessToken": "<fresh Keycloak access token>" }` when valid.
- Never persist this token client-side.

**Exit criteria:** unauthenticated requests return `401`; authenticated requests return a fresh
short-lived access token.

### Phase 6: Wire SignalR Access Token Factory

Update `frontend/app/lib/realtime/session-state-client.ts`.

- Change the hub builder to pass `accessTokenFactory` to `.withUrl(...)`.
- Fetch `/api/realtime/hub-token` with `credentials: 'include'` and `cache: 'no-store'`.
- Throw a stable error such as `hub_auth_failed` if token retrieval fails.
- Keep automatic reconnect. SignalR invokes `accessTokenFactory` on connect and reconnect, so
  expiry can self-heal through the route's refresh behavior.
- Keep `DashboardClient.tsx` call sites unchanged.

Example shape:

```ts
const connection = new HubConnectionBuilder()
  .withUrl(buildHubUrl(), {
    accessTokenFactory: async () => {
      const response = await fetch('/api/realtime/hub-token', {
        credentials: 'include',
        cache: 'no-store',
      })

      if (!response.ok) {
        throw new Error('hub_auth_failed')
      }

      const body = await response.json() as { accessToken: string }
      return body.accessToken
    },
  })
  .withAutomaticReconnect([0, 1500, 5000, 10000])
  .configureLogging(LogLevel.Warning)
  .build()
```

**Exit criteria:** SignalR negotiate and WebSocket upgrade authenticate through the gateway.

### Phase 7: Handle App-Session And Keycloak-Session Lifetime Mismatch

The app session currently lasts 7 days. `kc_session` is bounded by Keycloak refresh expiry, which may
be shorter. A user can therefore still have a valid UI app session while SignalR auth fails with
`401`.

Choose and implement explicit behavior for PR 1.

Recommended minimum behavior:

- Keep `hub-token` returning `401` when Keycloak tokens are missing or unrefreshable.
- In the SignalR client, distinguish auth failure from ordinary offline where practical.
- Surface a user-facing re-login path if the dashboard can represent auth expiry.
- Do not let `refreshSession()` mutate Keycloak tokens; it is a profile/access refresh path, not an
  OIDC token lifecycle path.

Optional broader behavior:

- Align app session expiry to Keycloak refresh expiry at login. This is a larger auth policy change
  and should be made deliberately.

**Exit criteria:** token expiry does not degrade silently forever; the behavior is documented in the
PR.

### Phase 8: Tests And Verification

Add minimal frontend test infrastructure only if needed.

- If adding Vitest, keep setup small:
  - `vitest.config.ts`
  - one `test` script
  - focused unit tests for pure helpers and token lifecycle
- Unit-test:
  - expiry and skew decisions
  - sealed token roundtrip
  - refresh-before-expiry
  - refresh-token rotation
  - clear-on-refresh-failure
- Route-handler tests, if practical:
  - no app session returns `401`
  - expired access token with valid refresh returns `200`
  - refresh failure returns `401`
- Manual or e2e verification:
  - Log in as an operator.
  - Confirm the real login access token has the gateway audience expected by api-gateway.
  - Open the dashboard session view.
  - Confirm SignalR reaches `Connected`.
  - Trigger a session state transition.
  - Confirm the dashboard receives `SessionStateChanged`.
  - Confirm browser traffic goes to the api-gateway, not directly to a backend service.
  - Confirm CORS preflight succeeds from frontend origin to gateway origin.

**Exit criteria:** unit tests pass where added, and a real browser flow receives hub events through
the gateway.

### Phase 9: Config And PR Notes

Update configuration and documentation.

- Add `KC_TOKEN_SECRET` to `frontend/.env.local.example`.
- Add any gateway frontend-origin CORS config to the backend config example or local compose env.
- Document:
  - `KC_TOKEN_SECRET` is required in production.
  - Development fallback may derive from `SESSION_SECRET`.
  - Rotating `KC_TOKEN_SECRET` invalidates live `kc_session` cookies and forces re-login.
  - PR 1 fixes SignalR auth only.
  - REST-through-gateway remains broken until the REST unification PR.

**Exit criteria:** PR description is explicit about scope and remaining auth-boundary work.

---

## PR 2: Role Mapping Before REST Migration

**Goal:** make gateway-injected roles match backend/frontend authorization expectations.

The gateway can emit Keycloak realm role values such as `Administrador`, while frontend/backend code
expects values such as `Administrator` and `Operator`. This is currently masked when the frontend
manually writes `X-User-Role`, but it will break once all REST calls go through the gateway.

### Work

- Decide the canonical mapping point:
  - Preferred: Keycloak realm/client role names emit canonical English domain roles.
  - Alternative: map Keycloak role names in `TrustedHeadersTransform.GetRealmRole`.
- Add tests proving gateway output matches backend expectations.
- Verify `AuthPathTests` expectations are updated to the canonical role value.
- Verify downstream authorization policies work with gateway-injected roles.

**Exit criteria:** gateway-injected `X-User-Role` is canonical and works end-to-end.

---

## PR 3: REST Gateway Unification And Gateway Hardening

**Goal:** restore the `CONTEXT-MAP.md` boundary for all frontend server-side REST calls.

### Work

- Add a shared server-only `gatewayFetch(path, init)` helper.
- `gatewayFetch` obtains a fresh Keycloak access token from the same token store used by SignalR.
- `gatewayFetch` attaches `Authorization: Bearer <fresh access token>`.
- Route all backend calls through `API_GATEWAY_URL`.
- Migrate:
  - `frontend/app/lib/teams.ts`
  - `frontend/app/lib/missions.ts`
  - `frontend/app/lib/trivias.ts`
  - `frontend/app/lib/users.ts`
  - `frontend/app/lib/identity.ts`
  - `frontend/app/lib/sessions.ts`
- Delete hardcoded `localhost:5001`, `localhost:5002`, and other direct service URLs.
- Delete manual frontend injection of `X-User-Id`, `X-User-Role`, and `X-User-Email`.
- Harden the gateway:
  - Remove inbound `X-User-Id`, `X-User-Role`, and `X-User-Email` before injecting trusted values.
  - Do not merely overwrite conditionally.
- Add tests for direct spoofed `X-User-*` headers proving gateway strips them.

**Exit criteria:** all frontend-to-backend REST traffic uses the gateway with bearer auth, and the
gateway is the only component that injects trusted identity headers.

---

## Critical Path

1. PR 1 phases 0 through 9 fix SignalR auth through the gateway.
2. PR 2 resolves role semantics and gates REST migration.
3. PR 3 migrates REST calls and closes the broader gateway boundary violation.

PR 1 can ship before PR 2 and PR 3, but it must not claim that all frontend auth routing is fixed.
It fixes authenticated realtime transport only.
