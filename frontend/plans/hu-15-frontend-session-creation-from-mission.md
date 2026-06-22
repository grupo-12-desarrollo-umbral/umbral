# Plan: HU-15 Frontend — Session Creation from an Active Mission

**Ref:** HU-15 · DES-22 · PRD DES-70
**Branch:** feature/hu-15-session-creation-from-mission
**Date:** 2026-06-21
**Builds on:** HU-11/12 frontend (sessions panel, `SessionOperatorPanel`, `app/lib/sessions.ts`,
`app/actions/sessions.ts`, `app/lib/missions.ts`), HU-03 (role-gated server actions).

---

## Context

Backend HU-15 is fully implemented (in the working tree per `HANDOFF.md`). The session-creation
endpoint has been **reshaped to mission-only**: the trivia quiz is no longer a session source. A
mission already carries its runtime snapshot (stages/substages/targets), so the session is built
from a `MissionRuntimeSnapshot` the backend fetches itself — the client sends only the mission id.

The frontend currently still creates a session from **two** sources: an active mission **and** a
published trivia quiz (`sourceTriviaQuizId`). Every layer carries the stale quiz field:

- `CreateTriviaSessionRequest` / `TriviaSessionCreatedDto` types (`definitions.ts`)
- `createTriviaSession` client (`app/lib/sessions.ts`) with quiz 404/409 error branches
- `createTriviaSession` + `getPublishedTrivias` server actions (`app/actions/sessions.ts`)
- `SessionOperatorPanel` — a **Quiz** `<select>`, quiz state, quiz error copy
- `sessions.spec.ts` — quiz-selector tests and a quiz happy-path

This slice removes the quiz as a session source everywhere in the frontend and aligns the client to
the reshaped contract. It is a **small surface** (one reshaped endpoint) — modelled on the
`hu-03-…` exemplar, code-complete throughout.

No `SessionMode` / `SessionSource` type exists in the frontend today (the explorer confirmed; the
e2e suite never references them). So the "no `SessionMode`" gate is satisfied by **not introducing
one** — there is nothing to remove. We record it as a standing gate, not a task.

---

## Verified Backend Contract

Verified against `backend/services/session-operations-service/src/...` in this worktree.

### `POST /api/sessions` — Administrator only

**Request body** (`Api/Endpoints/SessionsEndpoints.cs:206`, `CreateSessionRequest`)
```jsonc
{
  "missionId": 7,
  "title": "Friday Night Hunt",
  "maximumTimeMinutes": 45,
  "scheduledAt": "2026-12-15T18:00:00.000Z"   // ISO 8601 UTC
}
```
`SourceTriviaQuizId` is **gone** from the request (only survives in EF migration `*.Designer.cs`
history, which we never touch).

**Response `201 Created`** (`Application/Sessions/DTOs/CreateSessionResultDto.cs`)
Location header: `/api/sessions/{liveSessionId}`. Body (camelCase over the wire):
```jsonc
{
  "liveSessionId": "…uuid…",
  "sessionCode": "SES-…",
  "title": "Friday Night Hunt",
  "sessionState": "Scheduled",
  "scheduledAt": "2026-12-15T18:00:00+00:00"
}
```
**`sourceTriviaQuizId` and `questionCount` are gone from the response** — the current
`TriviaSessionCreatedDto` declares both; they must be dropped.

**Error cases** (verified in `Api/Services/ProblemDetailsExceptionHandler.cs` +
`Domain/Services/SessionCreationPolicy.cs`)

| Status | When | ProblemDetails `type` | `title` |
|---|---|---|---|
| `409 Conflict` | mission **inactive** OR **not runtime-ready** (one exception, two reasons) | `"mission-not-eligible-for-session"` | `"Conflict."` |
| `404 Not Found` | mission id absent | _(none set)_ | `"Resource not found."` |
| `401` | missing/invalid bearer | — | — |
| `403` | caller not Administrator | — | — |

`409` detail is `"Mission '{id}' cannot be used to create a new session because the mission is
inactive."` _or_ `"…because the mission is not runtime-ready."` — both share the one `type`, so the
frontend surfaces **one** "inactive or not runtime-ready" message and does not branch on the reason.

> **Contract note — the "422" in the task brief.** The verified eligibility/readiness rejection is
> **409 only**. No `422` is emitted for ineligibility. A `422`/`400` would only come from the
> FluentValidation pipeline on a malformed body (empty title, non-positive minutes) — which the UI
> already prevents via `required`/`min`/`max` and the submit-disabled guard. The client keeps its
> existing `400 → invalid_input` branch as the catch-all for malformed input and **adds no 422
> eligibility branch** (there is no source for one). See Open Questions if a 422 path is later
> introduced.

---

## Architecture Decisions

- **Rename away from "trivia", don't alias.** The stale names (`createTriviaSession`,
  `CreateTriviaSessionRequest`, `TriviaSessionCreatedDto`) are renamed to mission-neutral names
  (`createSession`, `CreateSessionRequest`, `SessionCreatedDto`) matching the backend DTOs. This
  is required by the gate ("no UI type/copy treats TriviaQuiz as a SessionSource") — a lingering
  `Trivia` in a session-creation symbol is exactly the stale-copy the gate forbids.
- **Mission is the only source select.** The Quiz `<select>` and all quiz state
  (`quizzes`, `quizzesError`, `selectedQuizId`) are deleted from `SessionOperatorPanel`. The form
  posts `{ missionId, title, maximumTimeMinutes, scheduledAt }`.
- **Surface 409 ineligibility as one explicit message.** The existing `mission_not_eligible`
  branch and its copy ("inactive or not runtime-ready…") are correct and **kept verbatim** — the
  e2e already asserts the substring `inactive or not runtime-ready`. We only delete the
  `quiz_not_published` / `quiz_not_found` branches.
- **Created session shows in Scheduled state.** Already satisfied — on success the panel calls
  `refreshSessions()` and the new row renders with its `sessionState` ("Scheduled") chip via the
  existing `getSessionStateTone` path. No new code; just verified by the happy-path e2e.
- **`getPublishedTrivias` server action is removed if orphaned.** It exists only to feed the quiz
  select. Phase 2 greps for other importers first; if none, delete it (and drop the now-unused
  `listTriviaQuizzes` import from `actions/sessions.ts`). The HU-11 **trivias panel** uses its own
  trivias action/lib path and is unaffected — Phase 4 keeps its regression test.
- **`TriviaQuizSummaryDto` type stays.** It is still used by the trivias-authoring surface (HU-11),
  not just sessions. Only its **use as a session source** is removed. Do not delete the type.

---

## Environment

No new env vars. `app/lib/sessions.ts` already reads `API_GATEWAY_URL` (line 18) and posts to
`${API_GATEWAY_URL}/api/sessions` with a Keycloak bearer via `getGatewayHeaders`. Mission listing
(`app/lib/missions.ts`) is unchanged.

---

## Phases

### Phase 1 — Types reshape (`app/lib/definitions.ts`)

**Scope** — rename the two session-creation types and drop the quiz/question fields. This surfaces
downstream compile errors that Phases 2–3 resolve.

**Change** (lines 245–261):
```ts
// Before
export type CreateTriviaSessionRequest = {
  missionId: number
  sourceTriviaQuizId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string // ISO 8601 UTC string
}

export type TriviaSessionCreatedDto = {
  liveSessionId: string
  sessionCode: string
  title: string
  sessionState: string
  scheduledAt: string
  sourceTriviaQuizId: number
  questionCount: number
}

// After
export type CreateSessionRequest = {
  missionId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string // ISO 8601 UTC string
}

export type SessionCreatedDto = {
  liveSessionId: string   // UUID
  sessionCode: string     // e.g. "SES-A1B2C3D4E5F6"
  title: string
  sessionState: string    // "Scheduled" on create
  scheduledAt: string     // ISO 8601
}
```
Leave `TriviaQuizSummaryDto`, `TriviaQuizDto`, `SessionLifecycleState`,
`SessionAssignmentSummaryDto` untouched.

**Gate**
- `pnpm build` surfaces type errors only in `app/lib/sessions.ts`, `app/actions/sessions.ts`,
  `app/dashboard/SessionOperatorPanel.tsx` (expected; resolved in Phases 2–3).

---

### Phase 2 — Data client + server action (`app/lib/sessions.ts`, `app/actions/sessions.ts`)

**Scope** — rename the client/action and strip quiz error handling.

**`app/lib/sessions.ts`** — rename `createTriviaSession` → `createSession`, retype, drop quiz
branches (current body at lines 38–68):
```ts
export async function createSession(
  req: CreateSessionRequest,
): Promise<SessionCreatedDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(req),
  })

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('mission_not_found')
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { type?: string } | null
    if (problem?.type === 'mission-not-eligible-for-session') throw new Error('mission_not_eligible')
    throw new Error('mission_not_eligible') // 409 has a single type now; default to the same message
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createSession failed with status ${response.status}`)
  }

  return response.json() as Promise<SessionCreatedDto>
}
```
Notes vs. current source:
- 404 no longer needs to read the body to disambiguate Mission-vs-Quiz — mission is the only
  source. Collapse to a single `mission_not_found`.
- 409 has one `type` now; the `quiz_not_published` fallback is removed. (Keeping the `type` read is
  harmless and future-proof, but both branches map to `mission_not_eligible`.)
- 403 copy changed from "Operator role required." → "Administrator role required." (endpoint is
  Administrator-only; verified `RequireAuthorization(AuthorizationPolicies.Administrator)`).
- Update the type imports at the top of the file (lines 2–13): `CreateTriviaSessionRequest` →
  `CreateSessionRequest`, `TriviaSessionCreatedDto` → `SessionCreatedDto`.

**`app/actions/sessions.ts`**
- Rename the imported lib symbol and the action (lines 7, 48–56):
  ```ts
  import { createSession as createSessionLib, /* …rest unchanged… */ } from '@/app/lib/sessions'

  export async function createSession(
    req: CreateSessionRequest,
  ): Promise<SessionCreatedDto> {
    const session = await verifySession()
    if (session.role !== 'Administrator') throw new Error('Forbidden')
    const result = await createSessionLib(req)
    revalidatePath('/dashboard')
    return result
  }
  ```
- Update the `@/app/lib/definitions` type imports (lines 18–31): `CreateTriviaSessionRequest` →
  `CreateSessionRequest`, `TriviaSessionCreatedDto` → `SessionCreatedDto`.
- **`getPublishedTrivias` (lines 34–39):** grep for importers first —
  `rg "getPublishedTrivias" frontend/`. If the only hit is `SessionOperatorPanel.tsx` (removed in
  Phase 3), delete the action and drop the now-unused `import { listTriviaQuizzes } from
  '@/app/lib/trivias'` (line 4). If anything else imports it, leave it. **Do not** touch
  `getActiveMissions` (lines 41–46) — it stays as the mission source feeder.

**Gate**
- `pnpm build` passes except for `SessionOperatorPanel.tsx` (resolved in Phase 3).
- A non-Administrator caller of `createSession` throws `'Forbidden'` before any fetch.
- `rg "createTriviaSession|sourceTriviaQuizId" frontend/app/lib frontend/app/actions` returns no
  hits.

---

### Phase 3 — Mission-only creation form (`app/dashboard/SessionOperatorPanel.tsx`)

**Scope** — remove the Quiz select and all quiz state/copy; post the mission-only request.

**Imports (lines 4–17)**
- From `@/app/actions/sessions`: drop `getPublishedTrivias`; rename `createTriviaSession` →
  `createSession`.
- From `@/app/lib/definitions`: drop `TriviaQuizSummaryDto`.

**State (lines 32–34)** — delete:
```ts
const [quizzes, setQuizzes] = useState<TriviaQuizSummaryDto[] | null>(null)
const [quizzesError, setQuizzesError] = useState<string | null>(null)
const [selectedQuizId, setSelectedQuizId] = useState('')
```

**Initial load (lines 41–48)** — drop the quiz fetch, keep missions:
```ts
useEffect(() => {
  getActiveMissions()
    .then(setMissions)
    .catch(() => setMissionsError('Failed to load available missions. Reload the page.'))
}, [])
```

**Submit handler (lines 124–164)** — drop `sourceTriviaQuizId` and `setSelectedQuizId('')`;
remove the `quiz_not_published` / `quiz_not_found` error branches. Keep `mission_not_eligible`
(verbatim copy), `mission_not_found`, `invalid_input`, and the generic fallback:
```ts
await createSession({
  missionId: Number(selectedMissionId),
  title: title.trim(),
  maximumTimeMinutes: Number(maxMinutes),
  scheduledAt: new Date(scheduledAt).toISOString(),
})
setSelectedMissionId('')
setTitle('')
setMaxMinutes('60')
setScheduledAt('')
refreshSessions()
```
```ts
} catch (err) {
  if (err instanceof Error && err.message === 'mission_not_eligible') {
    setFormError(
      'The selected mission is inactive or not runtime-ready and cannot be used for session creation. ' +
      'Activate the mission and ensure all stages, substages, and targets are configured.',
    )
  } else if (err instanceof Error && err.message === 'mission_not_found') {
    setFormError('The selected mission was not found. Reload the page and try again.')
  } else if (err instanceof Error && err.message === 'invalid_input') {
    setFormError('Invalid session data. Check all fields and try again.')
  } else {
    setFormError('Session creation failed. Try again.')
  }
}
```
> Keep the substring **"inactive or not runtime-ready"** — the e2e asserts it. The trailing clause
> changes "quiz selections" → "targets" (mission has no quiz now); the asserted substring is
> unaffected.

**Markup**
- Delete the `quizzesError` banner (lines 307–311).
- Delete the entire **Quiz** `formGroup` and the "No published quizzes" empty-state (lines
  346–370).
- Submit button `disabled` (lines 420–428) — drop `!selectedQuizId` and `quizzes?.length === 0`:
  ```tsx
  disabled={
    isCreating ||
    !selectedMissionId ||
    !title.trim() ||
    !maxMinutes ||
    !scheduledAt
  }
  ```
- The "Create a session from an active mission…" heading copy (line 296) is already correct —
  leave it.

**Gate**
- `pnpm build` passes with zero type errors.
- `rg "quiz|Quiz|trivia|Trivia" app/dashboard/SessionOperatorPanel.tsx` returns no hits.
- Admin → Sessions shows a Mission select, Title, Maximum time, Scheduled at, and a Create button —
  **no Quiz select**.

---

### Phase 4 — E2E reshape (`tests/e2e/sessions.spec.ts`)

**Scope** — drop quiz-centric tests, fix the happy-path and the network-intercept marker, keep the
mission-not-eligible and regression tests.

**Delete**
- `'session quiz selector contains only published quizzes'` (lines 43–58) — the select is gone.

**Edit**
- `'session submit button is disabled until all fields are filled'` (lines 68–90): remove the quiz
  select/fill steps; assert disabled after mission only, enabled once title + maxTime + scheduledAt
  are filled.
- `'admin can create a session …'` (rename to **"from an active mission"**, lines 94–120) and
  `'admin create form clears after a successful creation'` (lines 124–144): delete the
  `quizSelect` block; select a mission, fill title/maxTime/scheduledAt, submit, assert the title
  appears in `[data-testid="session-operator-list"]` and the form clears. (The created row renders
  in **Scheduled** state — optionally assert the row text contains `Scheduled`.)
- Both 409 intercepts (lines 149–219): the marker `body.includes('createTriviaSession')` must
  become `body.includes('createSession')` (the Server Action id in the POST-to-`/dashboard` body).
  Keep the `mission-not-eligible-for-session` test asserting the `inactive or not runtime-ready`
  substring. The generic-409 test (lines 149–182) now also resolves to `mission_not_eligible` →
  assert `[data-testid="session-form-error"]` visible + form intact (drop any quiz-published copy
  assumption).
- `'session submit button is disabled when no quiz is selected'` (lines 62–66): rename to
  **"disabled when no mission is selected"** and assert it is disabled on initial load (no mission
  chosen yet).

**Keep unchanged**
- Nav-visibility tests (lines 5–18), `'operator sessions panel is read-only…'` (22–31),
  `'admin sessions panel loads and shows the create form'` (33–39), HU-11 trivias regression
  (223–229), HU-03 role-chip regression (233–237).

**Gate**
- `pnpm exec playwright test tests/e2e/sessions.spec.ts` passes.
- The create-from-mission happy path and the **409 mission-not-eligible** path are both exercised.
- `rg "session-quiz-select|sourceTriviaQuizId|createTriviaSession" tests/` returns no hits.

---

## Final Gates (whole slice)

- `pnpm build` (typecheck + build) passes.
- `pnpm exec playwright test tests/e2e/sessions.spec.ts` passes.
- UI selects a **Mission** as the only session source; no UI type/copy treats `TriviaQuiz` as a
  `SessionSource` — `rg -i "trivia|quiz" app/lib/sessions.ts app/actions/sessions.ts app/dashboard/SessionOperatorPanel.tsx`
  returns no session-source hits.
- No UI type/copy introduces `SessionMode` — `rg "SessionMode" frontend/` returns no hits
  (nothing to add; standing gate).
- Created session is shown in **Scheduled** state in the assignment list.

---

## Commit Sequence

```
feat(frontend): phase 1 — reshape session-creation types to mission-only (HU-15)
feat(frontend): phase 2 — createSession client + server action, drop quiz source (HU-15)
feat(frontend): phase 3 — mission-only session creation form (HU-15)
test(frontend): phase 4 — e2e create-from-mission incl. not-eligible path (HU-15)

Ref: HU-15
```

---

## Out of Scope

- Backend changes — the reshaped endpoint already ships in this worktree (`HANDOFF.md`).
- Surfacing the mission's runtime snapshot (stages/substages/targets) in the create form — the
  client sends only `missionId`; the backend resolves the snapshot. No preview UI is required.
- Trivia-quiz authoring (HU-11) — untouched; `TriviaQuizSummaryDto` and the trivias panel remain.
- A `422` eligibility branch — backend emits `409` only for ineligibility (see Contract note).

---

## Open Questions

- **`getPublishedTrivias` ownership.** Phase 2 deletes it only if `rg "getPublishedTrivias"
  frontend/` shows no importer besides the (removed) panel. If another surface imports it, leave it
  and the `listTriviaQuizzes` import in place.
- **422 from validation.** If the backend later maps FluentValidation failures to `422` (instead of
  the assumed `400`), add a `422 → invalid_input` branch in `createSession`. Verified today: only
  `400` is handled and the UI prevents malformed bodies, so no branch is added now.
