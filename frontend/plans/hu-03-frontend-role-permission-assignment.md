# Plan: HU-03 Frontend — Role & Permission Assignment

**Ref:** HU-03
**Branch:** feature/hu-03-role-permission-assignment
**Date:** 2026-05-30
**Builds on:** HU-02 frontend (users panel, deactivation, mid-session guard, dal.ts, users.ts, actions/users.ts)

---

## Context

Backend HU-03 is fully implemented. One new endpoint is available:

- `PATCH /api/users/{id}/role` — changes a user's role; `Administrator` only

HU-01 and HU-02 infrastructure is in place: session cookies, `dal.ts`, `users.ts`,
`actions/users.ts`, `UsersPanel` in `DashboardClient`, deactivation flow, mid-session guard.

Three new frontend concerns beyond what HU-02 already handles:

1. **Role selector in the users panel** — admin can change a user's role inline from the
   existing users table; the current role cell is read-only text.
2. **Participant role support** — `Role` type currently only covers `Administrator | Operator`;
   the session payload, `toDashboardRole`, and `DashboardClient` all need to handle
   `Participant`. Without this, a Participant who logs in falls silently into the operator
   branch, which is wrong.
3. **Role-based visibility and guards** — each role gets the right content; Participant must
   never see admin or operator panels; admin-only server actions must reject non-admin callers
   even on direct invocation.

---

## Verified Backend Contract

### `PATCH /api/users/{id}/role`

Path param: `id: int` (internal numeric user id)

**Request body**
```ts
{ role: "Administrator" | "Operator" | "Participant" }
```

**Response `204`** — role updated

**Error cases**
- `401` — missing trusted headers
- `403` — caller is not `Administrator`
- `400` — unknown role value (e.g. `"SuperAdmin"`)
- `422` — target user is deactivated (`"Target user must be active."`)

**Additional verified behaviour (from integration tests)**
- After the role change, the user's new role is immediately reflected in `GET /api/users` results.
- The reassigned user's `GET /api/permissions/authenticated-platform-access` still returns `isAllowed: true` — role changes do not deactivate the user.
- Their _next session bootstrap_ will carry the new role, but their current session cookie still holds the old role string until they re-authenticate.

---

## Architecture Decisions

- **Inline role selector, no modal.** The role column in `UsersPanel` becomes a `<select>` for
  admins when the row is active; the current value is pre-selected. Selecting a new value shows
  a "Save" / "Cancel" button pair in the Actions column so accidental changes are not committed
  on first click. This keeps the same two-step pattern used for deactivation.
- **Server Action owns the role change call.** `assignUserRole` in `app/actions/users.ts` runs
  server-side, calls the identity service with session-derived identity headers, and enforces
  Administrator-only at the action layer before reaching the backend. Even if the UI were
  bypassed, a direct action call from a non-admin would be rejected.
- **Optimistic UI after role change.** The role cell updates instantly on confirmation; the
  Server Action runs in a `useTransition`. On error the previous role is restored and an error
  chip appears.
- **`Participant` added to `Role` type.** `definitions.ts` currently declares
  `Role = 'Administrator' | 'Operator'`. HU-03 requires `'Participant'` to be a valid session
  role so that `SessionPayload.role` correctly types the full domain enum.
- **`DashboardRole` gains a `'participant'` variant.** `DashboardClient` maps
  `'participant'` to a read-only participant view. This view shows a minimal "you are logged
  in as a participant" panel — no admin metrics, no users table, no session controls. The
  `else` branch currently acts as the implicit admin fallback; it is made explicit so
  `'participant'` can never accidentally render admin content.
- **`toDashboardRole` in `page.tsx` maps Participant explicitly.** Any unknown role value now
  redirects to `/login` rather than silently falling through to an operator or admin view.
- **Participant view is a distinct rendered branch, not a redirect.** Participants have valid
  platform access (`isAllowed: true`). Sending them to `/login` would be incorrect. Instead,
  they land on the dashboard and see participant content. Operator and admin branches are
  never rendered for them — the server component enforces this before hydration.
- **Server Actions enforce role at the action layer.** `getUsersPage`, `deactivateUser`, and
  `assignUserRole` all call `verifySession()` and check `session.role` before reaching the
  backend. Participant callers receive `Error('Forbidden')` immediately.
- **`revalidatePath('/dashboard')` after role assignment.** Clears any RSC cache of the users
  list so the next panel open reflects the updated role from the backend.

---

## Environment

No new environment variables. `IDENTITY_SERVICE_URL` in `users.ts` already covers
`PATCH /api/users/{id}/role`.

---

## Phases

### Phase 1 — Type extension

**Scope**
- Extend `Role` union in `app/lib/definitions.ts` to include `'Participant'`.
- No other file changes in this phase; the type change will surface downstream compile errors
  that each subsequent phase resolves.

**`app/lib/definitions.ts` change**
```ts
// Before
export type Role = 'Administrator' | 'Operator'

// After
export type Role = 'Administrator' | 'Operator' | 'Participant'
```

**Gate**
- `pnpm build` may surface type errors in `DashboardClient` and `page.tsx` — that is expected
  and will be resolved in Phase 3.
- No runtime change; existing sessions are unaffected.

---

### Phase 2 — API client and Server Action for role assignment

**Scope**
- Add `assignUserRole` to `app/lib/users.ts`.
- Add `assignUserRole` Server Action to `app/actions/users.ts`.

**`app/lib/users.ts` addition**
```ts
export async function assignUserRole(id: number, role: string): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/${id}/role`, {
    method: 'PATCH',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ role }),
  })

  if (response.status === 400) {
    throw new Error('invalid_role')
  }

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }

  if (response.status === 422) {
    throw new Error('target_deactivated')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `assignUserRole failed with status ${response.status}`)
  }
}
```

**`app/actions/users.ts` addition**
```ts
export async function assignUserRole(id: number, role: string): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await assignUserRoleLib(id, role)   // imported as assignUserRole from @/app/lib/users
  revalidatePath('/dashboard')
}
```

Note: the import from `app/lib/users` must be aliased to avoid naming conflict with the
exported Server Action. Use:
```ts
import { listUsers, deactivateUserAccess, assignUserRole as assignUserRoleLib } from '@/app/lib/users'
```

**Gate**
- `pnpm build` passes with no type errors.
- Calling `assignUserRole` from an Operator session throws `'Forbidden'` before reaching the backend.

---

### Phase 3 — Participant guard and role-based visibility

**Scope**
- Update `toDashboardRole` in `app/dashboard/page.tsx` to handle `'Participant'` explicitly.
- Update `DashboardRole` type in `DashboardClient.tsx` to include `'participant'`.
- Add a `'participant'` rendering branch in `DashboardClient` that shows a minimal access
  panel and never renders admin or operator content.
- Ensure `navigation` items that are management-only are filtered by role.

**`app/dashboard/page.tsx` update**
```ts
function toDashboardRole(role: Role): 'admin' | 'operator' | 'participant' {
  switch (role) {
    case 'Administrator': return 'admin'
    case 'Operator':      return 'operator'
    case 'Participant':   return 'participant'
    // TypeScript exhaustiveness: no default needed once Role covers all three.
  }
}
```

**`DashboardClient.tsx` type update**
```ts
type DashboardRole = 'operator' | 'admin' | 'participant';
```

**Navigation filtering** — some nav items should be hidden from participants. The
navigation array does not need structural change; the filter is applied at render:
```tsx
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') {
    // Participants only see overview
    return item.key === 'overview'
  }
  if (role === 'operator') {
    // Operators do not see users management
    return item.key !== 'users'
  }
  return true // admin sees everything
})
```
Use `visibleNavigation` instead of `navigation` in the nav loop.

Note: operator not seeing the users nav item is not a regression — operators already
could navigate to the users panel. The scope says admin and operator can see the user list
(HU-02), but the _users nav item_ should remain for operators since they can still read
the list. Do not hide it for operators — only hide admin-specific management panels
(deactivate, role change). The filter above keeps `users` for operators.

Revised filter: only hide `users` for `participant`:
```tsx
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  return true
})
```

**Participant rendering branch** — insert before the existing `role === 'operator'` chain in
the main content area (the `activeNav === 'users'` check already at the top is fine; a
Participant will never reach it because `users` is not in their nav):
```tsx
{role === 'participant' ? (
  <section className={styles.emptyState} aria-labelledby="participant-title" data-testid="participant-panel">
    <div>
      <h1 id="participant-title">Welcome, {displayName}</h1>
      <p className={styles.emptyStateCopy}>
        You are logged in as a participant. Operator and admin controls are not available
        in this view.
      </p>
    </div>
  </section>
) : activeNav === 'users' ? (
  <UsersPanel role={role} />
) : role === 'operator' && ...
```

**Gate**
- `pnpm build` passes with no type errors.
- A Participant session navigating to `/dashboard` renders the participant panel and nothing
  else — no users table, no admin metrics, no session controls.
- Admin and Operator flows are unaffected.

---

### Phase 4 — Role selector in UsersPanel

**Scope**
- Add inline role editing to `UsersPanel` (admin only).
- The role cell becomes a `<select>` when the admin clicks "Change role"; the current value
  is pre-selected; a "Save" and "Cancel" button pair appear in the Actions column.
- Import `assignUserRole` from `app/actions/users`.

**State additions to `UsersPanel`**
```ts
const [roleEditId, setRoleEditId] = useState<number | null>(null)
const [pendingRole, setPendingRole] = useState<string>('')
const [roleError, setRoleError] = useState<string | null>(null)
```

**Role column rendering** (inside the `data.items.map` tbody, role cell):
```tsx
<td>
  {role === 'admin' && roleEditId === user.id ? (
    <select
      className={styles.inlineSelect}
      data-testid={`role-select-${user.id}`}
      value={pendingRole}
      onChange={(e) => setPendingRole(e.target.value)}
    >
      <option value="Administrator">Administrator</option>
      <option value="Operator">Operator</option>
      <option value="Participant">Participant</option>
    </select>
  ) : (
    user.role
  )}
</td>
```

**Actions column additions** (inside the admin `<td>`, alongside the existing deactivate logic):
```tsx
{/* Role edit trigger — only shown when not already in deactivate confirm mode */}
{user.isActive && confirmId !== user.id && roleEditId !== user.id && (
  <button
    className={styles.inlineButton}
    data-testid={`change-role-btn-${user.id}`}
    disabled={isPending}
    onClick={() => { setRoleEditId(user.id); setPendingRole(user.role) }}
    type="button"
  >
    Change role
  </button>
)}

{/* Role save/cancel — only shown when this row is in role-edit mode */}
{roleEditId === user.id && (
  <span className={styles.confirmRow}>
    <button
      className={styles.smallButton}
      data-testid={`save-role-btn-${user.id}`}
      disabled={isPending || pendingRole === user.role}
      onClick={() => handleRoleChange(user.id, user.role)}
      type="button"
    >
      Save
    </button>
    <button
      className={styles.inlineButton}
      disabled={isPending}
      onClick={() => { setRoleEditId(null); setRoleError(null) }}
      type="button"
    >
      Cancel
    </button>
  </span>
)}
```

**`handleRoleChange` function**
```ts
async function handleRoleChange(id: number, previousRole: string) {
  startTransition(async () => {
    setRoleError(null)
    try {
      await assignUserRole(id, pendingRole)
      setData((prev) =>
        prev
          ? {
              ...prev,
              items: prev.items.map((u) =>
                u.id === id ? { ...u, role: pendingRole } : u
              ),
            }
          : prev
      )
      setRoleEditId(null)
    } catch {
      setRoleError('Role change failed. Try again.')
      setPendingRole(previousRole)
      setRoleEditId(null)
    }
  })
}
```

**CSS additions to `dashboard.module.css`**
- `.inlineSelect` — matches the visual style of `.inlineButton`: small font, transparent
  background, `border: 1px solid var(--border)`, `border-radius: var(--radius-sm)`,
  `padding: 0.25rem 0.5rem`, `color: var(--text-primary)`

**`data-testid` additions required**
- `data-testid="change-role-btn-{id}"` on the role trigger button
- `data-testid="role-select-{id}"` on the role select element
- `data-testid="save-role-btn-{id}"` on the save button

**Gate**
- Admin navigates to "Users" → each active row shows both "Deactivate" and "Change role"
  buttons.
- Clicking "Change role" shows a `<select>` pre-filled with the current role and "Save" /
  "Cancel" buttons; "Save" is disabled when the pending role equals the current role.
- Selecting a different role and clicking "Save" optimistically updates the role cell;
  the row returns to read mode.
- Clicking "Cancel" discards the pending selection without any network call.
- Deactivated rows have no "Change role" button (consistent with the backend 422 constraint).
- Operator sees the users table read-only; no "Change role" button.
- Deactivate and role-change interact safely: a confirm-deactivation in progress prevents
  the role editor from opening on the same row (both check `confirmId !== user.id` /
  `roleEditId !== user.id` respectively).

---

### Phase 5 — E2E tests (HU-03 extension)

**Scope**
- Add `participantPage` fixture to `tests/fixtures/auth.ts`.
- Add `tests/e2e/roles.spec.ts` covering all HU-03 acceptance criteria.
- Verify no HU-01 or HU-02 regressions: re-run existing `auth.spec.ts` and `users.spec.ts`
  against the updated dashboard.

**`participantPage` fixture addition (`tests/fixtures/auth.ts`)**
```ts
participantPage: async ({ browser }, runPageFixture) => {
  const ctx = await browser.newContext()
  const payload: SessionPayload = {
    externalIdentityId: 'participant-1',
    displayName: 'Participant One',
    email: 'participant@umbral.local',
    role: 'Participant',
    isActive: true,
    expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
  }
  const session = await encryptForTest(payload)
  await ctx.addCookies([{ name: 'session', value: session, url: 'http://localhost:3000' }])
  const page = await ctx.newPage()
  await runPageFixture(page)
  await ctx.close()
},
```

The `test` base extension must include the new fixture type:
```ts
export const test = base.extend<{
  operatorPage: Page
  adminPage: Page
  deactivatedPage: Page
  participantPage: Page   // add this
}>({ ... })
```

**`tests/e2e/roles.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

// --- Role selector ---

test('admin can open role editor on an active user row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]').first()).toBeVisible()
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  await expect(page.locator('[data-testid^="role-select-"]').first()).toBeVisible()
})

test('admin role select is pre-filled with current role', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  const firstChangeBtn = page.locator('[data-testid^="change-role-btn-"]').first()
  // Read the current role text before opening editor
  const row = firstChangeBtn.locator('xpath=ancestor::tr')
  const currentRole = await row.locator('td:nth-child(3)').innerText()
  await firstChangeBtn.click()
  const select = page.locator('[data-testid^="role-select-"]').first()
  await expect(select).toHaveValue(currentRole.trim())
})

test('admin save button is disabled when role unchanged', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  const saveBtn = page.locator('[data-testid^="save-role-btn-"]').first()
  await expect(saveBtn).toBeDisabled()
})

test('admin can cancel role change without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  await page.locator('[data-testid^="role-select-"]').first().selectOption('Participant')
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  // row should be back to read mode — no select visible
  await expect(page.locator('[data-testid^="role-select-"]')).toHaveCount(0)
})

test('operator sees no change-role button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]')).toHaveCount(0)
})

// --- Role-based visibility ---

test('participant sees participant panel not admin or operator panel', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="operator-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="users-panel"]')).toHaveCount(0)
})

test('participant nav does not include users management item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-users"]')).toHaveCount(0)
})

// --- Route guard ---

test('participant direct URL access to dashboard yields participant view not admin content', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  // Role chip confirms participant identity
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('participant')
})

// --- HU-01 regression ---

test('HU-01 operator flow is not regressed', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
})

test('HU-01 admin flow is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})

// --- HU-02 regression ---

test('HU-02 users panel still renders for admin', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]').first()).toBeVisible()
})

test('HU-02 users panel still renders read-only for operator', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]')).toHaveCount(0)
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All four HU-03 acceptance criteria are exercised by automated tests.
- All HU-01 and HU-02 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — add Participant to Role type
feat(frontend): phase 2 — api client and server action for role assignment
feat(frontend): phase 3 — participant guard and role-based visibility
feat(frontend): phase 4 — inline role selector in users panel
feat(frontend): phase 5 — e2e tests for HU-03 and HU-01/HU-02 regression checks

Ref: HU-03
```

---

## Out of Scope

- Session cookie refresh after a role change. When an admin changes another user's role, that
  user's active session cookie still carries the old role. The new role takes effect on their
  next login. This is an accepted limitation — the same pattern applies to Keycloak-sourced
  roles in most BFF architectures.
- `GET /api/users/me` integration. The plan derives visibility from the session cookie role,
  which is set at bootstrap from Keycloak. `GET /api/users/me` could be used for a "refresh
  my own profile" flow, but that is not required by HU-03.
- Participant-specific views beyond the minimal access panel. The participant content area is
  intentionally left as a placeholder; future HUs will populate it.
- Self-role-change prevention (backend does not guard against an admin downgrading themselves;
  frontend does not need to guard it either at this stage).
- Any backend changes.
