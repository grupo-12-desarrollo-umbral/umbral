# Plan: HU-09 Frontend — Mission Management

**Ref:** HU-09
**Branch:** feature/hu-09-mission-management
**Date:** 2026-05-31
**Builds on:** HU-03 frontend (Role type, role-based visibility, DashboardClient nav filtering, TeamsPanel pattern)

---

## Context

Backend HU-09 (`mission-design-service`) is fully implemented. Five endpoints are available:

- `POST /api/missions` — create mission; `Administrator` only
- `GET /api/missions` — list all missions (summary); `Administrator` only
- `GET /api/missions/{id}` — mission detail; `Administrator` only
- `PUT /api/missions/{id}` — update mission; `Administrator` only
- `DELETE /api/missions/{id}` — deactivate mission; `Administrator` only

Four frontend concerns:

1. **Type definitions** — `MissionSummaryDto` and `MissionDto` are not in `definitions.ts` yet.
2. **API client and Server Actions** — `app/lib/missions.ts` and `app/actions/missions.ts` do not exist.
3. **MissionsPanel component** — a four-view panel (list, detail, create, edit) mirroring `TeamsPanel`.
4. **DashboardClient wiring** — mount the panel behind `activeNav === 'missions'`; gate the nav item to admins only.

---

## Verified Backend Contract

### Common headers (trust envelope)

```
X-User-Id:   {session.externalIdentityId}
X-User-Role: {session.role}
X-User-Email: {session.email}
```

The mission-design service reads only `X-User-Id` and `X-User-Role`; the email header is forwarded for consistency with the existing `getIdentityHeaders` helper and silently ignored.

### `POST /api/missions`

**Request body**
```ts
{
  name: string             // required, max 200
  description: string      // required, max 2000
  difficulty: string       // required, max 100 — free-form (e.g. "Beginner", "Advanced")
  maximumTimeMinutes: number  // required, > 0
}
```

**Response `201 Created`** — `Location: /api/missions/{id}` + body `MissionResponse`

**Error cases**
- `400` — validation failed (empty fields or `maximumTimeMinutes ≤ 0`)
- `401` — missing or invalid trust headers
- `403` — caller is not `Administrator`

---

### `GET /api/missions`

**Response `200 OK`** — `MissionSummaryResponse[]`

```ts
{
  id: number
  name: string
  description: string
  isActive: boolean              // false when activationState === "Inactive"
  activationState: string        // "Draft" | "Ready" | "Inactive"
  isSourceReady: boolean         // true only when activationState === "Ready"
}
```

**Additional verified behaviour**
- Deactivated missions are included in the catalog (`isActive: false`).
- A newly created mission has `activationState: "Draft"` and `isSourceReady: false`.

---

### `GET /api/missions/{id}`

**Response `200 OK`** — `MissionResponse`

```ts
{
  id: number
  name: string
  description: string
  difficulty: string
  maximumTimeMinutes: number
  isActive: boolean
  activationState: string
  isSourceReady: boolean
}
```

**Error cases**
- `400` — `id ≤ 0` (invalid route value, validator rejects it)
- `404` — mission not found

---

### `PUT /api/missions/{id}`

**Request body** — same shape as `POST`

**Response `200 OK`** — `MissionResponse` with updated values

**Error cases**
- `400` — validation failed
- `404` — mission not found

---

### `DELETE /api/missions/{id}`

**Response `204 No Content`**

**Error cases**
- `400` — `id ≤ 0`
- `404` — mission not found
- `500` — mission is already deactivated (`MissionAlreadyDeactivatedException` is not mapped by the exception handler and falls through to the generic 500 branch); treat as a generic deactivation error in the UI.

---

## Architecture Decisions

- **`MissionsPanel` is a standalone file** (`app/dashboard/MissionsPanel.tsx`), following the `TeamsPanel` pattern exactly. It is not inlined into `DashboardClient`.
- **Admin-only throughout.** The missions nav item is hidden from operators and participants (the `visibleNavigation` filter in `DashboardClient` already hides items for participants; missions requires a separate operator exclusion). Server actions enforce the role at the action layer before reaching the backend.
- **Four views: `list | detail | create | edit`**, managed by a `MissionPanelView` type and a `view` state variable inside `MissionsPanel`. No router navigation; the panel is entirely in-memory.
- **List view loads on mount.** `useEffect` + `useTransition` fetches the catalog. A `refreshKey` state variable increments after mutations to trigger a re-fetch, matching the `TeamsPanel` pattern.
- **Detail view shows all fields** including read-only `activationState` badge and `isSourceReady` indicator. Two action buttons: "Edit" and "Deactivate". Deactivate is hidden when `!mission.isActive`.
- **Optimistic status update on deactivate.** After a successful deactivate action, the local `selectedMission` state is updated (`isActive: false`, `activationState: 'Inactive'`) without a network round-trip. `refreshKey` increments to sync the list. On error, the prior state is restored.
- **Two-step deactivate confirm**, identical to the pattern in `UsersPanel` and `TeamsPanel`: a `confirmDeactivate` boolean shows a "Confirm" / "Cancel" button pair before the action fires.
- **Create and Edit share a form component** (`MissionForm`) defined locally in `MissionsPanel.tsx`. It receives initial values (empty for create, populated for edit) and an `onSubmit` / `onCancel` pair. This avoids duplicating the four-field form.
- **`revalidatePath('/dashboard')` after every mutation** in server actions, clearing the RSC cache.
- **`MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'`** — the mission-design service's direct development port, consistent with how `teams.ts` and `users.ts` call the identity service at `http://localhost:5002`.

---

## Environment

No new environment variables. `http://localhost:5001` is hardcoded in `app/lib/missions.ts`, matching the `IDENTITY_SERVICE_URL` convention used in the other lib files.

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add `MissionSummaryDto` and `MissionDto` to `app/lib/definitions.ts`.

**`app/lib/definitions.ts` additions**
```ts
export type MissionSummaryDto = {
  id: number
  name: string
  description: string
  isActive: boolean
  activationState: string   // "Draft" | "Ready" | "Inactive"
  isSourceReady: boolean
}

export type MissionDto = {
  id: number
  name: string
  description: string
  difficulty: string
  maximumTimeMinutes: number
  isActive: boolean
  activationState: string
  isSourceReady: boolean
}
```

**Gate**
- `pnpm build` passes. No downstream consumers exist yet so no compile errors are expected.

---

### Phase 2 — API client

**Scope**
- Create `app/lib/missions.ts` with five functions that cover the full backend contract.

**`app/lib/missions.ts`**
```ts
import 'server-only'
import { IdentityError, type MissionSummaryDto, type MissionDto } from './definitions'
import { verifySession } from './dal'

const MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'

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

export async function listMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions`, {
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
    throw new IdentityError('unknown', `listMissions failed with status ${response.status}`)
  }

  return response.json()
}

export async function getMissionById(id: number): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `getMissionById failed with status ${response.status}`)
  }

  return response.json()
}

export async function createMission(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ name, description, difficulty, maximumTimeMinutes }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createMission failed with status ${response.status}`)
  }

  return response.json()
}

export async function updateMission(
  id: number,
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    method: 'PUT',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ name, description, difficulty, maximumTimeMinutes }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `updateMission failed with status ${response.status}`)
  }

  return response.json()
}

export async function deactivateMission(id: number): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (!response.ok) {
    // Covers 500 from MissionAlreadyDeactivatedException (not mapped by the exception handler)
    throw new Error('deactivation_failed')
  }
}
```

**Gate**
- `pnpm build` passes with no type errors.

---

### Phase 3 — Server Actions

**Scope**
- Create `app/actions/missions.ts` with five server actions, all gated to `Administrator`.

**`app/actions/missions.ts`**
```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listMissions,
  getMissionById,
  createMission as createMissionLib,
  updateMission as updateMissionLib,
  deactivateMission as deactivateMissionLib,
} from '@/app/lib/missions'
import { revalidatePath } from 'next/cache'
import type { MissionSummaryDto, MissionDto } from '@/app/lib/definitions'

export async function getMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  return listMissions()
}

export async function getMission(id: number): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  return getMissionById(id)
}

export async function createMission(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await createMissionLib(name, description, difficulty, maximumTimeMinutes)
  revalidatePath('/dashboard')
  return result
}

export async function updateMission(
  id: number,
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await updateMissionLib(id, name, description, difficulty, maximumTimeMinutes)
  revalidatePath('/dashboard')
  return result
}

export async function deactivateMission(id: number): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await deactivateMissionLib(id)
  revalidatePath('/dashboard')
}
```

**Gate**
- `pnpm build` passes.
- Calling any action from a non-Administrator session throws `'Forbidden'` before reaching the backend.

---

### Phase 4 — MissionsPanel component

**Scope**
- Create `app/dashboard/MissionsPanel.tsx`.
- Four views: `list`, `detail`, `create`, `edit`.
- CSS additions to `dashboard.module.css` only if a new visual element has no existing equivalent.

**State**
```ts
type MissionPanelView = 'list' | 'detail' | 'create' | 'edit'

const [view, setView] = useState<MissionPanelView>('list')
const [selectedMission, setSelectedMission] = useState<MissionDto | null>(null)
const [listData, setListData] = useState<MissionSummaryDto[] | null>(null)
const [isPending, startTransition] = useTransition()
const [listError, setListError] = useState<string | null>(null)
const [formError, setFormError] = useState<string | null>(null)
const [deactivateError, setDeactivateError] = useState<string | null>(null)
const [confirmDeactivate, setConfirmDeactivate] = useState(false)
const [refreshKey, setRefreshKey] = useState(0)
```

**List load effect**
```ts
useEffect(() => {
  startTransition(async () => {
    setListError(null)
    try {
      const result = await getMissions()
      setListData(result)
    } catch {
      setListError('Failed to load missions.')
    }
  })
}, [refreshKey])
```

**`handleCreate` handler**
```ts
async function handleCreate(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
) {
  startTransition(async () => {
    setFormError(null)
    try {
      const created = await createMission(name, description, difficulty, maximumTimeMinutes)
      setSelectedMission(created)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_fields') {
        setFormError('Check all fields: name, description, and difficulty are required; time must be a positive number.')
      } else {
        setFormError('Failed to create mission. Try again.')
      }
    }
  })
}
```

**`handleUpdate` handler**
```ts
async function handleUpdate(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
) {
  if (!selectedMission) return
  startTransition(async () => {
    setFormError(null)
    try {
      const updated = await updateMission(selectedMission.id, name, description, difficulty, maximumTimeMinutes)
      setSelectedMission(updated)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_fields') {
        setFormError('Check all fields: name, description, and difficulty are required; time must be a positive number.')
      } else if (msg === 'mission_not_found') {
        setFormError('Mission no longer exists.')
      } else {
        setFormError('Failed to update mission. Try again.')
      }
    }
  })
}
```

**`handleDeactivate` handler**
```ts
async function handleDeactivate(id: number) {
  startTransition(async () => {
    setDeactivateError(null)
    try {
      await deactivateMission(id)
      setSelectedMission((prev) =>
        prev ? { ...prev, isActive: false, activationState: 'Inactive', isSourceReady: false } : prev
      )
      setConfirmDeactivate(false)
      setRefreshKey((k) => k + 1)
    } catch {
      setDeactivateError('Deactivation failed. Try again.')
      setConfirmDeactivate(false)
    }
  })
}
```

**`MissionForm` local component** (shared by create and edit views)
```tsx
function MissionForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: { name: string; description: string; difficulty: string; maximumTimeMinutes: number }
  isPending: boolean
  error: string | null
  onSubmit: (name: string, description: string, difficulty: string, maximumTimeMinutes: number) => void
  onCancel: () => void
}) {
  const [name, setName] = useState(initial.name)
  const [description, setDescription] = useState(initial.description)
  const [difficulty, setDifficulty] = useState(initial.difficulty)
  const [maximumTimeMinutes, setMaximumTimeMinutes] = useState(initial.maximumTimeMinutes)

  return (
    <form
      className={styles.form}
      data-testid="mission-form"
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit(name.trim(), description.trim(), difficulty.trim(), maximumTimeMinutes)
      }}
    >
      {error && <p className={styles.formError} role="alert">{error}</p>}
      <label className={styles.formLabel}>
        Name
        <input
          className={styles.formInput}
          data-testid="mission-name-input"
          disabled={isPending}
          maxLength={200}
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <label className={styles.formLabel}>
        Description
        <textarea
          className={styles.formTextarea}
          data-testid="mission-description-input"
          disabled={isPending}
          maxLength={2000}
          required
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </label>
      <label className={styles.formLabel}>
        Difficulty
        <input
          className={styles.formInput}
          data-testid="mission-difficulty-input"
          disabled={isPending}
          maxLength={100}
          required
          value={difficulty}
          onChange={(e) => setDifficulty(e.target.value)}
        />
      </label>
      <label className={styles.formLabel}>
        Maximum time (minutes)
        <input
          className={styles.formInput}
          data-testid="mission-time-input"
          disabled={isPending}
          min={1}
          required
          type="number"
          value={maximumTimeMinutes}
          onChange={(e) => setMaximumTimeMinutes(Number(e.target.value))}
        />
      </label>
      <div className={styles.formActions}>
        <button
          className={styles.primaryButton}
          data-testid="mission-submit-btn"
          disabled={isPending}
          type="submit"
        >
          Save
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          type="button"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
```

**List view rendering (abbreviated)**
```tsx
<section data-testid="missions-panel">
  <div className={styles.panelHeader}>
    <h1>Missions</h1>
    <button
      className={styles.primaryButton}
      data-testid="create-mission-btn"
      disabled={isPending}
      onClick={() => { setFormError(null); setView('create') }}
      type="button"
    >
      Create mission
    </button>
  </div>
  {listError && <p className={styles.formError}>{listError}</p>}
  <table className={styles.table}>
    <thead>
      <tr>
        <th>Name</th>
        <th>Description</th>
        <th>Status</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      {listData?.map((mission) => (
        <tr key={mission.id} data-testid={`mission-row-${mission.id}`}>
          <td>{mission.name}</td>
          <td>{mission.description}</td>
          <td>
            <span
              className={styles.chip}
              data-tone={mission.isActive ? (mission.isSourceReady ? 'success' : 'warning') : 'critical'}
              data-testid={`mission-status-${mission.id}`}
            >
              {mission.activationState}
            </span>
          </td>
          <td>
            <button
              className={styles.inlineButton}
              data-testid={`view-mission-btn-${mission.id}`}
              disabled={isPending}
              onClick={() => handleOpenDetail(mission.id)}
              type="button"
            >
              View details
            </button>
          </td>
        </tr>
      ))}
    </tbody>
  </table>
</section>
```

**Status chip tone mapping:**
- `activationState === 'Ready'` → `data-tone="success"` (green)
- `activationState === 'Draft'` → `data-tone="warning"` (amber)
- `activationState === 'Inactive'` → `data-tone="critical"` (red)

**`handleOpenDetail`**
```ts
async function handleOpenDetail(id: number) {
  startTransition(async () => {
    try {
      const mission = await getMission(id)
      setSelectedMission(mission)
      setConfirmDeactivate(false)
      setDeactivateError(null)
      setView('detail')
    } catch {
      setListError('Failed to load mission details.')
    }
  })
}
```

**Detail view (abbreviated)**
```tsx
<section data-testid="mission-detail">
  <button onClick={() => setView('list')} type="button">← Back to missions</button>
  <h1 data-testid="mission-detail-name">{selectedMission.name}</h1>
  <p data-testid="mission-detail-description">{selectedMission.description}</p>
  <dl>
    <dt>Difficulty</dt>
    <dd data-testid="mission-detail-difficulty">{selectedMission.difficulty}</dd>
    <dt>Maximum time</dt>
    <dd data-testid="mission-detail-time">{selectedMission.maximumTimeMinutes} min</dd>
    <dt>Status</dt>
    <dd>
      <span
        className={styles.chip}
        data-tone={activationTone}
        data-testid="mission-detail-status"
      >
        {selectedMission.activationState}
      </span>
    </dd>
  </dl>

  <div className={styles.detailActions}>
    <button
      className={styles.inlineButton}
      data-testid="edit-mission-btn"
      disabled={isPending || !selectedMission.isActive}
      onClick={() => { setFormError(null); setView('edit') }}
      type="button"
    >
      Edit
    </button>

    {selectedMission.isActive && !confirmDeactivate && (
      <button
        className={styles.inlineButton}
        data-testid="deactivate-mission-btn"
        disabled={isPending}
        onClick={() => setConfirmDeactivate(true)}
        type="button"
      >
        Deactivate
      </button>
    )}

    {confirmDeactivate && (
      <span className={styles.confirmRow}>
        <span>Deactivate this mission?</span>
        <button
          className={styles.dangerButton}
          data-testid="confirm-deactivate-mission-btn"
          disabled={isPending}
          onClick={() => handleDeactivate(selectedMission.id)}
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
    {deactivateError && <p className={styles.formError} role="alert">{deactivateError}</p>}
  </div>
</section>
```

**Note on the Edit button:** Editing an inactive mission is blocked at the UI layer (`disabled={!selectedMission.isActive}`). The backend does not enforce this restriction, but deactivated missions should not be edited.

**`data-testid` inventory**
- `missions-panel` — list view root
- `create-mission-btn` — list view create button
- `mission-row-{id}` — tbody row
- `mission-status-{id}` — status chip in list row
- `view-mission-btn-{id}` — view details button per row
- `mission-detail` — detail view root
- `mission-detail-name`, `mission-detail-description`, `mission-detail-difficulty`, `mission-detail-time`, `mission-detail-status`
- `edit-mission-btn` — detail view edit button
- `deactivate-mission-btn` — detail view deactivate trigger
- `confirm-deactivate-mission-btn` — confirm step button
- `mission-form` — create/edit form root
- `mission-name-input`, `mission-description-input`, `mission-difficulty-input`, `mission-time-input`
- `mission-submit-btn` — form save button

**CSS additions to `dashboard.module.css`**
- `.formTextarea` — matches `.formInput` style but with `resize: vertical` and `min-height: 5rem`.
- Any other needed classes should reuse existing ones (`.form`, `.formLabel`, `.formInput`, `.formActions`, `.primaryButton`, `.inlineButton`, `.dangerButton`, `.chip`, `.table`, `.panelHeader`, `.confirmRow`, `.formError`). Add only what is genuinely missing.

**Gate**
- Admin navigates to "Missions" → list renders; each row shows name, description, status chip, and "View details" button.
- Clicking "View details" shows the detail view with all five fields and the activation state badge.
- "Create mission" button opens the create form; submitting a valid form navigates to the new mission's detail.
- "Edit" opens the edit form pre-populated; saving navigates back to detail with updated values.
- "Deactivate" shows a confirm step; confirming updates the status chip to "Inactive" and hides both "Deactivate" and "Edit" buttons.
- "Cancel" on the confirm step dismisses without any network call.

---

### Phase 5 — DashboardClient wiring

**Scope**
- Import `MissionsPanel` in `DashboardClient.tsx`.
- Extend `visibleNavigation` filter to hide `missions` from operators.
- Add the `activeNav === 'missions'` rendering branch.

**Import addition**
```ts
import { MissionsPanel } from './MissionsPanel'
```

**`visibleNavigation` filter update**

The current filter:
```tsx
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  return true
})
```

Updated:
```tsx
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'users'
  return true
})
```

Note: `users` was already admin-only by the action-layer guard. Adding it here makes the nav consistent with what the panel actually allows.

**Rendering branch addition** (insert between `activeNav === 'teams'` and the `role === 'operator'` fallback):
```tsx
) : activeNav === 'missions' ? (
  <MissionsPanel role={role} />
) : role === 'operator' && ...
```

**Gate**
- `pnpm build` passes.
- Admin sees the "Missions" nav item; operator and participant do not.
- Clicking "Missions" in the admin sidebar renders `MissionsPanel`.
- Operator dashboard is unaffected — all existing operator views still render.

---

### Phase 6 — E2E tests

**Scope**
- Add `tests/e2e/missions.spec.ts` covering all HU-09 acceptance criteria.
- Verify no HU-01/HU-02/HU-03 regressions: the existing test suites run unmodified.

**`tests/e2e/missions.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

// --- Catalog view ---

test('admin sees missions nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-missions"]')).toBeVisible()
})

test('operator does not see missions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-missions"]')).toHaveCount(0)
})

test('admin missions panel loads with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-mission-btn"]')).toBeVisible()
})

// --- Create flow ---

test('admin can create a mission and land on its detail view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()

  await page.fill('[data-testid="mission-name-input"]', 'Test Mission Alpha')
  await page.fill('[data-testid="mission-description-input"]', 'Navigate to the relay point.')
  await page.fill('[data-testid="mission-difficulty-input"]', 'Advanced')
  await page.fill('[data-testid="mission-time-input"]', '45')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText('Test Mission Alpha')
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')
})

test('create form cancel returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-form"]')).toHaveCount(0)
})

// --- Detail/Edit flow ---

test('admin can open mission detail from catalog row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  const firstViewBtn = page.locator('[data-testid^="view-mission-btn-"]').first()
  await expect(firstViewBtn).toBeVisible()
  await firstViewBtn.click()

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-difficulty"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-time"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-status"]')).not.toBeEmpty()
})

test('admin can edit a mission and changes are reflected in detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.locator('[data-testid^="view-mission-btn-"]').first().click()
  await page.click('[data-testid="edit-mission-btn"]')

  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
  await page.fill('[data-testid="mission-name-input"]', 'Updated Mission Name')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText('Updated Mission Name')
})

test('edit form cancel returns to detail without saving', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.locator('[data-testid^="view-mission-btn-"]').first().click()
  const originalName = await page.locator('[data-testid="mission-detail-name"]').innerText()

  await page.click('[data-testid="edit-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Should Not Be Saved')
  await page.getByRole('button', { name: 'Cancel' }).click()

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText(originalName.trim())
})

// --- Deactivate flow ---

test('admin deactivate flow shows confirm step then marks mission inactive', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.locator('[data-testid^="view-mission-btn-"]').first().click()

  await expect(page.locator('[data-testid="deactivate-mission-btn"]')).toBeVisible()
  await page.click('[data-testid="deactivate-mission-btn"]')
  await expect(page.locator('[data-testid="confirm-deactivate-mission-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-deactivate-mission-btn"]')

  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Inactive')
  await expect(page.locator('[data-testid="deactivate-mission-btn"]')).toHaveCount(0)
})

test('admin deactivate confirm cancel dismisses without change', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.locator('[data-testid^="view-mission-btn-"]').first().click()
  const originalStatus = await page.locator('[data-testid="mission-detail-status"]').innerText()

  await page.click('[data-testid="deactivate-mission-btn"]')
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  await expect(page.locator('[data-testid="confirm-deactivate-mission-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText(originalStatus.trim())
})

// --- Status badge in catalog ---

test('mission status chip reflects activation state', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  // At least one row must have a status chip
  await expect(page.locator('[data-testid^="mission-status-"]').first()).toBeVisible()
})

// --- Regression guard ---

test('HU-01 admin session is not broken by missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('admin')
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})

test('HU-02 users panel still reachable for admin after missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
})

test('HU-04 teams panel still reachable for admin after missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All four HU-09 acceptance paths (create, browse, edit, deactivate) are covered by automated tests.
- HU-01, HU-02, and HU-04 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — add MissionSummaryDto and MissionDto types (HU-09)
feat(frontend): phase 2 — api client for mission-design service (HU-09)
feat(frontend): phase 3 — server actions for mission management (HU-09)
feat(frontend): phase 4 — MissionsPanel component with list/detail/create/edit views (HU-09)
feat(frontend): phase 5 — wire MissionsPanel into DashboardClient (HU-09)
feat(frontend): phase 6 — e2e tests for mission management and regression checks (HU-09)

Ref: HU-09
```

---

## Out of Scope

- **Operator read-only mission view.** The PRD scope for HU-09 specifies "administrator mission catalog view". Operators are excluded from the missions nav item.
- **Mission hierarchy (stages, substages, clues).** HU-10A/10B owns that. The detail view intentionally leaves a `data-testid="mission-detail"` root that future phases can extend.
- **`isSourceReady` gating.** The readiness indicator is displayed but does not gate any action. Readiness derives from HU-10+.
- **Pagination of the mission catalog.** `GET /api/missions` returns all missions without pagination parameters. When the list grows, pagination can be layered on top of the existing `listMissions` function.
- **Self-service mission activation.** `activationState: "Ready"` is a computed backend state determined by `MissionActivationPolicy`. There is no manual "activate" endpoint in HU-09.
- **Any backend changes.**
