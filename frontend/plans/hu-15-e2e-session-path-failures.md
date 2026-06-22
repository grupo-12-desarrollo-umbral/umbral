# Plan: HU-15 E2E Session Create/List Failures

**Branch:** `feature/hu-15-session-creation-from-mission`  
**Date:** 2026-06-21  
**Source:** `/tmp/HANDOFF-hu15-e2e-session-path.md`  
**Scope:** Frontend e2e harness and tests only, unless investigation disproves the auth-fixture hypothesis.

---

## Problem

`cd frontend && npx playwright test sessions.spec.ts` has three failing tests:

- Happy path: admin creates a session, but the assignment list remains empty.
- Reset flow: same create/list failure, so the form does not prove a successful reset.
- Typed 409 flow: the test expects the mission-ineligible copy, but its mock bypasses the code that maps backend 409 responses.

The backend session create/list path has already been verified outside Playwright. Direct calls to `session-operations` work, the gateway route exists, and the session create endpoint returns `201` for valid mission input. During the failing e2e run, `session-operations` received zero `/api/sessions` traffic.

---

## Likely Root Cause

The Playwright auth fixture only injects the app `session` cookie. The session gateway client in `frontend/app/lib/sessions.ts` also requires a valid `kc_session` cookie so `getValidAccessToken()` can attach a bearer token:

```ts
const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
  headers: await getGatewayHeaders(),
})
```

If `kc_session` is missing, `getGatewayHeaders()` throws before `fetch()` is issued. That exactly matches the observed zero traffic to `session-operations`.

Missions still load because `frontend/app/lib/missions.ts` uses a different local-dev path: it calls `localhost:5001` directly with trusted identity headers derived from the app `session` cookie. So "missions work" does not prove gateway auth is valid for sessions.

---

## Safety Boundary

Do not change production session client behavior unless the investigation disproves the missing-`kc_session` hypothesis.

Avoid:

- Adding trusted-header fallbacks to `frontend/app/lib/sessions.ts`.
- Bypassing Keycloak bearer auth for session endpoints.
- Changing gateway routes without direct route-matching evidence.
- Changing backend session create/list behavior for a Playwright-only failure.

The safest fix is to make the e2e harness match the real auth path.

---

## Phase 1: Confirm the Failure Mode

1. Run the focused suite:

```bash
cd frontend
npx playwright test sessions.spec.ts
```

2. Watch backend traffic during the failing tests:

```bash
docker logs -f backend-session-operations-service-1
```

3. If confirmation is still needed, temporarily log inside `frontend/app/lib/sessions.ts` around:

- `getValidAccessToken()`
- `fetch(`${API_GATEWAY_URL}/api/sessions`)`

Expected result: the action fails before fetch because `kc_session` is missing or invalid.

Remove any temporary logging before committing.

---

## Phase 2: Fix the Playwright Auth Fixture

Update `frontend/tests/fixtures/auth.ts` so authenticated fixtures add both cookies:

- `session`: current encrypted app session cookie.
- `kc_session`: sealed Keycloak token cookie accepted by `getValidAccessToken()`.

Preferred implementation:

1. Obtain real Keycloak tokens for each e2e actor that needs gateway-backed APIs.
2. Seal those tokens using the same shape as `frontend/app/lib/keycloak-tokens.ts`.
3. Add the sealed value as a `kc_session` cookie on `http://localhost:3000`.

Actors that likely need `kc_session`:

- `adminPage`, because create/list/assign session actions use `/api/sessions`.
- `operatorPage`, because operator session and realtime-token flows may also depend on gateway bearer auth.

Keep `participantPage` unchanged unless a failing participant flow requires gateway auth.

If the seeded app users do not exist in Keycloak, update the e2e setup so Keycloak and the identity DB agree on the e2e users:

- `admin-1`
- `op-1`
- `participant-1`
- `deactivated-1`

Do not rely on direct DB identity rows alone for gateway-authenticated session calls.

---

## Phase 3: Validate the Happy Path

Re-run:

```bash
cd frontend
npx playwright test sessions.spec.ts
```

Validate:

- The "admin can create a session..." test creates `E2E Mission Night`.
- The new session appears in `[data-testid="session-operator-list"]`.
- The state chip shows `Scheduled`.
- The create form clears after success.
- `session-operations` logs show real `POST /api/sessions` and `GET /api/sessions` traffic.

If traffic reaches `session-operations` but the tests still fail, continue diagnosis at the response/UI refresh layer rather than auth.

---

## Phase 4: Fix the Invalid Typed-409 E2E

The current typed 409 test intercepts the browser's POST to `/dashboard`:

```ts
await page.route('**/dashboard', ...)
```

That intercepts the Next Server Action request, not the backend gateway request. It bypasses `frontend/app/lib/sessions.ts`, where the backend `ProblemDetails.type` is mapped to `mission_not_eligible`. Therefore the test cannot prove the typed backend mapping.

Replace it with one of these safer options:

1. Unit-test the mapping in `frontend/tests/unit/app/lib/sessions.test.ts`.
2. Drive a real backend 409 by selecting or seeding a genuinely ineligible mission.

Recommendation: keep the typed `mission-not-eligible-for-session` mapping as a unit test and keep e2e focused on user-visible behavior from real paths.

The generic 409 e2e can remain only if its assertion is generic:

- Error banner is visible.
- Form remains intact.

Do not assert the specific "inactive or not runtime-ready" copy from a browser-level `/dashboard` mock.

---

## Verification

Run:

```bash
cd frontend
pnpm exec vitest run tests/unit/app/lib/sessions.test.ts
npx playwright test sessions.spec.ts
```

Optional broader checks:

```bash
cd frontend
pnpm lint
pnpm build
```

---

## Success Criteria

- Session create/list e2e tests produce real `/api/sessions` traffic.
- Happy-path create and reset tests pass.
- Typed backend 409 mapping is covered at the correct layer.
- No production auth fallback or gateway contract change is introduced for a test harness gap.
