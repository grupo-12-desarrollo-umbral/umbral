# Plan: HU-16 Frontend — Trivia-Substage Runtime-Snapshot Realignment

**Ref:** HU-16
**Branch:** feature/hu-16-runtime-snapshot-trivia-substage
**Date:** 2026-07-04
**Builds on:** HU-15 frontend (mission-only `CreateSessionRequest`, `SessionOperatorPanel`
create form, `app/lib/sessions.ts`, `app/actions/sessions.ts`, `sessions.spec.ts`)

---

## Context

This is a **verification + cleanup** slice, not a feature build. HU-15 already migrated
session creation to a **mission-only** source: the backend contract is UNCHANGED from HU-15
and the frontend was aligned to it in that slice. HU-16 realigns the trivia-substage runtime
snapshot on the backend; the frontend's only job is to **confirm** no residual UI copy or
types still frame a session as trivia-typed / quiz-sourced / SessionMode-driven, and to close
one small gap in how the eligibility rejection is surfaced.

Verified current state (each anchor opened and confirmed — see altitudes below):

- `app/lib/definitions.ts:245` — `CreateSessionRequest` is already mission-only:
  `{ missionId, title, maximumTimeMinutes, scheduledAt }`. **No `SessionMode` type exists**
  anywhere under `app/` (grep: 0 hits).
- `app/dashboard/SessionOperatorPanel.tsx:307` — the admin create form has a single source
  input, a Mission `<select>` (`data-testid="session-mission-select"`). No quiz / SessionMode /
  standalone-trivia option.
- `app/lib/sessions.ts:38` — `createSession` maps `409 → 'mission_not_eligible'` but **`422`
  is not handled** and falls through to `IdentityError('unknown', …)`, which the panel renders
  as the generic "Session creation failed" banner instead of the eligibility message. The task
  lists **409/422** both as the not-eligible signal — this is the one real gap.
- `tests/e2e/sessions.spec.ts` — already covers create-from-mission happy path (`:110`),
  not-runtime-ready missions disabled (`:71`), and the 409 error banner keeping the form
  intact (`:160`). **Missing:** the 422 not-eligible path, and a guard asserting the creation
  UI carries no SessionMode / quiz-as-source / standalone-trivia copy.
- `tests/e2e/mission-hierarchy.spec.ts:238` — an existing guard test (`no SessionMode or
  SessionSource copy appears in the mission authoring UI`) that Phase 3 mirrors for the
  session-creation surface.

The three residual `TriviaQuiz*` types in `definitions.ts` (`TriviaQuizSelectionDto`,
`TriviaQuizSummaryDto`, `TriviaQuizDto`, …) are **mission-substage authoring** types — trivia
is authored into a mission substage upstream. They are correct and out of scope; none is wired
into `CreateSessionRequest` or the create form.

---

## Verified Backend Contract (UNCHANGED from HU-15)

### `POST /api/sessions`

Administrator only. Creates a session from a single mission source.

**Request body** (`CreateSessionRequest`, `definitions.ts:245`)
```ts
{
  missionId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string // ISO 8601 UTC
}
```

**Response `201`** → `SessionCreatedDto` (`definitions.ts:252`):
`{ liveSessionId, sessionCode, title, sessionState /* "Scheduled" */, scheduledAt }`

**Error cases** (as mapped / to-be-mapped in `app/lib/sessions.ts`)
- `400` — invalid input → `Error('invalid_input')`
- `401` — auth failed → `IdentityError('unauthorized')`
- `403` — not Administrator → `IdentityError('unauthorized')`
- `404` — mission not found → `Error('mission_not_found')`
- `409` — mission not eligible / not runtime-ready → `Error('mission_not_eligible')` *(mapped)*
- `422` — mission not eligible / not runtime-ready → **currently unmapped** (Phase 2 fixes)

---

## Architecture Decisions

- **No new UI, no new types.** HU-15 already delivered the mission-only form and payload.
  HU-16 verifies that surface and closes one error-mapping gap. Anything larger is scope creep.
- **Map `422` to the same `mission_not_eligible` message as `409`.** The task states the
  backend signals ineligibility with **either** 409 or 422. Both mean the same thing to the
  admin ("activate the mission / finish authoring"), and the panel already renders a specific
  banner for `mission_not_eligible` (`SessionOperatorPanel.tsx:144`). Mapping both codes to one
  error keyword is correct regardless of which code the backend actually returns for a given
  cause — see **Open Questions**.
- **Guard the absence, don't just assume it.** A grep proving 0 `SessionMode` hits today does
  not stop a future edit from reintroducing the copy. Phase 3 adds a standing e2e guard on the
  session-creation UI, mirroring the existing mission-authoring guard
  (`mission-hierarchy.spec.ts:238`).
- **Leave the admin-overview demo scaffolding alone (flagged, not silently kept).** The
  `Live sessions` panel in `DashboardClient.tsx:1093` renders a hardcoded demo array
  (`const sessions: Session[]` at `:75`, `title: 'Downtown Trivia Night'`) and labels it
  "Active trivia sessions" (`:1097`). This is mockup content on the admin landing view, not a
  creation affordance and not wired to the real `SessionAssignmentSummaryDto` model. It does
  not offer a standalone-trivia-session **creation** path, so it does not violate the gates.
  Rewriting all of that overview's "trivia night" framing is a cosmetic effort outside this
  realignment slice — recorded under **Out of Scope** rather than expanded into here.

---

## Environment

No new environment variables. `API_GATEWAY_URL` in `app/lib/sessions.ts` already covers
`POST /api/sessions`.

---

## Phases

### Phase 1 — Verification sweep (gate-only, no code)

**Scope**
- Confirm, against source, that the session-creation surface is mission-only and free of
  SessionMode / quiz-as-source / standalone-trivia framing. This phase produces no diff; it
  establishes the baseline the later gates lock in.

**Checks**
- `app/lib/definitions.ts` — `CreateSessionRequest` has exactly `missionId, title,
  maximumTimeMinutes, scheduledAt`; no `SessionMode`, `sessionMode`, `triviaQuizId`, or
  `quiz` field on it.
- `app/dashboard/SessionOperatorPanel.tsx` — the `<form data-testid="session-create-form">`
  contains one source input, the Mission `<select>`; no quiz/mode selector.
- Repo greps must all return **0 hits under the session-creation surface**
  (`app/lib/sessions.ts`, `app/actions/sessions.ts`, `app/dashboard/SessionOperatorPanel.tsx`):
  ```
  grep -rniE 'sessionmode|standalone|quiz-as-source|trivia session|session source' \
    app/lib/sessions.ts app/actions/sessions.ts app/dashboard/SessionOperatorPanel.tsx
  ```
  (The only matches elsewhere are the two explanatory `// … session source …` code comments in
  `sessions.ts:55` and `SessionOperatorPanel.tsx:321`, which describe the mission-only model —
  keep them.)

**Gate**
- No code change in this phase.
- All three checks pass; any hit that frames session creation as trivia/quiz/mode-based is
  escalated into Phase 2 as a rewrite.

---

### Phase 2 — Surface the 422 eligibility rejection

**Scope**
- Map `422` in `createSession` (`app/lib/sessions.ts`) to `Error('mission_not_eligible')`, so
  the readiness/eligibility rejection reaches the existing panel banner regardless of whether
  the backend returns 409 or 422.

**`app/lib/sessions.ts` change** — add a `422` branch alongside the existing `409` handling in
`createSession` (currently `sessions.ts:54–60`):
```ts
  if (response.status === 404) throw new Error('mission_not_found')
  if (response.status === 409 || response.status === 422) {
    // Mission is the only session source now, so the eligibility/readiness rejection can arrive
    // as either 409 or 422 (mission inactive / not runtime-ready). Both map to one message.
    throw new Error('mission_not_eligible')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createSession failed with status ${response.status}`)
  }
```
This replaces the current 409-only block. The prior `problem.type` inspection is dropped: both
its branches already threw the same `'mission_not_eligible'`, so reading the body added nothing.

No change needed in `SessionOperatorPanel.tsx` — `handleCreate` (`:144`) already renders the
"inactive or not runtime-ready" banner for `message === 'mission_not_eligible'`, and
`app/actions/sessions.ts:39` passes the lib error straight through.

**Gate**
- `pnpm build` passes with no type errors.
- A `createSession` call whose backend responds `422` throws `mission_not_eligible` (not
  `unknown`), so the panel shows the eligibility banner and keeps the form intact.
- The existing `409` path (`sessions.spec.ts:160`) is unaffected.

---

### Phase 3 — Guard tests (session-creation surface)

**Scope**
- Add to `tests/e2e/sessions.spec.ts`:
  1. a 422-not-eligible test (parallel to the existing 409 test at `:160`);
  2. a copy guard asserting the admin session-creation surface carries no `SessionMode` /
     `SessionSource` / standalone-trivia / quiz-as-source text, mirroring
     `mission-hierarchy.spec.ts:238`.

**422 rejection test** (mirror the 409 test's Server-Action route intercept at `:160`):
```ts
test('session form shows the eligibility banner on 422 and keeps the form intact', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  await page.route('**/dashboard', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      const body = await request.postData()
      if (body && body.includes('maximumTimeMinutes')) {
        await route.fulfill({
          status: 422,
          body: JSON.stringify({ type: 'mission-not-eligible-for-session', detail: 'Not ready.' }),
        })
        return
      }
    }
    await route.continue()
  })

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  await missionSelect.selectOption(
    (await missionSelect
      .locator('option:not([value=""]):not([disabled])')
      .first()
      .getAttribute('value'))!,
  )

  await page.fill('[data-testid="session-title-input"]', '422 Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  const banner = page.locator('[data-testid="session-form-error"]')
  await expect(banner).toBeVisible()
  await expect(banner).toContainText('runtime-ready') // the eligibility message, not the generic one
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})
```

**No-trivia-framing guard** (mirror `mission-hierarchy.spec.ts:238`, scoped to the create panel):
```ts
test('no SessionMode / quiz-as-source / standalone-trivia copy in the session-creation UI', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const panel = page.locator('[data-testid="session-operator-panel"]')
  await expect(panel).toBeVisible()
  const text = (await panel.innerText()).toLowerCase()

  expect(text).not.toContain('sessionmode')
  expect(text).not.toContain('session mode')
  expect(text).not.toContain('quiz')
  expect(text).not.toContain('standalone')
  // the only source control is the mission select
  await expect(page.locator('[data-testid="session-mission-select"]')).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test tests/e2e/sessions.spec.ts` passes, including both new tests.
- No regression in the existing sessions / mission-hierarchy / trivias specs.

---

## Commit Sequence

```
test(frontend): phase 1 — verify session creation is mission-only (HU-16)
feat(frontend): phase 2 — surface 422 mission-eligibility rejection on session create (HU-16)
test(frontend): phase 3 — guard session-creation surface against trivia/quiz/SessionMode copy (HU-16)

Ref: HU-16
```

(Phase 1 carries no product diff; fold it into the Phase 2 commit if a code-free commit is
undesirable.)

---

## Gate Summary (maps to the task's stated gates)

- **Typecheck/build passes** — `pnpm build`, Phase 2 & 3 gates.
- **Create-from-mission exercised incl. not-eligible rejection** — happy path already at
  `sessions.spec.ts:110`; 409 at `:160`; 422 added in Phase 3.
- **No UI type/copy offers a standalone trivia-session / quiz-as-source creation path** —
  Phase 1 grep + Phase 3 panel guard.
- **No UI type/copy introduces or retains `SessionMode`** — Phase 1 grep (0 hits under `app/`)
  + Phase 3 panel guard.

---

## Open Questions

- **Which status code does `POST /api/sessions` actually return for an ineligible mission —
  409, 422, or both (by cause)?** The task states 409/422; the current lib handles only 409.
  Phase 2 maps both to `mission_not_eligible`, which is safe under any of the three answers, so
  this does not block implementation — but if backend integration tests pin one specific code
  per cause, mirror that here. (Backend not inspected — out of scope for this step.)

---

## Out of Scope

- **Backend changes** — explicitly excluded.
- **Admin-overview demo scaffolding** (`DashboardClient.tsx:75` `const sessions: Session[]`,
  the `Live sessions` panel at `:1093`, "Active trivia sessions" at `:1097`, "Trivia night
  overview"). This is hardcoded mockup content on the admin landing view, not the session
  creation path and not wired to the real session model. It does not offer a
  standalone-trivia-session **creation** affordance, so it passes the gates as-is. De-triviafying
  or removing that mock overview is a separate cosmetic cleanup.
- **The `TriviaQuiz*` DTOs / substage trivia authoring** (`definitions.ts:101,222,233`, the
  mission tree, `SubstageEditor`). Trivia is authored into a mission substage upstream; these
  types are correct and unrelated to session creation.
- **Any runtime trivia-round UI** (`TriviaRoundPanel`, `use-trivia-round-state`). That is live
  operation of an already-created session, not session creation.
</content>
</invoke>
