# Plan: HU-01 Frontend — General User Login

**Ref:** HU-01, DES-5
**Branch:** feature/hu-01-general-user-login
**Date:** 2026-05-30

---

## Context

The backend for HU-01 is fully implemented (phases X.1–X.4). The auth boundary is:

1. Keycloak authenticates the user and issues a JWT.
2. The api-gateway validates the JWT, strips `Authorization`, and injects trusted headers `X-User-Id`, `X-User-Email`, `X-User-Role` before proxying to downstream services.
3. `POST /api/users/authenticated` — post-Keycloak bootstrap: creates or syncs the internal `User` and returns an `AuthenticateUserResultDto`.
4. `GET /api/users/me` — returns `AuthenticatedActorProfileDto` for an authenticated actor.
5. `GET /api/permissions/authenticated-platform-access` — confirms active platform access; rejects deactivated users with 403.

The frontend never validates JWTs directly. It holds the Keycloak access token only long enough to call the bootstrap endpoint, then stores a short-lived encrypted session cookie that carries `{ externalIdentityId, displayName, email, role, isActive }`. All subsequent UI and route-guard decisions read from that cookie.

There is one shared Next.js 16 app for all roles (Operator, Admin). Role-aware rendering happens inside the shared dashboard, not in separate apps.

---

## Verified Backend Contract

### `POST /api/users/authenticated`

Requires `Authorization: Bearer <access_token>` (forwarded to api-gateway; gateway strips it and injects trusted headers).

**Request body**
```json
{ "displayName": "string" }
```

**Response `200`**
```ts
{
  actor: {
    externalIdentityId: string
    displayName: string
    email: string
    role: string           // "Admin" | "Operator"
    isActive: boolean
  }
  access: {
    capability: string     // "AuthenticatedPlatformAccess"
    isAllowed: boolean
    reason: string
  }
}
```

**Error cases**
- `401` — missing or invalid trusted headers (gateway rejected the token).
- `403` — user is deactivated (`isActive: false`, `access.isAllowed: false`).

### `GET /api/users/me`

Same auth requirements. Returns `AuthenticatedActorProfileDto` directly.

### `GET /api/permissions/authenticated-platform-access`

Returns `ProtectedAccessDecisionDto`. Returns `403` for deactivated users.

---

## Architecture Decisions

- **No external auth library** — the session is a stateless encrypted cookie built with `jose`. Keycloak OIDC is driven manually via redirect → callback route handler. This keeps the dependency surface minimal and avoids library version drift with Next.js 16.
- **Proxy (`proxy.ts`)** — Next.js 16 uses `proxy.ts` (not `middleware.ts`). It reads the session cookie for optimistic route guards. No database calls inside the proxy.
- **Server Actions for logout** — `deleteSession()` + `redirect('/login')` in a Server Action.
- **DAL (`app/lib/dal.ts`)** — `verifySession()` is the single server-side gate used by Server Components, Server Actions, and Route Handlers. Memoised with React `cache`.
- **Session payload** — stores only `{ externalIdentityId, role, isActive, displayName, email }`. The Keycloak access token is **never** persisted in the session cookie; it is only held in memory during the callback route handler.
- **Role-aware UI** — the existing `app/dashboard/page.tsx` renders two structurally different layouts: an operator surface (session controls, leaderboard, clue control, submission queue) and an admin surface (cross-session metrics, operator assignments, configuration shortcuts). The hardcoded role toggle is replaced with the `role` from the session. No separate admin/operator route trees.

---

## Environment Variables

```bash
# .env.local
SESSION_SECRET=<openssl rand -base64 32>

KEYCLOAK_URL=http://localhost:8080
KEYCLOAK_REALM=umbral
KEYCLOAK_CLIENT_ID=umbral-web
KEYCLOAK_CLIENT_SECRET=        # empty for public clients
NEXT_PUBLIC_KEYCLOAK_URL=http://localhost:8080
NEXT_PUBLIC_KEYCLOAK_REALM=umbral
NEXT_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-web

API_GATEWAY_URL=http://localhost:5000
```

`NEXT_PUBLIC_*` variables are safe to expose because they only point to the public Keycloak authorization endpoint. `SESSION_SECRET` and `KEYCLOAK_CLIENT_SECRET` are server-only.

---

## Phases

### Phase 1 — Dependencies and Session Foundation

**Scope**
- Install `jose` (`pnpm add jose`).
- Create `app/lib/definitions.ts` — shared TypeScript types used across auth modules.
- Create `app/lib/session.ts` — `encrypt`, `decrypt`, `createSession`, `deleteSession`, `updateSession`.
- Create `.env.local.example` documenting all required variables.

**Files**

`app/lib/definitions.ts`
```ts
export type Role = 'Admin' | 'Operator'

export type SessionPayload = {
  externalIdentityId: string
  displayName: string
  email: string
  role: Role
  isActive: boolean
  expiresAt: Date
}
```

`app/lib/session.ts`
```ts
import 'server-only'
import { SignJWT, jwtVerify } from 'jose'
import { cookies } from 'next/headers'
import type { SessionPayload } from './definitions'

const key = new TextEncoder().encode(process.env.SESSION_SECRET)

export async function encrypt(payload: SessionPayload): Promise<string>
export async function decrypt(token: string): Promise<SessionPayload | null>

export async function createSession(payload: Omit<SessionPayload, 'expiresAt'>): Promise<void>
// sets httpOnly, secure, sameSite: 'lax', 7-day expiry

export async function deleteSession(): Promise<void>
// deletes the 'session' cookie
```

**Gate**
- `pnpm build` passes with no type errors.
- `session.ts` does not import from any client-only module.

---

### Phase 2 — Keycloak OIDC Flow and Bootstrap

**Scope**
- Create `app/lib/keycloak.ts` — builds the Keycloak authorization URL and exchanges the authorization code for tokens (server-only, never exported to the client).
- Create `app/api/auth/callback/route.ts` — handles the Keycloak redirect callback: exchanges the code, calls `POST /api/users/authenticated` via the api-gateway, creates the session cookie, and redirects to `/dashboard`.
- Create `app/actions/auth.ts` — `logout()` Server Action: deletes session, redirects to `/login`.
- Create `app/lib/identity.ts` — `bootstrapUser(accessToken, displayName)` — the typed fetch call to the api-gateway; throws on 401/403 with descriptive errors.

**Key implementation notes**

`app/lib/keycloak.ts`
```ts
import 'server-only'

export function buildAuthorizationUrl(state: string): string {
  // Returns: ${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/auth
  //   ?client_id=…&redirect_uri=…&response_type=code&scope=openid+profile+email&state=…
}

export async function exchangeCode(code: string, state: string): Promise<{
  accessToken: string
  idToken: string
  displayName: string
  email: string
}>
// Calls the Keycloak token endpoint server-side. Parses the id_token to extract
// preferred_username / name claims for the displayName.
```

`app/api/auth/callback/route.ts`
```ts
// GET handler
// 1. Read ?code and ?state from the query string
// 2. Validate state matches the one stored in a short-lived cookie (CSRF protection)
// 3. exchangeCode(code) → { accessToken, displayName, email }
// 4. identity.bootstrapUser(accessToken, displayName) → AuthenticateUserResultDto
// 5. If !result.access.isAllowed → redirect to /login?error=deactivated
// 6. createSession({ externalIdentityId: actor.externalIdentityId, role: actor.role,
//      isActive: actor.isActive, displayName: actor.displayName, email: actor.email })
// 7. redirect('/dashboard')
```

`app/lib/identity.ts`
```ts
import 'server-only'

export async function bootstrapUser(
  accessToken: string,
  displayName: string,
): Promise<AuthenticateUserResultDto>
// POST ${API_GATEWAY_URL}/api/users/authenticated
// Authorization: Bearer accessToken
// Body: { displayName }
// Throws IdentityError('deactivated') on 403
// Throws IdentityError('unauthorized') on 401
```

**Gate**
- Successful Keycloak OIDC roundtrip results in a session cookie being set and a redirect to `/dashboard`.
- Deactivated user lands on `/login?error=deactivated`.
- State mismatch or missing code returns 400.

---

### Phase 3 — Login Page (`/login`)

**Scope**
- Create `app/login/page.tsx` — Server Component that renders a `LoginCard` client component.
- Create `app/login/LoginCard.tsx` — `'use client'` component with a "Sign in" button that redirects to the Keycloak authorization URL.
- Create `app/login/login.module.css` — styles following DESIGN.md (warm ember palette, Geist Sans, instrument-control inputs).

**Page structure**

```
/login
  Centered vertically and horizontally on the page.
  Umbral compass mark (SVG, already exists in dashboard) at the top.
  Title: "Umbral" + subtitle: "Command center"
  Primary button: "Sign in with Keycloak" → redirects to Keycloak authorization URL
  If ?error=deactivated in the URL, show an inline error chip: "Your account has been deactivated."
  If ?error=unauthorized, show: "Authentication failed. Try again."
```

**Design alignment**
- Page background: `--bg-page` (Ivory Fog / Charcoal Room).
- Card: `panel-surface` with `1.4rem` radius, `1rem` padding, inset sheen.
- Button: primary ember button per DESIGN.md component spec.
- Error chip: `signal-critical` tone, pill shape.
- No logo image, no password input — Keycloak handles all credential UI.

**Gate**
- Navigating to `/login` renders the page without runtime errors.
- Clicking "Sign in" redirects the browser to the correct Keycloak authorization URL.
- `?error=deactivated` shows the deactivated error chip.

---

### Phase 4 — Route Protection (`proxy.ts`)

**Scope**
- Create `proxy.ts` at the root of `frontend/` (Next.js 16 proxy file, not `middleware.ts`).
- Define protected and public routes.
- Read and decrypt the session cookie (optimistic — no DB call).
- Enforce: unauthenticated → `/login`, deactivated → `/login?error=deactivated`, authenticated on public route → `/dashboard`.

**Implementation**

```ts
// proxy.ts
import { NextRequest, NextResponse } from 'next/server'
import { decrypt } from '@/app/lib/session'

const PUBLIC_ROUTES = ['/login']
const AUTH_CALLBACK = '/api/auth/callback'

export default async function proxy(req: NextRequest) {
  const path = req.nextUrl.pathname

  // Let the callback route through unconditionally
  if (path.startsWith(AUTH_CALLBACK)) return NextResponse.next()

  const isPublic = PUBLIC_ROUTES.some((r) => path.startsWith(r))

  const cookie = req.cookies.get('session')?.value
  const session = cookie ? await decrypt(cookie) : null

  if (!session && !isPublic) {
    return NextResponse.redirect(new URL('/login', req.nextUrl))
  }

  if (session && !session.isActive && !isPublic) {
    return NextResponse.redirect(new URL('/login?error=deactivated', req.nextUrl))
  }

  if (session && isPublic) {
    return NextResponse.redirect(new URL('/dashboard', req.nextUrl))
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.png$).*)'],
}
```

**Gate**
- `GET /dashboard` without a session cookie → 307 to `/login`.
- `GET /dashboard` with a valid session cookie → page renders.
- `GET /dashboard` with a session where `isActive: false` → 307 to `/login?error=deactivated`.
- `GET /login` with a valid active session → 307 to `/dashboard`.

---

### Phase 5 — DAL and Server-Side Session Access

**Scope**
- Create `app/lib/dal.ts` — `verifySession()` and `getSessionUser()`.
- `verifySession()` is the authoritative server-side gate: reads + decrypts the cookie, redirects to `/login` if missing or expired, and returns the `SessionPayload`. Memoised with React `cache` to avoid duplicate cookie reads in one render pass.

**Implementation**

```ts
// app/lib/dal.ts
import 'server-only'
import { cache } from 'react'
import { cookies } from 'next/headers'
import { redirect } from 'next/navigation'
import { decrypt } from './session'
import type { SessionPayload } from './definitions'

export const verifySession = cache(async (): Promise<SessionPayload> => {
  const cookie = (await cookies()).get('session')?.value
  const session = await decrypt(cookie)

  if (!session) redirect('/login')
  if (!session.isActive) redirect('/login?error=deactivated')

  return session
})
```

**Gate**
- Server Components and Server Actions that call `verifySession()` get the typed `SessionPayload` or are redirected — never see `null`.
- Multiple calls to `verifySession()` within one React render pass hit the cache rather than re-reading the cookie.

---

### Phase 6 — Wire Dashboard to Real Session

**Scope**
- Convert `app/dashboard/page.tsx` from a pure Client Component to a Server Component shell + a Client Component interior.
- The Server Component calls `verifySession()` and passes `{ role, displayName }` down to the dashboard as props.
- Remove the hardcoded role `<select>` in the topbar — the role is read-only from the session.
- Replace the `AK` avatar initials button with the user's actual initials derived from `displayName`.
- Wire the logout button to the `logout()` Server Action from `app/actions/auth.ts`.

**Before/after role boundary**

```
Before (Phase 6 target):                   After:
app/dashboard/page.tsx ('use client')  →   app/dashboard/page.tsx  (Server Component)
                                               ↓ calls verifySession()
                                               ↓ passes { role, displayName } as props
                                           app/dashboard/DashboardClient.tsx ('use client')
                                               role comes from props, not local state
                                               operator branch → session-centric layout
                                               admin branch    → oversight layout
```

**Changes to DashboardClient**
- Remove `const [role, setRole] = useState<Role>('operator')`.
- Remove the role `<select>` from the topbar — the role is determined at login and cannot be toggled at runtime.
- Add a `role: Role` prop; the existing operator/admin branch logic is unchanged.
- Add a logout form button that invokes the `logout` Server Action.
- Replace `AK` with `initials(displayName)` helper.

**Gate** (all five HU-01 acceptance criteria)
- User can sign in via Keycloak and land on `/dashboard`.
- Role chip and role-conditional panels reflect the session role (`Admin` or `Operator`).
- Navigating to `/dashboard` without a session redirects to `/login`.
- Navigating to `/dashboard` as a deactivated user redirects to `/login?error=deactivated`.
- Operator view shows operator panels; Admin view shows admin panels; no cross-role bleed.

---

### Phase 7 — E2E Testing with Playwright MCP

**Scope**
- Adjust the existing Playwright setup (`playwright.config.ts` and `tests/` directory, already created via `pnpm playwright create`) for the Next.js 16 dev server.
- Set up Playwright MCP (Model Context Protocol) integration for test execution, trace collection, and reporting.
- Add test fixtures for authenticated sessions that bypass Keycloak by programmatically creating encrypted session cookies.
- Write E2E tests covering:
  1. Unauthenticated access to `/dashboard` redirects to `/login`.
  2. Authenticated active user lands on `/dashboard` and sees role-conditional UI.
  3. Deactivated user is redirected to `/login?error=deactivated`.
  4. Logout action clears the session and redirects to `/login`.
- Run tests using Playwright MCP; collect traces and screenshots on failure.

**Files**

`playwright.config.ts` (update existing)
```ts
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [['html', { open: 'never' }], ['mcp']], // MCP reporter
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
})
```

`tests/fixtures/auth.ts`
```ts
import { test as base, Page } from '@playwright/test'
import { encrypt } from '@/app/lib/session'
import type { SessionPayload } from '@/app/lib/definitions'

export * from '@playwright/test'

export const test = base.extend<{
  operatorPage: Page
  adminPage: Page
  deactivatedPage: Page
}>({
  operatorPage: async ({ browser }, use) => {
    const ctx = await browser.newContext()
    const payload: SessionPayload = {
      externalIdentityId: 'op-1',
      displayName: 'Operator One',
      email: 'op@umbral.local',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encrypt(payload)
    await ctx.addCookies([{ name: 'session', value: session, domain: 'localhost', path: '/' }])
    const page = await ctx.newPage()
    await use(page)
    await ctx.close()
  },
  // …adminPage and deactivatedPage follow same pattern
})
```

`tests/e2e/auth.spec.ts`
```ts
import { test, expect } from '../fixtures/auth'

test('unauthenticated user is redirected to login', async ({ page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login')
})

test('operator sees operator dashboard', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toHaveText('Operator')
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).not.toBeVisible()
})

test('admin sees admin dashboard', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toHaveText('Admin')
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})

test('deactivated user is redirected to deactivated error', async ({ deactivatedPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login?error=deactivated')
  await expect(page.locator('[data-testid="deactivated-chip"]')).toBeVisible()
})

test('logout clears session and redirects to login', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="logout-button"]')
  await expect(page).toHaveURL('/login')
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login')
})
```

**Implementation notes**
- The `encrypt` helper from `app/lib/session.ts` is reused in the test fixture so that the session cookie is identical to production.
- `data-testid` attributes must be added to the relevant UI elements in Phase 6 (role chip, operator panel, admin panel, deactivated chip, logout button).
- Playwright MCP reporter streams test results to the MCP server; ensure the MCP server endpoint is configured in CI.
- Existing `tests/example.spec.ts` can be removed once auth tests are in place.

**Gate**
- `pnpm exec playwright test` passes locally.
- Playwright MCP reporter produces output without errors.
- All five acceptance criteria are exercised by automated E2E tests.

---

## Commit Sequence

```
feat(frontend): phase 1 — session foundation  (jose, definitions, session helpers)
feat(frontend): phase 2 — keycloak oidc flow and bootstrap endpoint client
feat(frontend): phase 3 — login page /login
feat(frontend): phase 4 — route protection proxy.ts
feat(frontend): phase 5 — DAL verifySession
feat(frontend): phase 6 — wire dashboard to real session and role
feat(frontend): phase 7 — e2e tests with playwright mcp

Ref: HU-01
Ref: DES-5
```

---

## Out of Scope

- Keycloak theming, self-registration, or password-reset flows.
- Token refresh / session renewal (the 7-day cookie lifetime covers the sprint window).
- Participant or mobile flows (HU-06).
- Role assignment or deactivation UI (HU-02, HU-03).
- Any backend changes.
