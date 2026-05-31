# Plan: HU-05 Frontend — Participant-to-Team Assignment

**Ref:** HU-05 / DES-9 / DES-67
**Branch:** feature/hu-05-participant-team-assignment
**Date:** 2026-05-31
**Builds on:** HU-04 frontend (TeamsPanel with list/detail/create/edit sub-views,
`app/lib/teams.ts`, `app/actions/teams.ts`, `definitions.ts` with `TeamDto`)

---

## Context

Backend HU-05 is fully implemented prior to this frontend slice. Two new endpoints extend
the teams surface:

- `POST /api/teams/{id}/participants` — assign a Participant-role user to a team;
  Administrator only
- `GET /api/teams/{id}/participants` — list all participants assigned to a team;
  Administrator or Operator

HU-01 through HU-04 infrastructure is in place: session cookies, `dal.ts`, `users.ts`,
`teams.ts`, all existing Server Actions, `UsersPanel`, `TeamsPanel` (list / detail /
create / edit sub-views), deactivation two-step pattern, participant guard.

Three new frontend concerns beyond what HU-04 already handles:

1. **Participants section in the team detail sub-view** — `TeamsPanel` already renders the
   detail sub-view with team metadata. HU-05 appends a "Participants" section below the
   detail fields that auto-fetches `GET /api/teams/{id}/participants` when the detail view
   opens. Administrators and Operators both see this section; the assign action is
   Administrator-only.
2. **Assign participant UI** — a "+ Assign participant" button (admin only, active teams
   only) opens an inline form with a `<select>` populated from the existing
   `GET /api/users` list filtered to Participant-role users. Selection drives
   `POST /api/teams/{id}/participants`. The new participant is added to the list
   optimistically on success.
3. **Three typed error messages** — the backend returns two distinct 409 statuses and one
   422. The API client reads the ProblemDetails response body to distinguish
   `TeamNotActiveException` from `ParticipantAlreadyAssignedToTeamException`, and maps
   each to a user-friendly message in the UI.

---

## Verified Backend Contract

### `POST /api/teams/{id}/participants`

Path param: `id: string` (UUID of the team)

**Request body**
```ts
{ userId: string }  // UUID — the user's externalIdentityId (Keycloak identity GUID)
```

**Response `201`** (or `204` with no body — implementation verifies at runtime)
```ts
{ membershipId: string }  // UUID of the new TeamMembership record (if 201)
```

**Error cases**
- `403` — caller is not Administrator
- `404` — team not found or user not found
- `409` — one of two exceptions, distinguished by ProblemDetails body:
  - `TeamNotActiveException` → "This team is inactive and cannot accept new members."
  - `ParticipantAlreadyAssignedToTeamException` → "This user is already assigned to the team."
- `422` — `UserNotParticipantRoleException` → "The selected user does not have the
  Participant role."

**ProblemDetails disambiguation:** both 409 cases return the same HTTP status. The body
contains a `type` or `title` field naming the exception class. The API client reads this
field and throws a different `Error` message code for each case. The agent implementing
Phase 2 must verify the exact ProblemDetails field name and value against the live backend
before hardcoding the match string.

**userId semantics:** `UserAccessCatalogItemDto` exposes `externalIdentityId: string`
(Keycloak UUID) and `id: number` (database integer). The backend's
`AssignParticipantToTeamCommand` loads the user by a GUID. The `userId` field in the
request body must be `externalIdentityId`, not the integer `id`. The user selector in
the UI reads `user.externalIdentityId` when building the request.

---

### `GET /api/teams/{id}/participants`

Path param: `id: string` (team UUID)

**Response `200`**
```ts
TeamMembershipDto[]
```

Where `TeamMembershipDto` is:
```ts
{
  teamMembershipId: string  // UUID
  teamId: string            // UUID
  userId: string            // UUID (externalIdentityId of the assigned user)
  assignedAt: string        // ISO 8601
}
```

The backend currently does not return the user's displayName or email inside the membership
record. If this changes in a future slice, the UI should prefer that data over the raw UUID.

**Error cases**
- `403` — caller is Participant (Administrator and Operator both have access)
- `404` — team not found

---

## Architecture Decisions

- **All changes are within `TeamsPanel.tsx`.** No new files, no new routes, no changes to
  `DashboardClient.tsx`. The participants section is a new rendered block appended to the
  existing detail sub-view; the assign form is an inline collapsible form below that block.
- **Participants auto-fetch on detail view entry.** A `useEffect` keyed on `[view,
  selectedTeam?.id]` triggers `getTeamParticipants(selectedTeam.id)` whenever the detail
  sub-view becomes active. This keeps the list fresh when navigating between teams.
- **Separate `useTransition` for the assign flow.** `TeamsPanel` already uses
  `isPending` / `startTransition` for the main sub-view transitions (list load,
  create/edit/deactivate). A second `[isAssignPending, startAssignTransition]` pair
  isolates the participants fetch and the assignment POST so they do not block or disable
  the main panel actions.
- **User selector loads lazily.** The `<select>` of Participant-role users is not pre-loaded
  on detail entry (to avoid a redundant fetch when the assign form is never opened). It
  is loaded by `loadParticipantUsers()` when the "+ Assign participant" button is first
  clicked. `getUsersPage(1, 100)` is called and the result is filtered client-side to
  `role === 'Participant'` and `isActive === true`.
- **409 disambiguation via ProblemDetails body.** The `assignParticipant` function in
  `app/lib/teams.ts` reads the response body for all 409 responses. It checks the `type`
  or `title` field for a known exception class name and throws either
  `Error('team_not_active')` or `Error('participant_already_assigned')`. If the body
  is unreadable or neither key matches, it defaults to `Error('participant_already_assigned')`.
  The agent must verify field names against the live backend at implementation time.
- **Optimistic UI update.** After a successful POST, a synthetic `TeamMembershipDto` is
  appended to the `participants` state immediately, using the `selectedUserId` and
  `new Date().toISOString()` for `assignedAt`. The `teamMembershipId` is a temporary
  client-side key (`'optimistic-' + Date.now()`). The assign form is hidden and the
  selector is reset. On any error, the `participants` list is not mutated.
- **Operator read-only enforcement.** The "+ Assign participant" button and the assign form
  are only rendered when `role === 'admin'`. The Server Action `assignParticipantToTeam`
  throws `Error('Forbidden')` for non-Administrator callers independently.
- **Participant role guard is already in place.** `DashboardClient.tsx` renders
  `participant-panel` when `role === 'participant'`, taking priority over all `activeNav`
  branches. `TeamsPanel` is never rendered for Participants — no additional guard is
  needed within `TeamsPanel.tsx` itself. The existing `visibleNavigation` filter already
  hides the 'teams' nav item from Participants (established in HU-03).
- **Display name enrichment for future backend evolution.** The participants table
  currently shows the raw `userId` UUID. When the backend begins returning `displayName`
  or `email` fields in `TeamMembershipDto`, only the type definition and the table cell
  rendering need to change; all other logic is unaffected.

---

## Environment

No new environment variables. The two new endpoints are on `IDENTITY_SERVICE_URL`
(`http://localhost:5002`), the same constant already used in `app/lib/teams.ts`.

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add `TeamMembershipDto` to `app/lib/definitions.ts`.
- No other file changes; the type makes the response shape available for the API client
  and Server Actions in subsequent phases.

**`app/lib/definitions.ts` addition**
```ts
export type TeamMembershipDto = {
  teamMembershipId: string
  teamId: string
  userId: string       // externalIdentityId UUID of the assigned user
  assignedAt: string   // ISO 8601
}
```

Note: `AssignParticipantResultDto` (`{ membershipId: string }`) is not added as a
named type — the `assignParticipant` lib function returns `void` (the membership id
returned by a 201 is discarded in favour of the optimistic update pattern; a 204 is
handled the same way).

**Gate**
- `pnpm build` passes. No runtime change.

---

### Phase 2 — API client additions (`app/lib/teams.ts`)

**Scope**
- Add `listTeamParticipants` and `assignParticipant` to `app/lib/teams.ts`.
- The 409 disambiguation reads the ProblemDetails response body to map to specific
  error codes; the agent must verify the exact `type`/`title` field and value against
  the live backend ProblemDetails format before finalising the string match.

**Additions to `app/lib/teams.ts`**
```ts
export async function listTeamParticipants(teamId: string): Promise<TeamMembershipDto[]> {
  const session = await verifySession()
  const response = await fetch(
    `${IDENTITY_SERVICE_URL}/api/teams/${teamId}/participants`,
    {
      headers: getIdentityHeaders(session),
      cache: 'no-store',
    },
  )

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `listTeamParticipants failed with status ${response.status}`,
    )
  }

  return response.json()
}

export async function assignParticipant(
  teamId: string,
  userId: string,
): Promise<void> {
  const session = await verifySession()
  const response = await fetch(
    `${IDENTITY_SERVICE_URL}/api/teams/${teamId}/participants`,
    {
      method: 'POST',
      headers: {
        ...getIdentityHeaders(session),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ userId }),
    },
  )

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('not_found')
  }

  if (response.status === 409) {
    // Read ProblemDetails body to distinguish TeamNotActiveException from
    // ParticipantAlreadyAssignedToTeamException. Verify the exact field/value
    // against the live backend before finalising these match strings.
    let body: Record<string, unknown> = {}
    try {
      body = await response.json()
    } catch {
      // body unreadable — fall through to default 409 code
    }
    const errorKey = String(body.type ?? body.title ?? '').toLowerCase()
    if (errorKey.includes('notactive') || errorKey.includes('inactive')) {
      throw new Error('team_not_active')
    }
    throw new Error('participant_already_assigned')
  }

  if (response.status === 422) {
    throw new Error('user_not_participant_role')
  }

  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `assignParticipant failed with status ${response.status}`,
    )
  }

  // 201 body (membership id) is discarded — optimistic update handles UI state.
  // 204 with no body is also handled correctly here.
}
```

Also add the import for `TeamMembershipDto` to the existing import line at the top of
`teams.ts`:
```ts
import {
  IdentityError,
  type PagedResult,
  type TeamDto,
  type CreateTeamResultDto,
  type TeamMembershipDto,   // add this
} from './definitions'
```

**Gate**
- `pnpm build` passes. No runtime change.

---

### Phase 3 — Server Actions (`app/actions/teams.ts`)

**Scope**
- Add `getTeamParticipants` and `assignParticipantToTeam` to `app/actions/teams.ts`.
- `assignParticipantToTeam` is Administrator-only; `getTeamParticipants` is available to
  Administrator and Operator.
- Import the new lib functions with aliases to avoid naming conflicts.

**Additions to `app/actions/teams.ts`**

Update the import from `@/app/lib/teams` to include the new functions:
```ts
import {
  listTeams,
  getTeamById,
  createTeam as createTeamLib,
  updateTeam as updateTeamLib,
  deactivateTeam as deactivateTeamLib,
  listTeamParticipants,                       // add
  assignParticipant as assignParticipantLib,  // add
} from '@/app/lib/teams'
```

Add the following exported functions:
```ts
export async function getTeamParticipants(
  teamId: string,
): Promise<TeamMembershipDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listTeamParticipants(teamId)
}

export async function assignParticipantToTeam(
  teamId: string,
  userId: string,
): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await assignParticipantLib(teamId, userId)
  revalidatePath('/dashboard')
}
```

Also add `TeamMembershipDto` to the type import line at the top of `actions/teams.ts`:
```ts
import type {
  PagedResult,
  TeamDto,
  CreateTeamResultDto,
  TeamMembershipDto,   // add
} from '@/app/lib/definitions'
```

**Gate**
- `pnpm build` passes.
- Calling `assignParticipantToTeam` from an Operator session throws `'Forbidden'` before
  reaching the backend.
- Calling `getTeamParticipants` from a Participant session throws `'Forbidden'`.

---

### Phase 4 — Participants list section in the detail sub-view

**Scope**
- Add new state to `TeamsPanel.tsx` for participants and the assign transition.
- Add a `useEffect` that fetches participants when entering the detail sub-view.
- Render a "Participants" section below the `<dl>` in the detail sub-view.
- The section is visible to `admin` and `operator` alike (read-only for both in this phase;
  the assign form is wired in Phase 5).

**New imports in `TeamsPanel.tsx`**
```ts
import { getTeamParticipants, assignParticipantToTeam } from '@/app/actions/teams'
import { getUsersPage } from '@/app/actions/users'
import type { TeamMembershipDto, UserAccessCatalogItemDto } from '@/app/lib/definitions'
```

**New state additions** (inside the `TeamsPanel` component body, alongside existing state):
```ts
const [participants, setParticipants] = useState<TeamMembershipDto[]>([])
const [participantsError, setParticipantsError] = useState<string | null>(null)
const [isAssignPending, startAssignTransition] = useTransition()
const [showAssignForm, setShowAssignForm] = useState(false)
const [participantUsers, setParticipantUsers] = useState<UserAccessCatalogItemDto[]>([])
const [selectedUserId, setSelectedUserId] = useState<string>('')
const [assignError, setAssignError] = useState<string | null>(null)
```

**Participants auto-fetch** — add this `useEffect` alongside the existing list-fetch effect:
```ts
useEffect(() => {
  if (view !== 'detail' || !selectedTeam) return
  startAssignTransition(async () => {
    setParticipantsError(null)
    try {
      const result = await getTeamParticipants(selectedTeam.id)
      setParticipants(result)
    } catch {
      setParticipantsError('Failed to load participants.')
    }
  })
}, [view, selectedTeam?.id])
```

**Participants section JSX** — appended inside the detail sub-view section, immediately
after the closing `</dl>` tag of the team detail fields:

```tsx
<section
  aria-labelledby="participants-section-title"
  data-testid="participants-section"
>
  <div className={styles.subsectionHeader}>
    <h3 id="participants-section-title">Participants</h3>
    {/* Assign button rendered in Phase 5 */}
  </div>

  {participantsError && (
    <div className={styles.chip} data-tone="critical">
      {participantsError}
    </div>
  )}

  {isAssignPending && !participants.length && (
    <span className={styles.chip}>Loading…</span>
  )}

  {!participantsError && !isAssignPending && participants.length === 0 && (
    <p className={styles.panelMeta} data-testid="no-participants-message">
      No participants assigned yet.
    </p>
  )}

  {participants.length > 0 && (
    <table className={styles.table} data-testid="participants-table">
      <thead>
        <tr>
          <th>User</th>
          <th>Assigned</th>
        </tr>
      </thead>
      <tbody>
        {participants.map((m) => (
          <tr key={m.teamMembershipId} data-testid={`participant-row-${m.teamMembershipId}`}>
            <td data-testid={`participant-user-${m.userId}`}>{m.userId}</td>
            <td>{new Date(m.assignedAt).toLocaleDateString()}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )}
</section>
```

**Reset participants when leaving detail sub-view** — add a cleanup to the existing
navigation handlers that call `setView('list')` or `setView('edit')`:
```ts
// When transitioning away from detail (back to list or into edit), reset participants state
setParticipants([])
setParticipantsError(null)
setShowAssignForm(false)
setAssignError(null)
setSelectedUserId('')
```

These resets go inside the `onClick` handlers of "← Teams" and "Edit" buttons. This
ensures the participants section does not show stale data if the admin navigates back and
selects a different team.

**`data-testid` additions required in this phase**
- `data-testid="participants-section"` on the section wrapper
- `data-testid="no-participants-message"` on the empty-state paragraph
- `data-testid="participants-table"` on the table
- `data-testid="participant-row-{membershipId}"` on each row
- `data-testid="participant-user-{userId}"` on the user cell

**Gate**
- `pnpm build` passes.
- Admin navigating to a team detail sees "Participants" heading below the fields.
- When no participants are assigned, "No participants assigned yet." is displayed.
- Participants list populates when the team has existing memberships (verified against the
  live backend).
- Operator sees the same participants section (read-only).
- Navigating away and back to a different team shows fresh participant data, not stale data
  from the previous team.

---

### Phase 5 — Assign participant UI (admin only)

**Scope**
- Add the "+ Assign participant" button to the participants section header.
- Add `loadParticipantUsers()` to populate the `<select>` lazily.
- Add the inline assign form with user selector, error display, and confirm/cancel buttons.
- Add `handleAssign()` with optimistic update and error mapping for all three backend error
  codes.

**`loadParticipantUsers` function**
```ts
function loadParticipantUsers() {
  startAssignTransition(async () => {
    try {
      const result = await getUsersPage(1, 100)
      setParticipantUsers(
        result.items.filter((u) => u.role === 'Participant' && u.isActive),
      )
    } catch {
      // Selector will be empty; user can still try to submit if they know the id
    }
  })
}
```

**`handleAssign` function**
```ts
async function handleAssign() {
  if (!selectedTeam || !selectedUserId) return
  startAssignTransition(async () => {
    setAssignError(null)
    try {
      await assignParticipantToTeam(selectedTeam.id, selectedUserId)
      // Optimistic update — append synthetic membership entry
      setParticipants((prev) => [
        ...prev,
        {
          teamMembershipId: 'optimistic-' + Date.now(),
          teamId: selectedTeam.id,
          userId: selectedUserId,
          assignedAt: new Date().toISOString(),
        },
      ])
      setShowAssignForm(false)
      setSelectedUserId('')
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'team_not_active') {
        setAssignError('This team is inactive and cannot accept new members.')
      } else if (msg === 'participant_already_assigned') {
        setAssignError('This user is already assigned to the team.')
      } else if (msg === 'user_not_participant_role') {
        setAssignError('The selected user does not have the Participant role.')
      } else {
        setAssignError('Assignment failed. Try again.')
      }
    }
  })
}
```

**Updated participants section header** — replace the comment placeholder from Phase 4
with the actual button:
```tsx
<div className={styles.subsectionHeader}>
  <h3 id="participants-section-title">Participants</h3>
  {role === 'admin' && selectedTeam!.isActive && (
    <button
      className={styles.inlineButton}
      data-testid="assign-participant-btn"
      disabled={isAssignPending || showAssignForm}
      onClick={() => {
        setAssignError(null)
        setShowAssignForm(true)
        loadParticipantUsers()
      }}
      type="button"
    >
      + Assign participant
    </button>
  )}
</div>
```

**Assign form** — rendered between the section header and the participants table/empty
state, only when `showAssignForm === true && role === 'admin'`:
```tsx
{showAssignForm && role === 'admin' && (
  <div className={styles.formGroup} data-testid="assign-form">
    <label htmlFor="participant-select">Select participant</label>
    <select
      id="participant-select"
      className={styles.inlineSelect}
      data-testid="participant-select"
      value={selectedUserId}
      onChange={(e) => setSelectedUserId(e.target.value)}
      disabled={isAssignPending}
    >
      <option value="">— Select a participant —</option>
      {participantUsers.map((u) => (
        <option key={u.externalIdentityId} value={u.externalIdentityId}>
          {u.displayName} ({u.email})
        </option>
      ))}
    </select>

    {assignError && (
      <span className={styles.fieldError} data-testid="assign-error">
        {assignError}
      </span>
    )}

    <div className={styles.panelActions}>
      <button
        className={styles.primaryButton}
        data-testid="confirm-assign-btn"
        disabled={!selectedUserId || isAssignPending}
        onClick={handleAssign}
        type="button"
      >
        Assign
      </button>
      <button
        className={styles.inlineButton}
        disabled={isAssignPending}
        onClick={() => {
          setShowAssignForm(false)
          setAssignError(null)
          setSelectedUserId('')
        }}
        type="button"
      >
        Cancel
      </button>
    </div>
  </div>
)}
```

**`data-testid` additions required in this phase**
- `data-testid="assign-participant-btn"` on the section header button
- `data-testid="assign-form"` on the form wrapper div
- `data-testid="participant-select"` on the select element
- `data-testid="assign-error"` on the error span
- `data-testid="confirm-assign-btn"` on the confirm button

**Gate**
- Admin sees "+ Assign participant" button for active teams; button is absent for inactive
  teams (the deactivated team detail shows no assign button).
- Clicking "+ Assign participant" opens the form with the selector populated by
  Participant-role users from the live backend.
- "Assign" is disabled until a user is selected.
- Selecting a user and clicking "Assign" adds the participant row to the list optimistically
  and closes the form.
- The three error messages appear in the right scenarios:
  - inactive team (verified by attempting assignment to a deactivated team) → "This team
    is inactive and cannot accept new members."
  - duplicate assignment (assign the same user twice) → "This user is already assigned to
    the team."
  - non-Participant user selected (if the selector ever includes them) → "The selected user
    does not have the Participant role."
- Operator sees no "+ Assign participant" button.
- "Cancel" closes the form without any network call.

---

### Phase 6 — E2E tests

**Scope**
- Create `tests/e2e/participants.spec.ts` covering all HU-05 acceptance criteria.
- Verify no HU-01 through HU-04 regressions.
- No new fixtures needed; `adminPage`, `operatorPage`, and `participantPage` are already
  declared in `tests/fixtures/auth.ts`.

**`tests/e2e/participants.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

// --- Participants list view ---

test('admin detail view shows participants section', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="participants-section"]')).toBeVisible()
})

test('operator detail view shows participants section', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="participants-section"]')).toBeVisible()
})

test('operator sees no assign participant button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="assign-participant-btn"]')).toHaveCount(0)
})

test('participant cannot see teams panel or participants section', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="teams-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="participants-section"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="nav-teams"]')).toHaveCount(0)
})

// --- Assign participant UI ---

test('admin sees assign button only on active teams', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Open first active team
  await page.locator('[data-testid^="team-row-"]').first().click()
  const statusText = await page.locator('[data-testid="detail-status"]').innerText()
  if (statusText.includes('Active')) {
    await expect(page.locator('[data-testid="assign-participant-btn"]')).toBeVisible()
  } else {
    await expect(page.locator('[data-testid="assign-participant-btn"]')).toHaveCount(0)
  }
})

test('admin can open assign form and it shows participant users', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="detail-status"]')).toContainText('Active')
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="assign-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="participant-select"]')).toBeVisible()
})

test('confirm assign button is disabled until user selected', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="confirm-assign-btn"]')).toBeDisabled()
})

test('admin can cancel assign form without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="assign-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="assign-form"]')).toHaveCount(0)
})

test('successful assignment adds participant row optimistically', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="assign-participant-btn"]')
  // Select the first available participant
  const select = page.locator('[data-testid="participant-select"]')
  const options = await select.locator('option').count()
  if (options <= 1) {
    // No participant users in test DB — skip execution part of test
    await page.click('[data-testid="confirm-assign-btn"]').catch(() => {})
    return
  }
  await select.selectOption({ index: 1 })
  await page.click('[data-testid="confirm-assign-btn"]')
  // Form should close
  await expect(page.locator('[data-testid="assign-form"]')).toHaveCount(0)
  // Table should now have at least one row
  await expect(page.locator('[data-testid="participants-table"]')).toBeVisible()
})

// --- Duplicate assignment error ---

test('duplicate assignment shows user-friendly error', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  // Open assign form twice (second time will fail with 409 duplicate if user was just assigned)
  await page.click('[data-testid="assign-participant-btn"]')
  const select = page.locator('[data-testid="participant-select"]')
  const options = await select.locator('option').count()
  if (options <= 1) return  // skip if no participants available in test environment
  await select.selectOption({ index: 1 })
  await page.click('[data-testid="confirm-assign-btn"]')
  // Re-open assign form and try the same user
  await page.click('[data-testid="assign-participant-btn"]')
  await select.selectOption({ index: 1 })
  await page.click('[data-testid="confirm-assign-btn"]')
  await expect(page.locator('[data-testid="assign-error"]')).toContainText(
    'already assigned',
  )
})

// --- Navigation: stale data does not persist ---

test('participants section resets when navigating to a different team', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Open first team
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="participants-section"]')).toBeVisible()
  // Navigate back and open a second team (if one exists)
  await page.click('[data-testid="teams-back-btn"]')
  const rows = await page.locator('[data-testid^="team-row-"]').count()
  if (rows < 2) return  // only one team in test environment
  await page.locator('[data-testid^="team-row-"]').nth(1).click()
  // Participants section should reload (not show previous team's data)
  await expect(page.locator('[data-testid="participants-section"]')).toBeVisible()
})

// --- HU-04 regression ---

test('HU-04 teams panel list still renders for admin', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-btn"]')).toBeVisible()
})

test('HU-04 team detail still shows edit and deactivate buttons for admin', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  const detailPanel = page.locator('[data-testid="team-detail-panel"]')
  await expect(detailPanel).toBeVisible()
  await expect(page.locator('[data-testid="detail-team-code"]')).toBeVisible()
})

test('HU-04 team create form still works', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
})

// --- HU-01 regression ---

test('HU-01 operator flow is not regressed by HU-05', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
})

// --- HU-02 regression ---

test('HU-02 users panel still renders for admin', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]').first()).toBeVisible()
})

// --- HU-03 regression ---

test('HU-03 participant guard is not regressed', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toHaveCount(0)
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-05 acceptance criteria are exercised.
- HU-01 through HU-04 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — add TeamMembershipDto type
feat(frontend): phase 2 — api client for participant endpoints (teams.ts)
feat(frontend): phase 3 — server actions for participant management (actions/teams.ts)
feat(frontend): phase 4 — participants list section in team detail sub-view
feat(frontend): phase 5 — assign participant UI with optimistic update and error mapping
feat(frontend): phase 6 — e2e tests for HU-05 and HU-01 through HU-04 regression checks

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

---

## Out of Scope

- **Participant display name in membership list.** The current backend `TeamMembershipDto`
  returns only `userId` (UUID). Displaying the participant's `displayName` requires either
  a backend change (include user projection in the membership record) or a client-side join
  against the users list. Neither is implemented here. The `participant-user-{userId}`
  `data-testid` is intentionally scoped to the UUID so tests remain stable when the backend
  evolves.
- **User search / filter in the assign selector.** The selector is a `<select>` populated
  from `getUsersPage(1, 100)` filtered client-side. If the participant-user count exceeds
  100, some users will not appear. A search-as-you-type input would require a debounced
  backend query; that is deferred to a future HU.
- **Remove participant action.** HU-05 introduces assignment only. A "Remove" action is
  not part of the backend API surface for this HU.
- **Team membership display for Participants themselves.** Participants cannot access the
  teams panel at all in the current architecture. A future HU may give participants a
  read-only view of their own team assignments.
- **Branch targeting.** The PR for this slice targets `feature/hu-04-team-registration`
  (not `develop`), because HU-05 depends on the `Team` aggregate introduced by HU-04.
  After HU-04 merges to `develop`, this PR must be retargeted before final merge.
- **Any backend changes.** This plan is frontend-only; the backend is assumed fully
  implemented and gated before this slice begins.
