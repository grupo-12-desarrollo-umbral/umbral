# Plan: HU-19 Frontend — Session Operator Assignment

**Ref:** HU-19
**Branch:** feature/hu-19-session-operator-assignment
**Date:** 2026-06-03
**Builds on:** HU-16 frontend (session creation: `app/lib/sessions.ts`, `app/actions/sessions.ts`,
`SessionsPanel`, `TriviaSessionCreatedDto`), HU-02/HU-03 frontend (users catalog, `listUsers`,
Administrator-gated server actions, inline two-step confirm pattern, `dal.ts`, `definitions.ts`).

---

## Context

HU-19 is "an Administrator can assign / change the responsible operator for a session." The
backend **domain and application layers exist** for this (committed domain in `a71d782`,
application layer staged on this branch): an `AssignOperatorToSessionCommand` with an
`[Authorize(Roles = "Administrator")]` gate, a backend-owned eligibility check, and a result DTO.
This plan implements the frontend slice that drives that contract.

Three frontend realities shape the work:

1. **The admin surface today is mock.** In `DashboardClient.tsx` the admin branch renders a
   static "Operator assignments" panel (`operatorAssignments` string array) and an inert
   "Assign operators" button. The `sessions` array is hardcoded sample data. None of it is
   backend-driven, and the `sessions` nav item is filtered out for admins
   (`if (role === 'admin') return item.key !== 'sessions'`). HU-19 replaces the inert admin
   assignment affordance with a real, backend-driven flow.

2. **The operator catalog is the Identity user catalog.** The only verified actor-facts source
   for "who is a real operator/admin" is Identity's `GET /api/users`
   (`UserAccessCatalogItemDto`, already consumed by `listUsers` in `app/lib/users.ts`). Its
   numeric `id` is the same integer the backend command expects as `OperatorUserId`. The
   frontend must derive candidates from this source and must **not** invent its own rule for
   who is assignable — the backend eligibility check is the authority.

3. **Current-assignment state must come from the backend, not the client.** The gate forbids a
   client-side source of truth separate from session state. The UI must read the session's
   current `assignedOperatorUserId` from the backend and resolve it to a display name via the
   Identity catalog; after a successful assignment it reflects the value the backend returns.

The current branch's `SessionsPanel` is operator-only (session **creation**). HU-19 adds an
**admin** surface for operator assignment, kept deliberately separate from operator session
control.

---

## Verified Backend Contract

### Verified (application layer, this branch)

`AssignOperatorToSessionCommand` —
`backend/services/session-operations-service/src/Application/Sessions/Commands/AssignOperatorToSession/`:

```csharp
[Authorize(Roles = "Administrator")]
public sealed record AssignOperatorToSessionCommand(
    Guid LiveSessionId,
    int OperatorUserId) : IRequest<AssignOperatorToSessionResultDto>;
```

- **Authorization:** Administrator only (command-level `[Authorize]`). The
  `SessionAdministrationAuthorizationProxy` additionally rejects callers with no id, and any
  non-Administrator/Operator role with `ForbiddenAccessException`.
- **Validation:** `LiveSessionId` non-empty; `OperatorUserId > 0`.
- **Eligibility (backend-owned):** the facade calls `IAssignableSessionOperatorAccessClient`
  against Identity; if the target is not eligible it throws `IneligibleSessionOperatorException`.
  Unknown session id throws `NotFoundException`.
- **Result:** `AssignOperatorToSessionResultDto(Guid LiveSessionId, int AssignedOperatorUserId)`.
- **Domain:** `LiveSession.AssignedOperatorUserId` (`int?`, nullable = unassigned) is set by
  `AssignOperator(...)`; re-assignment overwrites the previous operator (idempotent change, not
  append). This is what makes "assign **or change**" a single operation.

`SessionOperatorEligibilityDecisionDto(string Source, bool IsEligible, int OperatorUserId,
string? Role, string? Reason)` is the backend's internal eligibility shape — **not** a
frontend-facing contract. The frontend never calls eligibility directly; it only observes the
backend's accept/reject on assignment.

### Verified Identity-backed operator catalog / lookup surface

`GET /api/users` (Identity, already wired through `app/lib/users.ts → listUsers`) returns
`PagedResult<UserAccessCatalogItemDto>`:

```ts
type UserAccessCatalogItemDto = {
  id: number            // == OperatorUserId expected by the backend command
  externalIdentityId: string
  displayName: string
  email: string
  role: string          // "Administrator" | "Operator" | "Participant"
  isActive: boolean
}
```

This is the verified actor-facts source for candidate operators **and** for resolving a
session's `assignedOperatorUserId` (an `int`) back to a human-readable name.

### Backend HTTP projections this slice depends on (prerequisites)

The application capability is verified, but two HTTP surfaces are **not yet wired** in
`SessionsEndpoints.cs` (which today exposes only `POST /api/sessions` and
`POST /api/sessions/{id}/participants/reconnect`). The frontend slice consumes the natural REST
projection of the verified command and of the existing `LiveSession` read model. **These must be
exposed before the gate can pass**; the frontend `lib` layer is written so that only thin
status/field mapping changes if the deployed shape differs.

1. **Assign / change operator** — projection of `AssignOperatorToSessionCommand`:

   ```
   POST /api/sessions/{liveSessionId:guid}/operator
   Authorization: Administrator
   Body: { "operatorUserId": <int> }
   200 → { "liveSessionId": "<guid>", "assignedOperatorUserId": <int> }
   ```

   Expected error mapping (frontend treats these defensively):
   - `401` — missing trusted headers
   - `403` — caller is not Administrator (`ForbiddenAccessException`)
   - `404` — `liveSessionId` not found (`NotFoundException`)
   - `422` — target not eligible / invalid target actor (`IneligibleSessionOperatorException`)
   - `400` — malformed body / `operatorUserId <= 0`

2. **Read session assignment state** — projection of the existing `LiveSession`:

   ```
   GET /api/sessions/{liveSessionId:guid}
   Authorization: Administrator (Operator may read own; out of scope here)
   200 → {
     "liveSessionId": "<guid>",
     "sessionCode": "SES-…",
     "title": "…",
     "sessionState": "Scheduled" | "Active" | "Paused" | "Finished" | "Cancelled",
     "assignedOperatorUserId": <int> | null   // null = unassigned
   }
   ```

> **Honesty note for the implementer:** if these routes are not yet merged on the
> session-operations service when this slice starts, treat Phase 2 (lib) and the backend route
> wiring as a coordinated dependency. Do not fabricate assignment state on the client to work
> around a missing `GET` — that would violate the gate ("no client-side source of truth separate
> from the session state"). If the backend ships the read state inside a list endpoint or a
> different field name, adjust only the mapping in `app/lib/sessions.ts`.

---

## Architecture Decisions

- **Admin-only assignment surface, separate from operator control.** A new `SessionOperatorPanel`
  (admin) handles assignment/change. It is reached from the `sessions` nav item, which is
  **enabled for admins** in HU-19 (currently hidden). The operator's `SessionsPanel` (creation)
  is untouched; `DashboardClient` routes `activeNav === 'sessions'` to the operator panel for
  operators and to `SessionOperatorPanel` for admins.

- **Session is addressed by `liveSessionId`.** Because there is no session **list/read model**
  yet (that is HU-20), the admin surface operates on a single session identified by its
  `liveSessionId` (entered, or carried in from the HU-16 "session created" result). A richer
  session picker arrives with the HU-20 read model and is explicitly out of scope. The panel
  reads that session's current state from the backend on load.

- **Candidates come from the verified actor-facts source, backend decides eligibility.** The
  operator `<select>` is populated by filtering the Identity user catalog to **active** users
  whose role is `Operator` or `Administrator`. This filter is a UX convenience that mirrors the
  inputs the backend eligibility check uses (role + active); it is **not** a frontend rule of
  record. The backend remains the authority: an assignment the UI optimistically allowed but the
  backend rejects (`422`) is surfaced as an error and the displayed state is reverted to what the
  backend last confirmed.

- **Current operator is rendered from backend state, resolved through the catalog.** The panel
  displays the session's `assignedOperatorUserId` resolved to `displayName (email)` via the
  catalog, or "Unassigned" when `null`. There is no local mirror of assignment beyond what the
  last backend read/response returned.

- **Server Action owns the assignment call and re-checks Administrator.** `assignSessionOperator`
  in `app/actions/sessions.ts` runs server-side, re-verifies `session.role === 'Administrator'`
  before reaching the backend (defense in depth, mirroring `assignUserRole`), and
  `revalidatePath('/dashboard')` after success.

- **Two-step confirm for the change, matching the established pattern.** Selecting a new operator
  reveals an "Assign" / "Cancel" pair (same shape as HU-03 role change and HU-02 deactivation),
  so an accidental dropdown change is never committed on first interaction. "Assign" is disabled
  while the selection equals the current operator.

- **Optimistic display with revert on failure.** On confirm, the displayed operator updates
  inside a `useTransition`; on error the previous (backend-confirmed) operator is restored and an
  error chip is shown. The optimistic value is replaced by the backend's returned
  `assignedOperatorUserId`, keeping backend state authoritative.

- **Clean, distinct error surfaces.** `403` → "Administrator role required." `422` → "That user
  cannot be assigned as the responsible operator." `404` → "Session not found." Everything else →
  generic retry copy. Unauthorized and invalid-target are visually distinct, per the gate.

---

## Environment

`app/lib/sessions.ts` already reads `SESSION_OPERATIONS_SERVICE_URL`; the new session-ops calls
reuse it. The operator catalog reuses the existing Identity base URL in `app/lib/users.ts`. No new
environment variables.

---

## Phases

### Phase 1 — Types

**Scope**
- Add the frontend-facing types to `app/lib/definitions.ts`. No behaviour change.

**`app/lib/definitions.ts` additions**
```ts
// Backend read projection of a session's assignment-relevant state (GET /api/sessions/{id})
export type SessionOperatorStateDto = {
  liveSessionId: string
  sessionCode: string
  title: string
  sessionState: string
  assignedOperatorUserId: number | null // null = unassigned
}

// Result of POST /api/sessions/{id}/operator (projection of AssignOperatorToSessionResultDto)
export type AssignSessionOperatorResultDto = {
  liveSessionId: string
  assignedOperatorUserId: number
}

// A candidate operator derived from the Identity actor-facts catalog (not a new backend type)
export type AssignableOperatorDto = {
  id: number
  displayName: string
  email: string
  role: string
}
```

**Gate**
- `pnpm build` passes; no runtime change.

---

### Phase 2 — API client layer (`app/lib`)

**Scope**
- Add `getSessionOperatorState` and `assignSessionOperator` to `app/lib/sessions.ts`.
- Add `listAssignableOperators` to `app/lib/users.ts` (derived from the existing `listUsers`).

**`app/lib/sessions.ts` additions** (reuse the existing `getIdentityHeaders` /
`SESSION_OPERATIONS_SERVICE_URL`):
```ts
export async function getSessionOperatorState(
  liveSessionId: string,
): Promise<SessionOperatorStateDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}`,
    { headers: getIdentityHeaders(session), cache: 'no-store' },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getSessionOperatorState failed with status ${response.status}`)
  }
  return response.json() as Promise<SessionOperatorStateDto>
}

export async function assignSessionOperator(
  liveSessionId: string,
  operatorUserId: number,
): Promise<AssignSessionOperatorResultDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}/operator`,
    {
      method: 'POST',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify({ operatorUserId }),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 422) throw new Error('ineligible_operator')
  if (!response.ok) {
    throw new IdentityError('unknown', `assignSessionOperator failed with status ${response.status}`)
  }
  return response.json() as Promise<AssignSessionOperatorResultDto>
}
```
Add the new types to the existing import from `./definitions`.

**`app/lib/users.ts` addition** — candidates from the verified actor-facts source. This walks the
catalog pages so the candidate set is the real user list, not a guessed subset:
```ts
export async function listAssignableOperators(): Promise<AssignableOperatorDto[]> {
  const assignable: AssignableOperatorDto[] = []
  let page = 1
  // The candidate set is "active users whose role can operate a session" — role + active are
  // actor-facts read straight from Identity. The backend eligibility check is still the authority.
  for (;;) {
    const result = await listUsers(page, 100)
    for (const u of result.items) {
      if (u.isActive && (u.role === 'Operator' || u.role === 'Administrator')) {
        assignable.push({ id: u.id, displayName: u.displayName, email: u.email, role: u.role })
      }
    }
    if (!result.hasNextPage) break
    page += 1
  }
  return assignable
}
```
Import `AssignableOperatorDto` into `app/lib/users.ts`.

**Gate**
- `pnpm build` passes.
- `getSessionOperatorState` / `assignSessionOperator` map every documented status to a distinct
  error (`session_not_found`, `ineligible_operator`, `invalid_input`, `IdentityError`).
- `listAssignableOperators` returns only active Operator/Administrator catalog entries.

---

### Phase 3 — Server Actions (`app/actions/sessions.ts`)

**Scope**
- Add three Administrator-gated actions; alias lib imports to avoid name clashes (the HU-03
  pattern).

**`app/actions/sessions.ts` additions**
```ts
import {
  getSessionOperatorState as getSessionOperatorStateLib,
  assignSessionOperator as assignSessionOperatorLib,
} from '@/app/lib/sessions'
import { listAssignableOperators as listAssignableOperatorsLib } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type {
  SessionOperatorStateDto,
  AssignSessionOperatorResultDto,
  AssignableOperatorDto,
} from '@/app/lib/definitions'

export async function getAssignableOperators(): Promise<AssignableOperatorDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  return listAssignableOperatorsLib()
}

export async function getSessionOperatorState(
  liveSessionId: string,
): Promise<SessionOperatorStateDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  return getSessionOperatorStateLib(liveSessionId)
}

export async function assignSessionOperator(
  liveSessionId: string,
  operatorUserId: number,
): Promise<AssignSessionOperatorResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await assignSessionOperatorLib(liveSessionId, operatorUserId)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes.
- Each action throws `'Forbidden'` for any non-Administrator session before any backend call.
- A successful assignment revalidates `/dashboard`.

---

### Phase 4 — Admin `SessionOperatorPanel` and navigation

**Scope**
- Enable the `sessions` nav item for admins in `DashboardClient.tsx`.
- Route `activeNav === 'sessions'` to `SessionOperatorPanel` for admins (operators keep
  `SessionsPanel`).
- Add `SessionOperatorPanel` (new file `app/dashboard/SessionOperatorPanel.tsx`).
- Replace the inert admin "Assign operators" affordance: the static `operatorAssignments` panel
  is left as read-only context for now, but the "Assign operators" button navigates to the
  sessions panel (`setActiveNav('sessions')`) instead of being inert.

**Navigation change in `DashboardClient.tsx`**
```tsx
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  // HU-19: admins now get the sessions nav for operator assignment
  if (role === 'admin') return true
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'trivias'
  return true
})
```

**Routing change** — the `sessions` branch must fork by role:
```tsx
) : activeNav === 'sessions' ? (
  role === 'admin'
    ? <SessionOperatorPanel />
    : <SessionsPanel role={role as 'operator'} />
) : ...
```

**`app/dashboard/SessionOperatorPanel.tsx`** (new). Behaviour:
- An input to set the target `liveSessionId` (prefilled if one was just created in this session
  via HU-16; otherwise admin pastes the id). On submit, calls `getSessionOperatorState`.
- Renders the backend-confirmed current state: title, session code, state, and **current
  responsible operator** resolved from the catalog (`displayName (email)`, or "Unassigned").
- An operator `<select>` populated by `getAssignableOperators`, pre-selected to the current
  `assignedOperatorUserId` when present.
- Two-step confirm: changing the select reveals "Assign" / "Cancel"; "Assign" is disabled when
  the pending id equals the current id.
- On "Assign": `assignSessionOperator` inside `useTransition`; optimistic display update replaced
  by the returned `assignedOperatorUserId`; on error, revert and show an error chip.

Sketch:
```tsx
'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getAssignableOperators,
  getSessionOperatorState,
  assignSessionOperator,
} from '@/app/actions/sessions'
import type { AssignableOperatorDto, SessionOperatorStateDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

export function SessionOperatorPanel() {
  const [sessionId, setSessionId] = useState('')
  const [state, setState] = useState<SessionOperatorStateDto | null>(null)
  const [operators, setOperators] = useState<AssignableOperatorDto[]>([])
  const [pendingOperatorId, setPendingOperatorId] = useState<number | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  useEffect(() => {
    getAssignableOperators().then(setOperators).catch(() => setOperators([]))
  }, [])

  function loadSession(id: string) {
    startTransition(async () => {
      setLoadError(null); setAssignError(null)
      try {
        const s = await getSessionOperatorState(id.trim())
        setState(s)
        setPendingOperatorId(s.assignedOperatorUserId)
      } catch (err) {
        setState(null)
        setLoadError(
          err instanceof Error && err.message === 'session_not_found'
            ? 'Session not found. Check the session id.'
            : err instanceof Error && err.message.includes('Administrator')
              ? 'Administrator role required.'
              : 'Failed to load the session. Try again.',
        )
      }
    })
  }

  function currentOperatorLabel(s: SessionOperatorStateDto): string {
    if (s.assignedOperatorUserId == null) return 'Unassigned'
    const op = operators.find((o) => o.id === s.assignedOperatorUserId)
    return op ? `${op.displayName} (${op.email})` : `User #${s.assignedOperatorUserId}`
  }

  function handleAssign() {
    if (!state || pendingOperatorId == null) return
    const previous = state.assignedOperatorUserId
    startTransition(async () => {
      setAssignError(null)
      try {
        const result = await assignSessionOperator(state.liveSessionId, pendingOperatorId)
        setState({ ...state, assignedOperatorUserId: result.assignedOperatorUserId })
      } catch (err) {
        setPendingOperatorId(previous)
        setAssignError(
          err instanceof Error && err.message === 'ineligible_operator'
            ? 'That user cannot be assigned as the responsible operator.'
            : err instanceof Error && err.message === 'session_not_found'
              ? 'Session not found.'
              : err instanceof Error && err.message.includes('Administrator')
                ? 'Administrator role required.'
                : 'Assignment failed. Try again.',
        )
      }
    })
  }

  // …render: session-id form (data-testid="session-operator-id-input",
  //   "session-operator-load-btn"); current-state card
  //   (data-testid="current-operator", "session-operator-state"); operator <select>
  //   (data-testid="operator-select") + Assign/Cancel (data-testid="assign-operator-btn");
  //   error chips (data-testid="session-operator-load-error" / "assign-operator-error").
}
```

**`data-testid` additions required**
- `session-operator-panel` (root section)
- `session-operator-id-input`, `session-operator-load-btn`
- `current-operator`, `session-operator-state`
- `operator-select`, `assign-operator-btn`
- `session-operator-load-error`, `assign-operator-error`

**CSS** — reuse existing `dashboard.module.css` classes (`panel`, `panelHeader`, `inlineSelect`,
`inlineButton`, `primaryButton`, `confirmRow`, `errorBanner`, `chip`). No new design tokens.

**Gate**
- Admin sees a `sessions` nav item; clicking it renders `SessionOperatorPanel`.
- Loading a known session id shows its backend state and current operator (name resolved from the
  catalog, or "Unassigned").
- The operator `<select>` lists only active Operator/Administrator candidates and pre-selects the
  current operator.
- Operators still see `SessionsPanel` (creation) under `sessions`; their flow is unchanged.

---

### Phase 5 — E2E tests (HU-19)

**Scope**
- Add `tests/e2e/session-operator.spec.ts`. The existing `adminPage` / `operatorPage` fixtures
  cover the roles; no new fixture is required.
- Network calls to the session-ops and Identity services are exercised through the server actions;
  follow the interception/stub approach already used in `sessions.spec.ts` for backend responses.
- Re-run `sessions.spec.ts`, `users.spec.ts`, `roles.spec.ts`, `auth.spec.ts` for regressions.

**Coverage (one test per gate clause, plus role guards)**
```ts
import { test, expect } from '../fixtures/auth'

// Surfacing current state (gate: UI shows current assignment from backend)
test('admin sees the sessions nav and can open the operator assignment panel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-operator-panel"]')).toBeVisible()
})

test('loading a session renders its current responsible operator from backend state', async ({ adminPage: page }) => {
  // stub GET /api/sessions/{id} → assignedOperatorUserId set; GET /api/users → catalog
  // expect [data-testid="current-operator"] to show the resolved displayName, not a client guess
})

test('a session with no operator shows Unassigned', async ({ adminPage: page }) => {
  // stub assignedOperatorUserId: null → current-operator === "Unassigned"
})

// Assign / change (gate: admin can assign or change successfully)
test('admin can assign an operator and the panel reflects the backend result', async ({ adminPage: page }) => {
  // select an operator, Assign, stub 200 → current-operator updates to the assigned user
})

// Clean failures (gate: invalid-target and unauthorized render cleanly)
test('invalid target operator surfaces a clean error and reverts', async ({ adminPage: page }) => {
  // stub POST → 422 → [data-testid="assign-operator-error"] visible, current-operator unchanged
})

test('unauthorized assignment surfaces a clean error', async ({ adminPage: page }) => {
  // stub POST → 403 → distinct "Administrator role required." error rendered
})

// Role guard
test('operator sees the creation panel, not the operator-assignment panel', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-operator-panel"]')).toHaveCount(0)
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All four HU-19 acceptance gate clauses are exercised: current-state display, successful
  assign/change, clean invalid-target (`422`) failure, clean unauthorized (`403`) failure.
- No regression in HU-16 session creation or HU-02/HU-03 users/roles specs.

---

## Commit Sequence

```
feat(frontend): phase 1 — session operator assignment types
feat(frontend): phase 2 — session-ops client and assignable-operator catalog lookup
feat(frontend): phase 3 — administrator-gated session operator server actions
feat(frontend): phase 4 — admin SessionOperatorPanel and sessions nav for admins
feat(frontend): phase 5 — e2e tests for HU-19 session operator assignment

Ref: HU-19
```

---

## Out of Scope

- **The "my assigned sessions" operator read model (HU-20).** No operator-facing list of assigned
  sessions, no cross-session assignment dashboard, no session list/search. The admin surface here
  targets a single `liveSessionId`; a real session picker arrives with HU-20.
- **A backend eligibility-preview call.** The UI does not pre-validate candidates against the
  backend eligibility endpoint; it offers role+active candidates and defers final authority to the
  backend's accept/reject on assignment.
- **Replacing the static admin "Operator assignments" / sample `sessions` mock wholesale.** Those
  remain as visual context until the HU-20 read model backs them with real data; HU-19 only wires
  the assignment flow and points the admin's "Assign operators" affordance at it.
- **Backend work.** Wiring `POST /api/sessions/{id}/operator` and `GET /api/sessions/{id}` in
  `SessionsEndpoints.cs`, implementing `IAssignableSessionOperatorAccessClient` in Infrastructure,
  and the exception→status mapping are session-operations service responsibilities (prerequisites,
  documented above), not part of this frontend slice.
- **Refreshing an operator's own session/role after they are assigned.** Same accepted limitation
  as HU-03: assignment changes are reflected on the next read, not pushed to a live operator
  session.
```
