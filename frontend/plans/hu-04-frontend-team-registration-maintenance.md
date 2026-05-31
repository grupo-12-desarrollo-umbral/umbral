# Plan: HU-04 Frontend — Team Registration and Maintenance

**Ref:** HU-04 / DES-8 / DES-67
**Branch:** feature/hu-04-team-registration
**Date:** 2026-05-31
**Builds on:** HU-03 frontend (participant guard, `visibleNavigation`, dal.ts, users.ts, actions/users.ts, UsersPanel, TeamsPanel nav key already present)

---

## Context

Backend HU-04 is fully implemented prior to this frontend slice. Five new endpoints are
available in `identity-access-service`:

- `POST /api/teams` — register a new team; Administrator only
- `GET /api/teams` — paginated list (active and inactive); Administrator or Operator
- `GET /api/teams/{id}` — single team detail; Administrator or Operator
- `PATCH /api/teams/{id}` — update DisplayName and TeamCode; Administrator only
- `DELETE /api/teams/{id}/status` — soft deactivate; Administrator only

HU-01 through HU-03 infrastructure is in place: session cookies, `dal.ts`, `users.ts`,
`actions/users.ts`, `UsersPanel` in `DashboardClient`, two-step deactivation pattern,
participant guard, `visibleNavigation` filtering.

Four new frontend concerns beyond what HU-03 already handles:

1. **TeamsPanel** — `DashboardClient.tsx` already declares
   `{ key: 'teams', label: 'Teams', icon: '◫' }` in the navigation array, but
   `activeNav === 'teams'` currently falls through to the admin overview. HU-04 wires that
   key to a proper `TeamsPanel` component.
2. **Sub-view state within TeamsPanel** — list → detail → create/edit navigation is managed
   as local component state, consistent with the in-panel approach used by `UsersPanel`.
   No new Next.js routes are introduced.
3. **Role-scoped team surface** — Administrators see create / edit / deactivate actions;
   Operators see the panel read-only. Participants are already excluded from the 'teams'
   nav item by the existing `visibleNavigation` filter (Phase 3 of HU-03 established
   `if (role === 'participant') return item.key === 'overview'`).
4. **Server Actions for all team mutations** — `app/actions/teams.ts` checks
   `session.role` before reaching the backend, matching the action-layer guard established
   in HU-02 and HU-03.

---

## Verified Backend Contract

### `POST /api/teams`

**Request body**
```ts
{ displayName: string; teamCode: string }
```

**Response `201`**
```ts
{ id: string }  // UUID of the newly registered team
```

**Error cases**
- `400` — blank displayName or teamCode
- `403` — caller is not Administrator
- `409` — teamCode already taken

---

### `GET /api/teams`

**Query params:** `page` (int), `pageSize` (int)

**Response `200`**
```ts
PagedResult<TeamDto>
```

Where `TeamDto` is:
```ts
{
  id: string          // UUID
  displayName: string
  teamCode: string
  isActive: boolean
  createdAt: string   // ISO 8601
  updatedAt: string   // ISO 8601
}
```

**Error cases**
- `401` — missing trusted headers
- `403` — caller is Participant (Administrator and Operator both have access)

---

### `GET /api/teams/{id}`

Path param: `id: string` (UUID)

**Response `200`:** `TeamDto`

**Error cases**
- `403` — caller is Participant
- `404` — team not found

---

### `PATCH /api/teams/{id}`

**Request body**
```ts
{ displayName: string; teamCode: string }
```

**Response `204`**

**Error cases**
- `400` — blank fields
- `403` — caller is not Administrator
- `404` — team not found (deleted between navigation and form submission)
- `409` — teamCode conflicts with another team's code

---

### `DELETE /api/teams/{id}/status`

**Response `200`**
```ts
TeamDto  // isActive: false
```

**Error cases**
- `403` — caller is not Administrator
- `409` / `422` — team is already inactive

---

## Architecture Decisions

- **In-panel sub-views, no new URL routes.** Team management lives entirely within the
  existing `/dashboard` route. `TeamsPanel` maintains local sub-view state
  (`'list' | 'detail' | 'create' | 'edit'`) so team detail, creation, and editing happen
  without URL changes, consistent with the `UsersPanel` pattern. Guard requirements are
  satisfied at three layers: the `/dashboard` server component already rejects non-active
  sessions; `visibleNavigation` already excludes Participants from the 'teams' key; Server
  Actions enforce `role === 'Administrator'` before reaching the backend.
- **Separate `TeamsPanel.tsx` file.** `DashboardClient.tsx` is already ~1400 lines.
  `TeamsPanel` is extracted into its own `app/dashboard/TeamsPanel.tsx` (Client Component),
  matching the split used for `UsersPanel` but placed in a dedicated file to keep both
  files maintainable. The single change to `DashboardClient.tsx` is adding one branch to
  the content switch and one import.
- **`createTeam` Server Action returns `{ id: string }`.** After a successful POST, the
  client immediately calls `getTeam(id)` to load the new team's full detail and transitions
  to the detail sub-view. This avoids a URL redirect while still showing the newly created
  team immediately.
- **Shared `TeamForm` internal component for create and edit.** Both sub-views render the
  same form component with `mode: 'create' | 'edit'`. Client-side blank-field validation
  runs before the action call. A 409 response surfaces as a form-level error chip
  ("A team with this code already exists."). A 404 response during edit surfaces as
  "Team no longer exists."
- **Deactivation mirrors the HU-02 two-step pattern.** In the detail sub-view, "Deactivate"
  is replaced by "Confirm" + "Cancel" on first click. Confirming calls `deactivateTeam(id)`.
  The backend returns the updated `TeamDto`; the client updates `selectedTeam` from the
  response (no separate re-fetch needed). On 409/422, an error chip appears and confirm
  state resets.
- **Operator enforcement is two-layer.** The `TeamsPanel` component renders no
  create/edit/deactivate buttons when `role !== 'admin'`. Server Actions throw
  `Error('Forbidden')` for non-Administrator callers, so UI bypass is rejected server-side
  as well.
- **`revalidatePath('/dashboard')` after mutations.** Create, update, and deactivate all
  revalidate the dashboard path so subsequent team list fetches reflect backend state.
- **CSS additions are self-contained.** Four new classes are added to `dashboard.module.css`:
  `.formGroup`, `.formInput`, `.fieldError`, `.detailList`. The existing `.confirmRow`,
  `.inlineButton`, `.primaryButton`, `.smallButton`, `.chip`, `.pagination`, and
  `.paginationButtons` styles are reused without modification.

---

## Environment

No new environment variables. `IDENTITY_SERVICE_URL` (`http://localhost:5002`) in
`app/lib/teams.ts` mirrors the constant already in `app/lib/users.ts`.

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add `TeamDto` and `CreateTeamResultDto` to `app/lib/definitions.ts`.
- No other file changes; this phase surfaces type errors in later phases at compile time.

**`app/lib/definitions.ts` additions**
```ts
export type TeamDto = {
  id: string
  displayName: string
  teamCode: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export type CreateTeamResultDto = {
  id: string
}
```

`PagedResult<T>` already covers `PagedResult<TeamDto>` with no change.

**Gate**
- `pnpm build` passes. No runtime change.

---

### Phase 2 — API client (`app/lib/teams.ts`)

**Scope**
- Create `app/lib/teams.ts` with all five backend calls.
- Error codes follow the `Error('code_string')` / `IdentityError` pattern from `users.ts`.
- `getIdentityHeaders` is redeclared locally (same shape as `users.ts`); no shared helper
  is extracted per the project's don't-over-abstract rule.

**`app/lib/teams.ts`**
```ts
import 'server-only'
import { IdentityError, type PagedResult, type TeamDto, type CreateTeamResultDto } from './definitions'
import { verifySession } from './dal'

const IDENTITY_SERVICE_URL = 'http://localhost:5002'

function getIdentityHeaders(session: {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
}) {
  return {
    'X-User-Id': session.externalIdentityId,
    'X-User-Role': session.role,
    'X-User-Email': session.email,
  }
}

export async function listTeams(page = 1, pageSize = 20): Promise<PagedResult<TeamDto>> {
  const session = await verifySession()
  const url = new URL(`${IDENTITY_SERVICE_URL}/api/teams`)
  url.searchParams.set('page', String(page))
  url.searchParams.set('pageSize', String(pageSize))

  const response = await fetch(url.toString(), {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `listTeams failed with status ${response.status}`)
  }

  return response.json()
}

export async function getTeamById(id: string): Promise<TeamDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `getTeamById failed with status ${response.status}`)
  }

  return response.json()
}

export async function createTeam(
  displayName: string,
  teamCode: string,
): Promise<CreateTeamResultDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ displayName, teamCode }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 409) {
    throw new Error('duplicate_team_code')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createTeam failed with status ${response.status}`)
  }

  return response.json()
}

export async function updateTeam(
  id: string,
  displayName: string,
  teamCode: string,
): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}`, {
    method: 'PATCH',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ displayName, teamCode }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (response.status === 409) {
    throw new Error('duplicate_team_code')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `updateTeam failed with status ${response.status}`)
  }
}

export async function deactivateTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}/status`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 409 || response.status === 422) {
    throw new Error('already_inactive')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `deactivateTeam failed with status ${response.status}`)
  }

  return response.json()
}
```

**Gate**
- `pnpm build` passes. No runtime change.

---

### Phase 3 — Server Actions (`app/actions/teams.ts`)

**Scope**
- Create `app/actions/teams.ts` with five Server Actions.
- `createTeam`, `updateTeam`, and `deactivateTeam` are Administrator-only.
- `getTeamsPage` and `getTeam` are available to Administrator and Operator.
- Import aliases avoid naming conflicts with the lib functions.

**`app/actions/teams.ts`**
```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTeams,
  getTeamById,
  createTeam as createTeamLib,
  updateTeam as updateTeamLib,
  deactivateTeam as deactivateTeamLib,
} from '@/app/lib/teams'
import { revalidatePath } from 'next/cache'
import type { PagedResult, TeamDto, CreateTeamResultDto } from '@/app/lib/definitions'

export async function getTeamsPage(
  page: number,
  pageSize = 20,
): Promise<PagedResult<TeamDto>> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listTeams(page, pageSize)
}

export async function getTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return getTeamById(id)
}

export async function createTeam(
  displayName: string,
  teamCode: string,
): Promise<CreateTeamResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await createTeamLib(displayName, teamCode)
  revalidatePath('/dashboard')
  return result
}

export async function updateTeam(
  id: string,
  displayName: string,
  teamCode: string,
): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await updateTeamLib(id, displayName, teamCode)
  revalidatePath('/dashboard')
}

export async function deactivateTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await deactivateTeamLib(id)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes.
- Calling `createTeam` / `updateTeam` / `deactivateTeam` from an Operator session throws
  `'Forbidden'` before reaching the backend.

---

### Phase 4 — Teams list view and navigation wiring

**Scope**
- Create `app/dashboard/TeamsPanel.tsx` with the list sub-view (the default view on first
  mount) plus the state shell that all subsequent sub-views will use.
- Add the `activeNav === 'teams'` branch and import to `DashboardClient.tsx`.

#### `DashboardClient.tsx` — two changes only

**Import addition** (at top of file alongside other panel imports):
```ts
import { TeamsPanel } from './TeamsPanel'
```

**Content switch addition** (immediately after the `activeNav === 'users'` branch):
```tsx
// Before:
) : activeNav === 'users' ? (
  <UsersPanel role={role} />
) : role === 'operator' && selectedSessionId === 'assigned-list' ? (

// After:
) : activeNav === 'users' ? (
  <UsersPanel role={role} />
) : activeNav === 'teams' ? (
  <TeamsPanel role={role} />
) : role === 'operator' && selectedSessionId === 'assigned-list' ? (
```

No other changes to `DashboardClient.tsx`. The 'teams' nav key is already in the
`navigation` array; `visibleNavigation` already filters it for Participants.

#### `app/dashboard/TeamsPanel.tsx` — full file

```ts
'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getTeamsPage,
  getTeam,
  createTeam,
  updateTeam,
  deactivateTeam,
} from '@/app/actions/teams'
import type { PagedResult, TeamDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TeamPanelView = 'list' | 'detail' | 'create' | 'edit'
```

**State declarations** (inside `TeamsPanel`):
```ts
const [view, setView] = useState<TeamPanelView>('list')
const [selectedTeam, setSelectedTeam] = useState<TeamDto | null>(null)
const [listData, setListData] = useState<PagedResult<TeamDto> | null>(null)
const [page, setPage] = useState(1)
const [isPending, startTransition] = useTransition()
const [listError, setListError] = useState<string | null>(null)
const [formError, setFormError] = useState<string | null>(null)
const [deactivateError, setDeactivateError] = useState<string | null>(null)
const [confirmDeactivate, setConfirmDeactivate] = useState(false)
```

**List fetch** (on mount and page change):
```ts
useEffect(() => {
  startTransition(async () => {
    setListError(null)
    try {
      const result = await getTeamsPage(page)
      setListData(result)
    } catch {
      setListError('Failed to load teams.')
    }
  })
}, [page])
```

**List sub-view** (rendered when `view === 'list'`):
```tsx
<section
  className={styles.panel}
  aria-labelledby="teams-panel-title"
  data-testid="teams-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <h2 id="teams-panel-title">Registered teams</h2>
      <div className={styles.panelMeta}>
        {role === 'admin'
          ? 'Team registry. Inactive teams are preserved for audit.'
          : 'Read-only team catalog.'}
      </div>
    </div>
    <div className={styles.panelActions}>
      {isPending && <span className={styles.chip}>Loading…</span>}
      {role === 'admin' && (
        <button
          className={styles.primaryButton}
          data-testid="create-team-btn"
          disabled={isPending}
          onClick={() => { setFormError(null); setView('create') }}
          type="button"
        >
          + New team
        </button>
      )}
    </div>
  </div>

  {listError && (
    <div className={styles.chip} data-tone="critical">
      {listError}
    </div>
  )}

  {listData && (
    <>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Code</th>
            <th>Status</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          {listData.items.map((team) => (
            <tr
              key={team.id}
              data-testid={`team-row-${team.id}`}
              onClick={() => {
                setSelectedTeam(team)
                setConfirmDeactivate(false)
                setDeactivateError(null)
                setView('detail')
              }}
              style={{ cursor: 'pointer' }}
            >
              <td>{team.displayName}</td>
              <td>{team.teamCode}</td>
              <td>
                <span
                  className={styles.chip}
                  data-tone={team.isActive ? 'success' : 'critical'}
                >
                  {team.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>{new Date(team.createdAt).toLocaleDateString()}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className={styles.pagination} data-testid="teams-pagination">
        <span className={styles.panelMeta}>
          Page {listData.page} of {listData.totalPages} ({listData.totalCount} teams)
        </span>
        <span className={styles.paginationButtons}>
          <button
            className={styles.inlineButton}
            disabled={!listData.hasPreviousPage || isPending}
            onClick={() => setPage((p) => p - 1)}
            type="button"
          >
            ← Previous
          </button>
          <button
            className={styles.inlineButton}
            disabled={!listData.hasNextPage || isPending}
            onClick={() => setPage((p) => p + 1)}
            type="button"
          >
            Next →
          </button>
        </span>
      </div>
    </>
  )}
</section>
```

**`data-testid` additions required in this phase**
- `data-testid="teams-panel"` on the section
- `data-testid="create-team-btn"` on the create button (admin only)
- `data-testid="team-row-{id}"` on each table row
- `data-testid="teams-pagination"` on the pagination bar

**Gate**
- `pnpm build` passes.
- Admin navigating to "Teams" sees the list table with a "+ New team" button.
- Operator navigating to "Teams" sees the list table without a create button.
- Participant does not see the "Teams" nav item (no change from HU-03).
- Clicking a team row transitions to a detail view (implemented in Phase 5).

---

### Phase 5 — Team detail sub-view and deactivation

**Scope**
- Add the detail sub-view rendering to `TeamsPanel.tsx`.
- Add `handleDeactivate` handler.
- Deactivation uses the same two-step confirmation pattern as user deactivation in HU-02.

**`handleDeactivate` function**
```ts
async function handleDeactivate(id: string) {
  startTransition(async () => {
    setDeactivateError(null)
    try {
      const updated = await deactivateTeam(id)
      setSelectedTeam(updated)
      setConfirmDeactivate(false)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'already_inactive') {
        setDeactivateError('This team is already inactive.')
      } else {
        setDeactivateError('Deactivation failed. Try again.')
      }
      setConfirmDeactivate(false)
    }
  })
}
```

**Detail sub-view** (rendered when `view === 'detail' && selectedTeam !== null`):
```tsx
<section
  className={styles.panel}
  aria-labelledby="team-detail-title"
  data-testid="team-detail-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <button
        className={styles.inlineButton}
        data-testid="teams-back-btn"
        onClick={() => { setView('list'); setConfirmDeactivate(false); setDeactivateError(null) }}
        type="button"
      >
        ← Teams
      </button>
      <h2 id="team-detail-title">{selectedTeam.displayName}</h2>
      <div className={styles.panelMeta}>{selectedTeam.teamCode}</div>
    </div>

    {role === 'admin' && selectedTeam.isActive && (
      <div className={styles.panelActions}>
        {!confirmDeactivate && (
          <button
            className={styles.inlineButton}
            data-testid="edit-team-btn"
            disabled={isPending}
            onClick={() => { setFormError(null); setView('edit') }}
            type="button"
          >
            Edit
          </button>
        )}
        {!confirmDeactivate ? (
          <button
            className={styles.inlineButton}
            data-testid="deactivate-team-btn"
            disabled={isPending}
            onClick={() => setConfirmDeactivate(true)}
            type="button"
          >
            Deactivate
          </button>
        ) : (
          <span className={styles.confirmRow}>
            <button
              className={styles.smallButton}
              data-testid="confirm-deactivate-team-btn"
              data-tone="critical"
              disabled={isPending}
              onClick={() => handleDeactivate(selectedTeam.id)}
              type="button"
            >
              Confirm
            </button>
            <button
              className={styles.inlineButton}
              disabled={isPending}
              onClick={() => setConfirmDeactivate(false)}
              type="button"
            >
              Cancel
            </button>
          </span>
        )}
      </div>
    )}
  </div>

  {deactivateError && (
    <div className={styles.chip} data-tone="critical">
      {deactivateError}
    </div>
  )}

  <dl className={styles.detailList} data-testid="team-detail-fields">
    <dt>Display name</dt>
    <dd data-testid="detail-display-name">{selectedTeam.displayName}</dd>
    <dt>Team code</dt>
    <dd data-testid="detail-team-code">{selectedTeam.teamCode}</dd>
    <dt>Status</dt>
    <dd>
      <span
        className={styles.chip}
        data-tone={selectedTeam.isActive ? 'success' : 'critical'}
        data-testid="detail-status"
      >
        {selectedTeam.isActive ? 'Active' : 'Inactive'}
      </span>
    </dd>
    <dt>Created</dt>
    <dd>{new Date(selectedTeam.createdAt).toLocaleDateString()}</dd>
    <dt>Last updated</dt>
    <dd>{new Date(selectedTeam.updatedAt).toLocaleDateString()}</dd>
  </dl>
</section>
```

**CSS addition to `dashboard.module.css`**
```css
.detailList {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.5rem 1.5rem;
  padding: 0.5rem 0;
}

.detailList dt {
  color: var(--text-secondary);
  font-size: 0.82rem;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  padding-top: 0.15rem;
}

.detailList dd {
  margin: 0;
}
```

**`data-testid` additions required in this phase**
- `data-testid="team-detail-panel"` on the section
- `data-testid="teams-back-btn"` on the back button
- `data-testid="edit-team-btn"` on the edit button (admin + active only)
- `data-testid="deactivate-team-btn"` on the deactivate trigger (admin + active only)
- `data-testid="confirm-deactivate-team-btn"` on the confirm button
- `data-testid="team-detail-fields"` on the `<dl>`
- `data-testid="detail-display-name"`, `detail-team-code`, `detail-status` on the `<dd>` cells

**Gate**
- Clicking a team row shows the detail sub-view with all fields.
- Admin sees "Edit" and "Deactivate" buttons for active teams; no action buttons for
  inactive teams.
- Operator sees no action buttons.
- Clicking "Deactivate" replaces it with "Confirm" + "Cancel"; cancelling restores the
  original button pair.
- Confirming deactivation updates the status chip to "Inactive" and hides the action
  buttons (the team is now inactive).
- Already-inactive team deactivation attempt surfaces an error chip.
- "← Teams" returns to the list sub-view.

---

### Phase 6 — Team create form

**Scope**
- Add the `TeamForm` internal component to `TeamsPanel.tsx`.
- Add the create sub-view rendering and `handleCreate` handler.
- `TeamForm` is reused by the edit sub-view in Phase 7.

**`TeamForm` internal component** (defined within `TeamsPanel.tsx`, not exported):
```tsx
function TeamForm({
  mode,
  initialValues,
  onSubmit,
  onCancel,
  isPending,
  formError,
}: {
  mode: 'create' | 'edit'
  initialValues?: { displayName: string; teamCode: string }
  onSubmit: (displayName: string, teamCode: string) => void
  onCancel: () => void
  isPending: boolean
  formError: string | null
}) {
  const [displayName, setDisplayName] = useState(initialValues?.displayName ?? '')
  const [teamCode, setTeamCode] = useState(initialValues?.teamCode ?? '')
  const [fieldErrors, setFieldErrors] = useState<{
    displayName?: string
    teamCode?: string
  }>({})

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const errors: { displayName?: string; teamCode?: string } = {}
    if (!displayName.trim()) errors.displayName = 'Display name is required.'
    if (!teamCode.trim()) errors.teamCode = 'Team code is required.'
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }
    setFieldErrors({})
    onSubmit(displayName.trim(), teamCode.trim())
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      {formError && (
        <div className={styles.chip} data-tone="critical" data-testid="form-error">
          {formError}
        </div>
      )}

      <div className={styles.formGroup}>
        <label htmlFor="team-display-name">Display name</label>
        <input
          id="team-display-name"
          className={styles.formInput}
          data-testid="team-display-name-input"
          type="text"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          disabled={isPending}
        />
        {fieldErrors.displayName && (
          <span className={styles.fieldError} data-testid="display-name-error">
            {fieldErrors.displayName}
          </span>
        )}
      </div>

      <div className={styles.formGroup}>
        <label htmlFor="team-code">Team code</label>
        <input
          id="team-code"
          className={styles.formInput}
          data-testid="team-code-input"
          type="text"
          value={teamCode}
          onChange={(e) => setTeamCode(e.target.value)}
          disabled={isPending}
        />
        {fieldErrors.teamCode && (
          <span className={styles.fieldError} data-testid="team-code-error">
            {fieldErrors.teamCode}
          </span>
        )}
      </div>

      <div className={styles.panelActions}>
        <button
          className={styles.primaryButton}
          data-testid="team-form-submit"
          disabled={isPending}
          type="submit"
        >
          {mode === 'create' ? 'Create team' : 'Save changes'}
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          onClick={onCancel}
          type="button"
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
```

**`handleCreate` function**
```ts
async function handleCreate(displayName: string, teamCode: string) {
  startTransition(async () => {
    setFormError(null)
    try {
      const result = await createTeam(displayName, teamCode)
      const newTeam = await getTeam(result.id)
      setSelectedTeam(newTeam)
      setConfirmDeactivate(false)
      setDeactivateError(null)
      setView('detail')
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'duplicate_team_code') {
        setFormError('A team with this code already exists.')
      } else {
        setFormError('Failed to create team. Try again.')
      }
    }
  })
}
```

**Create sub-view** (rendered when `view === 'create' && role === 'admin'`):
```tsx
<section
  className={styles.panel}
  aria-labelledby="create-team-title"
  data-testid="create-team-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <button
        className={styles.inlineButton}
        onClick={() => { setView('list'); setFormError(null) }}
        type="button"
      >
        ← Teams
      </button>
      <h2 id="create-team-title">New team</h2>
    </div>
  </div>

  <TeamForm
    mode="create"
    isPending={isPending}
    formError={formError}
    onCancel={() => { setView('list'); setFormError(null) }}
    onSubmit={handleCreate}
  />
</section>
```

**CSS additions to `dashboard.module.css`**
```css
.formGroup {
  display: grid;
  gap: 0.35rem;
  margin-bottom: 1rem;
}

.formGroup label {
  color: var(--text-secondary);
  font-size: 0.82rem;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.formInput {
  min-height: 2.85rem;
  padding: 0.7rem 0.95rem;
  border: 1px solid var(--border-subtle);
  border-radius: 1rem;
  background: var(--surface);
  color: var(--text-primary);
  font-size: 1rem;
}

.formInput:focus {
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}

.formInput:disabled {
  opacity: 0.58;
  cursor: not-allowed;
}

.fieldError {
  color: var(--critical);
  font-size: 0.82rem;
}
```

**`data-testid` additions required in this phase**
- `data-testid="create-team-panel"` on the section
- `data-testid="team-display-name-input"` on the name input
- `data-testid="team-code-input"` on the code input
- `data-testid="team-form-submit"` on the submit button
- `data-testid="form-error"` on the form-level error chip
- `data-testid="display-name-error"`, `data-testid="team-code-error"` on field-level errors

**Gate**
- Admin clicks "+ New team" → create sub-view renders.
- Submitting blank fields shows the per-field error messages; no network call is made.
- Submitting valid fields creates the team and transitions to the detail sub-view for the
  new team.
- Duplicate TeamCode (409) shows "A team with this code already exists." as a form-level
  error chip.
- "Cancel" and "← Teams" return to the list without a network call.

---

### Phase 7 — Team edit form

**Scope**
- Add the edit sub-view rendering and `handleUpdate` handler to `TeamsPanel.tsx`.
- Reuses `TeamForm` from Phase 6.
- Handles 404 (team deleted during edit) and 409 (duplicate TeamCode) gracefully.

**`handleUpdate` function**
```ts
async function handleUpdate(displayName: string, teamCode: string) {
  if (!selectedTeam) return
  startTransition(async () => {
    setFormError(null)
    try {
      await updateTeam(selectedTeam.id, displayName, teamCode)
      const refreshed = await getTeam(selectedTeam.id)
      setSelectedTeam(refreshed)
      setView('detail')
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'duplicate_team_code') {
        setFormError('A team with this code already exists.')
      } else if (msg === 'team_not_found') {
        setFormError('This team no longer exists.')
      } else {
        setFormError('Failed to save changes. Try again.')
      }
    }
  })
}
```

**Edit sub-view** (rendered when `view === 'edit' && selectedTeam !== null && role === 'admin'`):
```tsx
<section
  className={styles.panel}
  aria-labelledby="edit-team-title"
  data-testid="edit-team-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <button
        className={styles.inlineButton}
        onClick={() => { setView('detail'); setFormError(null) }}
        type="button"
      >
        ← {selectedTeam.displayName}
      </button>
      <h2 id="edit-team-title">Edit team</h2>
    </div>
  </div>

  <TeamForm
    mode="edit"
    initialValues={{
      displayName: selectedTeam.displayName,
      teamCode: selectedTeam.teamCode,
    }}
    isPending={isPending}
    formError={formError}
    onCancel={() => { setView('detail'); setFormError(null) }}
    onSubmit={handleUpdate}
  />
</section>
```

**`data-testid` additions required in this phase**
- `data-testid="edit-team-panel"` on the section
- The `TeamForm` testids (`team-display-name-input`, `team-code-input`, `team-form-submit`,
  `form-error`, `display-name-error`, `team-code-error`) are shared with the create form.

**Gate**
- Clicking "Edit" from the detail sub-view pre-populates the form with the current values.
- Saving valid changed fields updates the detail sub-view to show the new values.
- Duplicate TeamCode shows the form-level error chip.
- 404 (team deleted since navigation) shows "This team no longer exists."
- "Cancel" returns to the detail sub-view without a network call.
- "← {teamName}" also returns to detail without a network call.

---

### Phase 8 — E2E tests (HU-04 extension)

**Scope**
- Add `tests/e2e/teams.spec.ts` covering all HU-04 acceptance criteria.
- No new fixtures needed; `adminPage`, `operatorPage`, and `participantPage` are already
  defined in `tests/fixtures/auth.ts`.
- Verify no HU-01, HU-02, or HU-03 regressions.

**`tests/e2e/teams.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

// --- Teams list view ---

test('admin sees teams panel with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-btn"]')).toBeVisible()
})

test('operator sees teams panel without create button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-btn"]')).toHaveCount(0)
})

test('participant does not see teams nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-teams"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
})

// --- Team detail view ---

test('admin can open team detail by clicking a row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="detail-team-code"]')).toBeVisible()
  await expect(page.locator('[data-testid="edit-team-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toBeVisible()
})

test('operator detail view has no action buttons', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="edit-team-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toHaveCount(0)
})

test('back button from detail returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="teams-back-btn"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
})

// --- Team deactivation ---

test('admin deactivate flow shows confirm step then updates status', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Find an active team row
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="deactivate-team-btn"]')
  // Confirm step appears
  await expect(page.locator('[data-testid="confirm-deactivate-team-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-deactivate-team-btn"]')
  // After deactivation the status chip shows Inactive
  await expect(page.locator('[data-testid="detail-status"]')).toContainText('Inactive')
  // Edit and deactivate buttons are gone for inactive teams
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toHaveCount(0)
})

test('admin can cancel deactivation', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="deactivate-team-btn"]')
  await expect(page.locator('[data-testid="confirm-deactivate-team-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toBeVisible()
})

// --- Team create form ---

test('admin can open create form and cancel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
  await page.click('[data-testid="team-form-submit"]')
  // Blank submit → field errors, no panel change
  await expect(page.locator('[data-testid="display-name-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="team-code-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
})

test('admin create team success navigates to detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await page.fill('[data-testid="team-display-name-input"]', 'Alpha Squad')
  await page.fill('[data-testid="team-code-input"]', 'ALPHA')
  await page.click('[data-testid="team-form-submit"]')
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="detail-display-name"]')).toContainText('Alpha Squad')
})

// --- Team edit form ---

test('admin edit form is pre-populated', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  const currentCode = await page.locator('[data-testid="detail-team-code"]').innerText()
  await page.click('[data-testid="edit-team-btn"]')
  await expect(page.locator('[data-testid="edit-team-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="team-code-input"]')).toHaveValue(currentCode.trim())
})

test('admin cancel edit returns to detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="edit-team-btn"]')
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
})

// --- Role-based guards ---

test('participant navigating to dashboard never shows team content', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="teams-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="team-detail-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="create-team-panel"]')).toHaveCount(0)
})

// --- HU-01 regression ---

test('HU-01 operator flow is not regressed by teams', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
})

test('HU-01 admin flow is not regressed by teams', async ({ adminPage: page }) => {
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

// --- HU-03 regression ---

test('HU-03 admin role change is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]').first()).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-04 acceptance criteria are exercised.
- HU-01, HU-02, and HU-03 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — add TeamDto and CreateTeamResultDto types
feat(frontend): phase 2 — api client for team endpoints (teams.ts)
feat(frontend): phase 3 — server actions for team management (actions/teams.ts)
feat(frontend): phase 4 — teams list panel and navigation wiring
feat(frontend): phase 5 — team detail sub-view and deactivation
feat(frontend): phase 6 — team create form
feat(frontend): phase 7 — team edit form
feat(frontend): phase 8 — e2e tests for HU-04 and regression checks

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

---

## Out of Scope

- **New Next.js URL routes for team pages.** All team sub-views are inline within the
  existing `/dashboard` route, consistent with `UsersPanel`. The "direct URL access"
  guard is satisfied by the `/dashboard` server component role check and `visibleNavigation`
  filtering — Participants cannot render team content regardless of `activeNav` state,
  because the `role === 'participant'` branch in `DashboardClient.tsx` takes precedence
  over all `activeNav` branches.
- **Dashboard shell extraction (`layout.tsx` refactor).** If a future HU introduces
  dedicated URL routes for teams or other management areas, a shared `DashboardShell`
  layout should be extracted at that point. It is not worth the refactoring risk for HU-04.
- **Team search or filtering.** The list returns all teams paginated; no search input is
  added. Future HUs can layer filtering onto `GET /api/teams` query params.
- **Session-operations `Team` entity.** The Identity-side `Team` introduced here is
  reference data only (DisplayName, TeamCode, IsActive). It must never reference runtime
  fields (score, join status, progress) owned by `session-operations-service`.
- **Self-serve team management for Participants.** Participants cannot view or interact
  with team management at all in this slice.
- **Optimistic list update after create.** After a successful create the client re-fetches
  the new team detail via `getTeam(result.id)` but does not eagerly push the new team into
  the list. The list refreshes naturally when the user navigates back to it (the page
  counter resets to 1 on each mount), or after `revalidatePath('/dashboard')` clears the
  RSC cache on the next full page load.
