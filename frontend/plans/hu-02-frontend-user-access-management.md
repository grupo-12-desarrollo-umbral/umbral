# Plan: HU-02 Frontend — User Access Management

**Ref:** HU-02, DES-67
**Branch:** feature/hu-02-user-access-management
**Date:** 2026-05-30
**Builds on:** HU-01 frontend (session, proxy.ts, dal.ts, login, dashboard)

---

## Context

Backend HU-02 is fully implemented (phases X.1–X.4). Two new endpoints are available:

- `GET /api/users` — paginated user catalog (all roles returned); accessible to `Administrator` and `Operator`
- `DELETE /api/users/{id}/access` — soft-deactivates a user; `Administrator` only

HU-01 infrastructure is in place: `session.ts`, `dal.ts`, `proxy.ts`, login page with
`?error=deactivated` handling, and `DashboardClient` wired to the real session. The shared
dashboard shell is kept — HU-02 adds a "Users" nav item and panel inside it rather than
creating a separate route.

The three new frontend concerns beyond what HU-01 already handles:

1. User list view with paginated table — admin and operator can see registered users.
2. Deactivate action — admin-only button with inline confirmation.
3. Mid-session deactivation — a user deactivated while logged in must be blocked on the
   next page access, not only at login. The session cookie's `isActive` field goes stale;
   the backend's `GET /api/permissions/authenticated-platform-access` is the authoritative
   check.

---

## Verified Backend Contract

### `GET /api/users`

Query params: `page: number = 1`, `pageSize: number = 20`

**Response `200`**
```ts
{
  items: Array<{
    id: number
    externalIdentityId: string
    displayName: string
    email: string
    role: string          // "Administrator" | "Operator" | "Participant"
    isActive: boolean
  }>
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}
```

**Error cases**
- `401` — missing or invalid trusted headers
- `403` — caller role is not `Administrator` or `Operator`; note: response includes all roles (<code>Participant</code> may appear)

### `DELETE /api/users/{id}/access`

Path param: `id: int` (internal numeric user id, not `externalIdentityId`)

**Response `204`** — user deactivated; history preserved

**Error cases**
- `401` — missing trusted headers
- `403` — caller is not `Administrator`
- `400` — user already deactivated (domain guard)

### `GET /api/permissions/authenticated-platform-access` (reused from HU-01)

**Response `200`** — `{ isAllowed: true, ... }` for active users
**Response `403`** — user is deactivated

---

## Architecture Decisions

- **Users panel inside DashboardClient, not a new route.** Adding a `users` key to the existing nav keeps the shell intact. The panel lazy-fetches via a Server Action when first visited; subsequent pagination calls the same action with a new page number.
- **Server Actions own the data fetch.** `getUsersPage` and `deactivateUser` in `app/actions/users.ts` run server-side, call the api-gateway with session-derived identity (consistent with the `app/lib/identity.ts` BFF pattern), and enforce role restrictions before calling the backend. Even if the UI hides the button, a direct action call is rejected.
- **Mid-session guard via `enforceActivePlatformAccess()` in dal.ts.** The dashboard Server Component (`app/dashboard/page.tsx`) calls this after `verifySession()`. It calls `GET /api/permissions/authenticated-platform-access`; on 403 it calls `deleteSession()` and redirects to `/login?error=deactivated`. Memoised with React `cache` so it fires once per render pass.
- **Admin-only deactivate UI.** The deactivate button is only rendered when `role === 'Administrator'`. Operator sessions see the users table read-only.
- **Inline confirmation, no extra dependency.** Two-step UI: first click shows a "Confirm deactivate?" state per row; second click submits the action. No dialog library.
- **Optimistic UI after deactivation.** The `isActive` column updates instantly; the Server Action runs in a `useTransition`. On error, the state reverts and an error toast appears.

---

## Environment

No new environment variables. The existing `API_GATEWAY_URL` covers the new endpoints.

---

## Phases

### Phase 1 — Types and API client

**Scope**
- Extend `app/lib/definitions.ts` with `UserAccessCatalogItemDto` and `PagedResult<T>`.
- Create `app/lib/users.ts` (server-only): `listUsers` and `deactivateUserAccess`.
- Extend `app/lib/identity.ts` with `checkPlatformAccess`.

**`app/lib/definitions.ts` additions**
```ts
export type UserAccessCatalogItemDto = {
  id: number
  externalIdentityId: string
  displayName: string
  email: string
  role: string
  isActive: boolean
}

export type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}
```

**`app/lib/users.ts`**
```ts
import 'server-only'
import type { PagedResult, UserAccessCatalogItemDto } from './definitions'

export async function listUsers(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>>
// GET ${API_GATEWAY_URL}/api/users?page=&pageSize=
// Server-to-server call with session-derived identity headers (BFF pattern,
// consistent with app/lib/identity.ts). Throws IdentityError('unauthorized')
// on 401, IdentityError('deactivated') on 403.

export async function deactivateUserAccess(id: number): Promise<void>
// DELETE ${API_GATEWAY_URL}/api/users/${id}/access
// Throws on non-204. Maps 400 → Error('already_deactivated').
```

**`app/lib/identity.ts` addition**
```ts
export async function checkPlatformAccess(): Promise<ProtectedAccessDecisionDto>
// GET ${API_GATEWAY_URL}/api/permissions/authenticated-platform-access
// Returns the dto on 200. Throws IdentityError('deactivated') on 403.
```

**Gate**
- `pnpm build` passes with no type errors.
- `users.ts` does not import from any client-only module.

---

### Phase 2 — Server Actions for user management

**Scope**
- Create `app/actions/users.ts` with two exported Server Actions.

**`app/actions/users.ts`**
```ts
'use server'
import { verifySession } from '@/app/lib/dal'
import { listUsers, deactivateUserAccess } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type { PagedResult, UserAccessCatalogItemDto } from '@/app/lib/definitions'

export async function getUsersPage(
  page: number,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const session = await verifySession()
  // Both Administrator and Operator may list users
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listUsers(page, pageSize)
}

export async function deactivateUser(id: number): Promise<void> {
  const session = await verifySession()
  // Only Administrator may deactivate
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await deactivateUserAccess(id)
  revalidatePath('/dashboard')  // invalidates any cached users data
}
```

**Gate**
- `pnpm build` passes.
- Calling `deactivateUser` as an Operator throws `'Forbidden'` before reaching the backend.

---

### Phase 3 — Mid-session deactivation guard

**Scope**
- Add `enforceActivePlatformAccess()` to `app/lib/dal.ts`.
- Call it in `app/dashboard/page.tsx` after `verifySession()`.

**`app/lib/dal.ts` addition**
```ts
export const enforceActivePlatformAccess = cache(async (): Promise<void> => {
  const session = await verifySession()          // already cached; no extra cookie read
  try {
    await checkPlatformAccess()
  } catch (err) {
    if (err instanceof IdentityError && err.code === 'deactivated') {
      await deleteSession()
      redirect('/login?error=deactivated')
    }
    throw err
  }
})
```

**`app/dashboard/page.tsx` update**
```ts
// Add after verifySession():
await enforceActivePlatformAccess()
```

**Why one call per page load is acceptable**
`GET /api/permissions/authenticated-platform-access` is a lightweight read (no database write).
It fires once per render pass (React `cache` deduplicates within the request) and only when the
user navigates to the dashboard — not on static assets or every client-side transition.

**Gate**
- Admin deactivates User A. User A navigates to `/dashboard` → session cookie is deleted,
  redirect to `/login?error=deactivated`.
- Active User B is unaffected; no extra latency on their happy-path navigation.

---

### Phase 4 — Users panel in DashboardClient

**Scope**
- Add `users` to the navigation array in `DashboardClient.tsx`.
- Add `UsersPanel` component rendered when `activeNav === 'users'`.
- Lazy-fetch first page on panel mount; pagination re-fetches via the same Server Action.
- Deactivate button visible only when `role === 'Administrator'`; inline two-step confirmation.

**Navigation addition**
```ts
const navigation = [
  ...existing items...,
  { key: 'users', label: 'Users', icon: '⊞' },
]
```

**Panel rendering** (insert before the existing `role === 'operator'` branch)
```tsx
{activeNav === 'users' ? (
  <UsersPanel role={role} />
) : role === 'operator' && selectedSessionId === 'assigned-list' ? (
  ... existing operator-assigned-sessions branch ...
) : ...
```

**`UsersPanel` component** (defined at bottom of `DashboardClient.tsx`)
```tsx
function UsersPanel({ role }: { role: DashboardRole }) {
  const [data, setData] = useState<PagedResult<UserAccessCatalogItemDto> | null>(null)
  const [page, setPage] = useState(1)
  const [isPending, startTransition] = useTransition()
  const [error, setError] = useState<string | null>(null)
  const [confirmId, setConfirmId] = useState<number | null>(null)

  // Fetch on mount and page change
  useEffect(() => {
    startTransition(async () => {
      setError(null)
      try {
        const result = await getUsersPage(page)
        setData(result)
      } catch {
        setError('Failed to load users.')
      }
    })
  }, [page])

  async function handleDeactivate(id: number) {
    startTransition(async () => {
      setError(null)
      try {
        await deactivateUser(id)
        // Optimistic update: mark row inactive
        setData((prev) =>
          prev
            ? {
                ...prev,
                items: prev.items.map((u) =>
                  u.id === id ? { ...u, isActive: false } : u
                ),
              }
            : prev
        )
        setConfirmId(null)
      } catch {
        setError('Deactivation failed. Try again.')
        setConfirmId(null)
      }
    })
  }

  return (
    <section
      className={styles.panel}
      aria-labelledby="users-panel-title"
      data-testid="users-panel"
    >
      <div className={styles.panelHeader}>
        <div>
          <h2 id="users-panel-title">Registered users</h2>
          <div className={styles.panelMeta}>
            {role === 'administrator'
              ? 'Admin and operator accounts. Deactivated users cannot log in.'
              : 'Registered accounts visible to operators.'}
          </div>
        </div>
        {isPending && <span className={styles.chip}>Loading…</span>}
      </div>

      {error && (
        <div className={styles.chip} data-tone="critical">
          {error}
        </div>
      )}

      {data && (
        <>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                {role === 'administrator' && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {data.items.map((user) => (
                <tr key={user.id}>
                  <td>{user.displayName}</td>
                  <td>{user.email}</td>
                  <td>{user.role}</td>
                  <td>
                    <span
                      className={styles.chip}
                      data-tone={user.isActive ? 'success' : 'critical'}
                    >
                      {user.isActive ? 'Active' : 'Deactivated'}
                    </span>
                  </td>
                  {role === 'administrator' && (
                    <td>
                      {user.isActive && confirmId !== user.id && (
                        <button
                          className={styles.inlineButton}
                          data-testid={`deactivate-btn-${user.id}`}
                          disabled={isPending}
                          onClick={() => setConfirmId(user.id)}
                          type="button"
                        >
                          Deactivate
                        </button>
                      )}
                      {user.isActive && confirmId === user.id && (
                        <span className={styles.confirmRow}>
                          <button
                            className={styles.smallButton}
                            data-testid={`confirm-deactivate-btn-${user.id}`}
                            data-tone="critical"
                            disabled={isPending}
                            onClick={() => handleDeactivate(user.id)}
                            type="button"
                          >
                            Confirm
                          </button>
                          <button
                            className={styles.inlineButton}
                            disabled={isPending}
                            onClick={() => setConfirmId(null)}
                            type="button"
                          >
                            Cancel
                          </button>
                        </span>
                      )}
                      {!user.isActive && (
                        <span className={styles.mutedText}>—</span>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          <div className={styles.pagination} data-testid="users-pagination">
            <button
              className={styles.inlineButton}
              disabled={!data.hasPreviousPage || isPending}
              onClick={() => setPage((p) => p - 1)}
              type="button"
            >
              ← Previous
            </button>
            <span className={styles.panelMeta}>
              Page {data.page} of {data.totalPages} ({data.totalCount} users)
            </span>
            <button
              className={styles.inlineButton}
              disabled={!data.hasNextPage || isPending}
              onClick={() => setPage((p) => p + 1)}
              type="button"
            >
              Next →
            </button>
          </div>
        </>
      )}
    </section>
  )
}
```

**CSS additions to `dashboard.module.css`**
- `.pagination` — flex row, space-between, `padding-block: 0.75rem`
- `.confirmRow` — flex row, gap, inline
- `.mutedText` — `color: var(--text-subtle)`, already exists

**Gate**
- Admin navigates to "Users" → table renders with fetched data.
- Admin sees "Deactivate" button on active rows; clicking shows "Confirm / Cancel"; confirming updates the row status chip to "Deactivated".
- Operator sees the table read-only; no "Deactivate" column.
- Pagination controls advance and retreat pages.

---

### Phase 5 — E2E tests (HU-02 extension)

**Scope**
- Add `tests/e2e/users.spec.ts` covering all HU-02 acceptance criteria.
- Extend `tests/fixtures/auth.ts` with a `deactivatedMidSessionPage` fixture (active cookie, but backend `checkPlatformAccess` returns 403).
- Verify no HU-01 regressions: re-run existing `auth.spec.ts` tests against the updated dashboard.

**New fixtures (`tests/fixtures/auth.ts` additions)**
```ts
// adminPage already exists or is added here
adminPage: async ({ browser }, use) => {
  const ctx = await browser.newContext()
  const payload: SessionPayload = {
    externalIdentityId: 'admin-1',
    displayName: 'Admin One',
    email: 'admin@umbral.local',
    role: 'Administrator',
    isActive: true,
    expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
  }
  const session = await encrypt(payload)
  await ctx.addCookies([{ name: 'session', value: session, domain: 'localhost', path: '/' }])
  const page = await ctx.newPage()
  await use(page)
  await ctx.close()
},
```

**`tests/e2e/users.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

test('admin can navigate to users panel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
})

test('admin sees deactivate button on active users', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="deactivate-btn-"]').first()).toBeVisible()
})

test('operator sees users panel read-only', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]')).toHaveCount(0)
})

test('deactivated user is blocked at login with clear error', async ({ page }) => {
  // This fixture has no session cookie; Keycloak would return deactivated user.
  // Simulate: navigate directly with error param (backend handles the 403 at bootstrap).
  await page.goto('/login?error=deactivated')
  await expect(page.locator('[data-testid="deactivated-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="deactivated-chip"]')).toContainText('deactivated')
})

test('active user HU-01 operator flow is not regressed', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
})

test('active user HU-01 admin flow is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})
```

**`data-testid` additions required in Phase 4**
- `data-testid="nav-users"` on the "Users" navigation button in `DashboardClient`
- `data-testid="users-panel"` already specified in `UsersPanel`
- `data-testid="deactivate-btn-{id}"` already specified
- `data-testid="confirm-deactivate-btn-{id}"` already specified
- `data-testid="users-pagination"` already specified

**Gate**
- `pnpm exec playwright test` passes.
- All four HU-02 acceptance criteria are exercised by automated tests.
- HU-01 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — user management types and API client layer
feat(frontend): phase 2 — server actions for user listing and deactivation
feat(frontend): phase 3 — mid-session deactivation guard in dal.ts
feat(frontend): phase 4 — users panel in dashboard with deactivation UI
feat(frontend): phase 5 — e2e tests for HU-02 and HU-01 regression checks

Ref: HU-02
Ref: DES-67
```

---

## Out of Scope

- Re-activation of deactivated users (no such endpoint exists yet).
- Role assignment or promotion (HU-03).
- Search or filter on the users table (backend supports simple pagination only; no server-side filter param).
- Pagination via URL query params / deep linking into a specific users page.
- Any backend changes.
