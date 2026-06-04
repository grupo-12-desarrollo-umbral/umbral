# Plan: HU-18 Frontend — Session Team Association

**Ref:** HU-18 / DES-25 / DES-70
**Branch:** feature/hu-18-asociacion-de-equipos-a-sesiones
**Date:** 2026-06-03
**Builds on:** HU-05 frontend (TeamsPanel with participant assignment, `app/lib/teams.ts`,
`app/actions/teams.ts`, `definitions.ts` with `TeamDto`, `TeamMembershipDto`)

---

## Context

Backend HU-18 is fully implemented. Two new endpoints are available on the
session-operations service, accessible via the API gateway at `SESSION_OPERATIONS_SERVICE_URL`:

- `POST /api/sessions/{liveSessionId}/teams` — associate a registered active team to a
  scheduled session; Operator or Administrator
- `GET /api/sessions/{liveSessionId}/teams` — list all teams already associated with a
  session; Operator or Administrator

HU-01 through HU-05 infrastructure is in place: session cookies, `dal.ts`, identity
headers pattern, `TeamDto`, `getTeamsPage`, `TeamsPanel` with full list/detail/create/edit
sub-views, Server Actions with role guards, and the `visibleNavigation` filter.

Three new frontend concerns:

1. **Type definitions** — three new DTO types that mirror the verified backend response
   shapes: `AssociatedSessionTeamDto`, `SessionAssociatedTeamsDto`, and
   `AssociateTeamToSessionResultDto`.
2. **API client and Server Actions** — a new `app/lib/session-operations.ts` calling
   `SESSION_OPERATIONS_SERVICE_URL`, and `app/actions/session-operations.ts` with role
   guards. This is the first frontend module that calls the session-operations service.
3. **`SessionsPanel` and dashboard wiring** — a new `SessionsPanel` component where the
   operator enters a session GUID, loads the list of already-associated teams, and assigns
   additional active teams from the registered team catalog. All backend error codes map to
   deterministic user-facing messages.

**Scope limitation — no session list endpoint:** The session-operations service exposes no
`GET /api/sessions` listing endpoint in the verified contract. The `SessionsPanel` therefore
opens with a session-ID input form. The operator enters the GUID of a scheduled session (as
provided by an administrator who created the session). A future HU may introduce session
listing; this plan does not invent that surface.

---

## Verified Backend Contract

The following is derived exclusively from the integration tests in
`backend/services/session-operations-service/tests/IntegrationTests/Api/SessionTeamAssociationEndpointTests.cs`
and the endpoint definition in
`backend/services/session-operations-service/src/Api/Endpoints/SessionsEndpoints.cs`.

### `POST /api/sessions/{liveSessionId}/teams`

Path param: `liveSessionId: string` (UUID)

**Request body**
```ts
{ referenceTeamId: string }  // UUID of the team from the identity service catalog
```

**Response `200`**
```ts
{
  liveSessionId: string        // UUID
  runtimeTeamId: string        // UUID — session-scoped team entity
  referenceTeamId: string      // UUID — catalog reference
  displayName: string
  teamCode: string
  sessionState: string         // e.g. "Scheduled"
  associatedTeamCount: number
}
```

**Error cases**
- `401` — missing trusted headers
- `403` — caller has Participant role
- `400` — team is inactive in catalog (`ProblemDetails.title: "Validation failed."`)
- `404` — `referenceTeamId` not found in team catalog
- `409` — team already associated to this session (`ProblemDetails.title: "Conflict."`)

### `GET /api/sessions/{liveSessionId}/teams`

Path param: `liveSessionId: string` (UUID)

**Response `200`**
```ts
{
  liveSessionId: string
  teams: Array<{
    runtimeTeamId: string      // UUID
    referenceTeamId: string    // UUID
    displayName: string
    teamCode: string
    joinStatus: string         // participant join state for this team in this session
  }>
}
```

**Error cases**
- `401` — missing trusted headers
- `403` — caller has Participant role

**Additional verified behaviour**
- Both endpoints require the `AuthorizationPolicies.Operator` policy, which accepts
  `Operator` or `Administrator` roles — Participant is explicitly rejected (`403`).
- The `referenceTeamId` in the POST body is the same `teamId` UUID exposed by
  `GET /api/teams` on the identity service. There is no separate ID mapping needed.
- Associating the same `referenceTeamId` twice to the same session returns `409`; the
  association is idempotent from the catalog perspective but not from the session's.
- Associating an inactive team (where `isActive: false` in the catalog) returns `400`, not
  `404`. The catalog presence check and the active-state check are distinct.

---

## Architecture Decisions

- **`SESSION_OPERATIONS_SERVICE_URL` as the service boundary.** The env var is already
  declared in `.env.local` at `http://localhost:5003`. `app/lib/session-operations.ts`
  reads it directly, following the same pattern as `IDENTITY_SERVICE_URL` in `teams.ts`
  and `MISSION_DESIGN_SERVICE_URL` in `missions.ts`.
- **Separate lib and actions files for session-operations.** The pattern used by missions,
  teams, users, and trivias is to split the raw HTTP client (`app/lib/`) from the Server
  Action wrapper (`app/actions/`). This plan follows that convention rather than inlining
  fetch calls in the component.
- **Session GUID input, no session list.** Because `GET /api/sessions` is not part of the
  verified contract, `SessionsPanel` opens with an input form that accepts a raw UUID.
  Submitting the form loads both the associated-teams list and the available-teams catalog
  in parallel. This is the most conservative approach that stays within the verified
  surface. The operator obtains the GUID from the admin who scheduled the session.
- **Parallel load on session lookup.** On form submit, `Promise.all` fetches associated
  teams and the team catalog together. Both are needed to render the picker. Loading either
  independently would cause a sequential waterfall with no benefit.
- **Available-team picker excludes already-associated teams.** The list of assignable teams
  is `availableTeams.items.filter(t => !associatedTeams.some(a => a.referenceTeamId === t.teamId))`.
  This client-side filter prevents the operator from attempting a duplicate association that
  the backend would reject with `409`. It does not make the client-side guard a substitute
  for backend validation — the error handler still maps `409` to a user message.
- **Optimistic update after successful association.** On `200`, the new team entry
  (constructed from the `AssociateTeamToSessionResultDto`) is appended to
  `associatedTeams` without re-fetching, matching the pattern in `TeamsPanel` and
  `UsersPanel`.
- **Four distinct error messages.** The backend returns four different failure modes for the
  POST. The API client maps each status to a typed error code string; the component maps
  each code to a human-readable message:
  - `session_not_found` (404 on GET) → "Session not found. Check the session ID."
  - `team_not_found` (404 on POST) → "Team not found in the catalog."
  - `team_inactive` (400) → "This team is inactive and cannot be assigned to a session."
  - `duplicate_association` (409) → "This team is already associated with this session."
- **`SessionsPanel` is a separate file.** Following `TeamsPanel.tsx`, `MissionsPanel.tsx`,
  and `TriviasPanel.tsx`, the component lives in
  `app/dashboard/SessionsPanel.tsx` and is imported into `DashboardClient.tsx`. This keeps
  the 1 400-line `DashboardClient.tsx` from growing further.
- **Nav visibility: sessions visible to Operator and Administrator, hidden from Participant.**
  The existing `visibleNavigation` filter already excludes everything except `overview`
  for participants. No additional filter clause is needed; adding the `sessions` nav item
  at any position other than `overview` automatically hides it from participants.
- **`revalidatePath('/dashboard')` after association.** Clears any RSC cache so if the
  operator navigates away and back, the associated-team list reflects the latest state from
  the server rather than a stale RSC snapshot.

---

## Environment

No new environment variables. `SESSION_OPERATIONS_SERVICE_URL` is already declared in
`.env.local`:

```
SESSION_OPERATIONS_SERVICE_URL=http://localhost:5003
```

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add three new DTO types to `app/lib/definitions.ts` that mirror the session-operations
  service response shapes.
- No other file changes in this phase.

**`app/lib/definitions.ts` additions**
```ts
export type AssociatedSessionTeamDto = {
  runtimeTeamId: string    // UUID — session-scoped identity
  referenceTeamId: string  // UUID — catalog identity (matches TeamDto.teamId)
  displayName: string
  teamCode: string
  joinStatus: string       // participant join state; display-only for now
}

export type SessionAssociatedTeamsDto = {
  liveSessionId: string
  teams: AssociatedSessionTeamDto[]
}

export type AssociateTeamToSessionResultDto = {
  liveSessionId: string
  runtimeTeamId: string
  referenceTeamId: string
  displayName: string
  teamCode: string
  sessionState: string
  associatedTeamCount: number
}
```

**Gate**
- `pnpm build` passes with no type errors. No runtime change.

---

### Phase 2 — API client for session-operations

**Scope**
- Create `app/lib/session-operations.ts` with two functions: `getSessionAssociatedTeams`
  and `associateTeamToSession`.
- Both functions use the same `getIdentityHeaders` pattern as `teams.ts` and `missions.ts`.

**`app/lib/session-operations.ts`**
```ts
import 'server-only'
import {
  IdentityError,
  type SessionAssociatedTeamsDto,
  type AssociateTeamToSessionResultDto,
} from './definitions'
import { verifySession } from './dal'

const SESSION_OPERATIONS_SERVICE_URL = process.env.SESSION_OPERATIONS_SERVICE_URL!

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

export async function getSessionAssociatedTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}/teams`,
    {
      headers: getIdentityHeaders(session),
      cache: 'no-store',
    },
  )

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('session_not_found')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `getSessionAssociatedTeams failed with status ${response.status}`,
    )
  }

  return response.json()
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}/teams`,
    {
      method: 'POST',
      headers: {
        ...getIdentityHeaders(session),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ referenceTeamId }),
    },
  )

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (response.status === 400) {
    throw new Error('team_inactive')
  }
  if (response.status === 409) {
    throw new Error('duplicate_association')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `associateTeamToSession failed with status ${response.status}`,
    )
  }

  return response.json()
}
```

**Gate**
- `pnpm build` passes with no type errors.
- `SESSION_OPERATIONS_SERVICE_URL` resolves at runtime; a missing env var throws at first
  call, not at import time (acceptable for a BFF module).

---

### Phase 3 — Server Actions for session-operations

**Scope**
- Create `app/actions/session-operations.ts` wrapping the two lib functions.
- Both actions require Operator or Administrator role; Participant callers receive
  `Error('Forbidden')` before reaching the backend.

**`app/actions/session-operations.ts`**
```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import {
  getSessionAssociatedTeams as getSessionAssociatedTeamsLib,
  associateTeamToSession as associateTeamToSessionLib,
} from '@/app/lib/session-operations'
import { revalidatePath } from 'next/cache'
import type {
  SessionAssociatedTeamsDto,
  AssociateTeamToSessionResultDto,
} from '@/app/lib/definitions'

export async function getSessionTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return getSessionAssociatedTeamsLib(liveSessionId)
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  const result = await associateTeamToSessionLib(liveSessionId, referenceTeamId)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes with no type errors.
- Calling `getSessionTeams` from a Participant session throws `'Forbidden'` without
  reaching the lib layer.

---

### Phase 4 — `SessionsPanel` component and dashboard integration

**Scope**
- Create `app/dashboard/SessionsPanel.tsx`.
- Add a `sessions` nav item to the `navigation` array in `DashboardClient.tsx`.
- Wire `activeNav === 'sessions'` to render `<SessionsPanel role={role} />`.
- Update the `visibleNavigation` filter operator clause to keep `sessions` visible for
  operators (it already is visible by default; the clause only needs to ensure it is not
  inadvertently excluded).

#### `app/dashboard/SessionsPanel.tsx`

```ts
'use client'

import { useState, useTransition } from 'react'
import { getSessionTeams, associateTeamToSession } from '@/app/actions/session-operations'
import { getTeamsPage } from '@/app/actions/teams'
import type {
  AssociatedSessionTeamDto,
  PagedResult,
  TeamDto,
} from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'

type SessionPanelView = 'lookup' | 'teams'
```

**State**
```ts
const [view, setView] = useState<SessionPanelView>('lookup')
const [sessionIdInput, setSessionIdInput] = useState<string>('')
const [liveSessionId, setLiveSessionId] = useState<string | null>(null)
const [associatedTeams, setAssociatedTeams] = useState<AssociatedSessionTeamDto[]>([])
const [availableTeams, setAvailableTeams] = useState<PagedResult<TeamDto> | null>(null)
const [lookupError, setLookupError] = useState<string | null>(null)
const [assignError, setAssignError] = useState<string | null>(null)
const [isPending, startTransition] = useTransition()
const [isAssigning, startAssignTransition] = useTransition()
```

**Session lookup form (view === 'lookup')**

The operator enters a session GUID and submits. On submit, both fetches run in parallel
via `Promise.all`. If `getSessionTeams` throws `session_not_found`, the form shows an
inline error and stays in lookup view.

```tsx
<section
  className={styles.panel}
  aria-labelledby="sessions-lookup-title"
  data-testid="sessions-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <h2 id="sessions-lookup-title">Session team setup</h2>
      <div className={styles.panelMeta}>
        Enter the session ID to load and manage team associations.
      </div>
    </div>
  </div>

  <form
    data-testid="session-lookup-form"
    onSubmit={(e) => {
      e.preventDefault()
      handleSessionLookup(sessionIdInput.trim())
    }}
  >
    <div className={styles.formGroup}>
      <label className={styles.fieldLabel} htmlFor="session-id-input">
        Session ID (UUID)
      </label>
      <input
        className={styles.textInput}
        data-testid="session-id-input"
        disabled={isPending}
        id="session-id-input"
        onChange={(e) => setSessionIdInput(e.target.value)}
        placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
        type="text"
        value={sessionIdInput}
      />
    </div>

    {lookupError && (
      <div className={styles.chip} data-testid="sessions-lookup-error" data-tone="critical">
        {lookupError}
      </div>
    )}

    <div className={styles.formActions}>
      <button
        className={styles.primaryButton}
        data-testid="session-lookup-submit"
        disabled={isPending || !sessionIdInput.trim()}
        type="submit"
      >
        {isPending ? 'Loading…' : 'Load session'}
      </button>
    </div>
  </form>
</section>
```

**`handleSessionLookup` function**
```ts
function handleSessionLookup(id: string) {
  startTransition(async () => {
    setLookupError(null)
    try {
      const [sessionData, teamsData] = await Promise.all([
        getSessionTeams(id),
        getTeamsPage(1, 100),  // load up to 100 active teams for the picker
      ])
      setAssociatedTeams(sessionData.teams)
      setAvailableTeams(teamsData)
      setLiveSessionId(id)
      setView('teams')
    } catch (err) {
      if (err instanceof Error && err.message === 'session_not_found') {
        setLookupError('Session not found. Check the session ID and try again.')
      } else {
        setLookupError('Failed to load session. Please try again.')
      }
    }
  })
}
```

**Teams view (view === 'teams')**

Two sections: already-associated teams (read-only table) and the team picker for adding
more. The picker list is `availableTeams.items` filtered to active teams not yet
associated.

```tsx
<section
  className={styles.panel}
  aria-labelledby="sessions-teams-title"
  data-testid="sessions-teams-panel"
>
  <div className={styles.panelHeader}>
    <div>
      <h2 id="sessions-teams-title">Session teams</h2>
      <div className={styles.panelMeta}>
        Session: <code>{liveSessionId}</code>
        {' · '}{associatedTeams.length} team{associatedTeams.length !== 1 ? 's' : ''} associated
      </div>
    </div>
    <button
      className={styles.inlineButton}
      data-testid="sessions-back-btn"
      disabled={isPending || isAssigning}
      onClick={handleBack}
      type="button"
    >
      ← Change session
    </button>
  </div>

  {/* Already-associated teams */}
  <div className={styles.subsectionHeader}>
    <h3>Associated teams</h3>
  </div>

  {associatedTeams.length === 0 ? (
    <p className={styles.mutedText} data-testid="no-associated-teams">
      No teams associated yet.
    </p>
  ) : (
    <table className={styles.table} data-testid="associated-teams-table">
      <thead>
        <tr>
          <th>Team name</th>
          <th>Code</th>
          <th>Join status</th>
        </tr>
      </thead>
      <tbody>
        {associatedTeams.map((team) => (
          <tr key={team.runtimeTeamId} data-testid={`associated-team-${team.referenceTeamId}`}>
            <td>{team.displayName}</td>
            <td>{team.teamCode}</td>
            <td>
              <span className={styles.chip}>{team.joinStatus}</span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )}

  {/* Team picker */}
  <div className={styles.subsectionHeader}>
    <h3>Assign a team</h3>
  </div>

  {assignError && (
    <div className={styles.chip} data-testid="sessions-assign-error" data-tone="critical">
      {assignError}
    </div>
  )}

  {availableTeams && (() => {
    const assignableTeams = availableTeams.items.filter(
      (t) => t.isActive && !associatedTeams.some((a) => a.referenceTeamId === t.teamId),
    )
    return assignableTeams.length === 0 ? (
      <p className={styles.mutedText} data-testid="no-assignable-teams">
        All active teams have been associated with this session.
      </p>
    ) : (
      <table className={styles.table} data-testid="assignable-teams-table">
        <thead>
          <tr>
            <th>Team name</th>
            <th>Code</th>
            <th>Status</th>
            <th>Action</th>
          </tr>
        </thead>
        <tbody>
          {assignableTeams.map((team) => (
            <tr key={team.teamId} data-testid={`assignable-team-${team.teamId}`}>
              <td>{team.displayName}</td>
              <td>{team.teamCode}</td>
              <td>
                <span className={styles.chip} data-tone="success">Active</span>
              </td>
              <td>
                <button
                  className={styles.inlineButton}
                  data-testid={`assign-team-btn-${team.teamId}`}
                  disabled={isAssigning || isPending}
                  onClick={() => handleAssign(team.teamId)}
                  type="button"
                >
                  Assign
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    )
  })()}
</section>
```

**`handleAssign` function**
```ts
function handleAssign(referenceTeamId: string) {
  if (!liveSessionId) return
  startAssignTransition(async () => {
    setAssignError(null)
    try {
      const result = await associateTeamToSession(liveSessionId, referenceTeamId)
      setAssociatedTeams((prev) => [
        ...prev,
        {
          runtimeTeamId: result.runtimeTeamId,
          referenceTeamId: result.referenceTeamId,
          displayName: result.displayName,
          teamCode: result.teamCode,
          joinStatus: 'Pending',
        },
      ])
    } catch (err) {
      if (!(err instanceof Error)) {
        setAssignError('Assignment failed. Please try again.')
        return
      }
      switch (err.message) {
        case 'team_not_found':
          setAssignError('Team not found in the catalog.')
          break
        case 'team_inactive':
          setAssignError('This team is inactive and cannot be assigned to a session.')
          break
        case 'duplicate_association':
          setAssignError('This team is already associated with this session.')
          break
        default:
          setAssignError('Assignment failed. Please try again.')
      }
    }
  })
}
```

**`handleBack` function**
```ts
function handleBack() {
  setView('lookup')
  setLiveSessionId(null)
  setAssociatedTeams([])
  setAvailableTeams(null)
  setAssignError(null)
  setLookupError(null)
}
```

#### `DashboardClient.tsx` changes

**`navigation` array — add `sessions` item**
```ts
// Add after 'trivias':
{ key: 'sessions', label: 'Sessions', icon: '⊙' },
```

**Import `SessionsPanel`**
```ts
import { SessionsPanel } from './SessionsPanel'
```

**`visibleNavigation` filter — no change needed** — the existing participant filter
(`if (role === 'participant') return item.key === 'overview'`) already excludes
`sessions` for participants. The operator filter (`item.key !== 'missions' && item.key !== 'trivias'`)
does not exclude `sessions`. No clause change is required.

**Rendering branch — add before the `role === 'operator'` live session block**

Locate the block:
```tsx
) : activeNav === 'missions' ? (
  <MissionsPanel role={role} />
```

Add after it:
```tsx
) : activeNav === 'sessions' ? (
  <SessionsPanel role={role} />
```

**`data-testid` additions required**
- `data-testid="sessions-panel"` — outer section in lookup view
- `data-testid="session-id-input"` — UUID text input
- `data-testid="session-lookup-submit"` — lookup form submit button
- `data-testid="session-lookup-form"` — the form element
- `data-testid="sessions-lookup-error"` — inline lookup error chip
- `data-testid="sessions-teams-panel"` — outer section in teams view
- `data-testid="sessions-back-btn"` — "Change session" back button
- `data-testid="no-associated-teams"` — empty-state paragraph
- `data-testid="associated-teams-table"` — table of associated teams
- `data-testid="associated-team-{referenceTeamId}"` — each associated-team row
- `data-testid="no-assignable-teams"` — empty-state paragraph when all assigned
- `data-testid="assignable-teams-table"` — table of available teams
- `data-testid="assignable-team-{teamId}"` — each assignable-team row
- `data-testid="assign-team-btn-{teamId}"` — assign button per row
- `data-testid="sessions-assign-error"` — assignment error chip

**CSS — no new classes needed.** Reuse `styles.panel`, `styles.panelHeader`,
`styles.panelMeta`, `styles.table`, `styles.chip`, `styles.inlineButton`,
`styles.primaryButton`, `styles.formGroup`, `styles.fieldLabel`, `styles.textInput`,
`styles.formActions`, `styles.subsectionHeader`, `styles.mutedText`. If `styles.textInput`
or `styles.formGroup` do not exist in `dashboard.module.css`, add them following the
visual style of the existing `styles.inlineSelect` pattern.

**Gate**
- `pnpm build` passes with no type errors.
- Operator navigates to "Sessions" in the dashboard nav and sees the lookup form.
- Entering a valid session UUID loads both the associated-teams section and the assignable
  teams picker.
- Clicking "Assign" on a team row adds the team to the associated list optimistically and
  removes it from the picker.
- Clicking "Change session" returns to the lookup form.
- Participant never sees the "Sessions" nav item.

---

### Phase 5 — E2E tests

**Scope**
- Add `tests/e2e/session-teams.spec.ts` covering all HU-18 acceptance criteria.
- Regression check: the `teams.spec.ts` and existing nav flows must still pass.

**`tests/e2e/session-teams.spec.ts`**

```ts
import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('operator sees Sessions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('admin sees Sessions nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('participant does not see Sessions nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
})

// --- Session lookup form ---

test('operator sees lookup form on entering Sessions panel', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-id-input"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-lookup-submit"]')).toBeVisible()
})

test('submit button is disabled when input is empty', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-lookup-submit"]')).toBeDisabled()
})

test('invalid session UUID shows lookup error', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await page.fill('[data-testid="session-id-input"]', '00000000-0000-0000-0000-000000000000')
  await page.click('[data-testid="session-lookup-submit"]')
  await expect(page.locator('[data-testid="sessions-lookup-error"]')).toBeVisible()
  // form stays in lookup view — teams panel not shown
  await expect(page.locator('[data-testid="sessions-teams-panel"]')).toHaveCount(0)
})

// --- Teams view ---

test('valid session UUID transitions to teams view', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  // Requires a live backend with a seeded scheduled session UUID
  const sessionId = process.env.TEST_SESSION_ID ?? ''
  test.skip(!sessionId, 'TEST_SESSION_ID not set — skipping live backend test')
  await page.fill('[data-testid="session-id-input"]', sessionId)
  await page.click('[data-testid="session-lookup-submit"]')
  await expect(page.locator('[data-testid="sessions-teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="sessions-back-btn"]')).toBeVisible()
})

test('back button returns to lookup form', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const sessionId = process.env.TEST_SESSION_ID ?? ''
  test.skip(!sessionId, 'TEST_SESSION_ID not set — skipping live backend test')
  await page.fill('[data-testid="session-id-input"]', sessionId)
  await page.click('[data-testid="session-lookup-submit"]')
  await page.click('[data-testid="sessions-back-btn"]')
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="sessions-teams-panel"]')).toHaveCount(0)
})

test('assigning a team moves it from picker to associated list', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const sessionId = process.env.TEST_SESSION_ID ?? ''
  test.skip(!sessionId, 'TEST_SESSION_ID not set — skipping live backend test')
  await page.fill('[data-testid="session-id-input"]', sessionId)
  await page.click('[data-testid="session-lookup-submit"]')
  const firstAssignBtn = page.locator('[data-testid^="assign-team-btn-"]').first()
  await expect(firstAssignBtn).toBeVisible()
  await firstAssignBtn.click()
  await expect(page.locator('[data-testid="sessions-assign-error"]')).toHaveCount(0)
  // The assigned team row should now appear in the associated-teams table
  await expect(page.locator('[data-testid="associated-teams-table"]')).toBeVisible()
})

// --- Regression ---

test('Teams nav panel still works for operator after sessions nav item is added', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
})

test('Missions nav panel still works for admin after sessions nav item is added', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
})
```

**Note on live-backend tests:** Tests that require a real scheduled session UUID
(`TEST_SESSION_ID`) are guarded with `test.skip`. The first three tests (nav visibility,
lookup form rendering, submit-button disabled state) run without a live backend because
they do not submit the form. The live-backend tests are integration-level and require
the backend running locally with a seeded `Scheduled` session.

**Gate**
- `pnpm exec playwright test tests/e2e/session-teams.spec.ts` passes the six
  non-backend-dependent tests.
- `pnpm exec playwright test tests/e2e/teams.spec.ts` passes unmodified.
- `pnpm exec playwright test tests/e2e/auth.spec.ts` passes unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — session-operations DTO types (HU-18)
feat(frontend): phase 2 — session-operations api client (HU-18)
feat(frontend): phase 3 — session-operations server actions (HU-18)
feat(frontend): phase 4 — sessions panel + dashboard nav wiring (HU-18)
feat(frontend): phase 5 — e2e tests for session team association (HU-18)

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

Final squashed commit message (gate for the whole branch):

```
feat(frontend): asociacion de equipos a sesiones - HU-18

Ref: HU-18
Ref: DES-25
Ref: DES-70
```

---

## Out of Scope

- **Session listing (`GET /api/sessions`).** No such endpoint is part of the verified
  contract for HU-18. A future HU that adds session scheduling to the frontend will
  introduce this listing. The UUID input form is an explicit MVP trade-off.
- **Session creation.** Scheduling a new session (POST /api/sessions or equivalent) is
  not part of HU-18's backend contract. The operator is assumed to receive the
  `liveSessionId` from the administrator who created the session.
- **Team removal from session.** The verified contract only exposes POST (associate) and
  GET (list). There is no DELETE or PATCH for removing a team from a session. Removing an
  association is not in scope.
- **Pagination on the team picker.** The plan loads `pageSize=100` teams in a single call.
  Pagination inside the picker adds complexity not warranted by the HU-18 scope; the
  identity service's team list is small enough for a single page.
- **Real-time updates via SignalR.** The session-operations service exposes a SignalR hub
  (`/hubs/{**catch-all}` in the gateway), but HU-18 does not cover live session events.
  The associated-teams list is loaded once on lookup and updated optimistically on assign.
- **Any backend changes.**
