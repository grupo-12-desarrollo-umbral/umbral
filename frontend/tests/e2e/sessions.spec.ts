import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('operator sees sessions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('admin sees sessions nav item for operator assignment', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('participant does not see sessions nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
})

// --- Panel rendering ---

test('operator sessions panel is read-only and has no create form', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
  // Creation is now admin-only; operators only operate assigned sessions.
  await expect(page.locator('[data-testid="session-create-form"]')).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'My sessions' })).toBeVisible()
  await expect(page.getByText(/Operate the sessions an administrator has assigned to you/)).toBeVisible()
  await expect(page.getByText(`Sessions you're responsible for`)).toBeVisible()
})

test('admin sessions panel loads and shows the create form', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-operator-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Create session' })).toBeVisible()
})

// --- Submit button disabled state ---

test('session submit button is disabled when no mission is selected', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  // On initial load no mission is chosen yet, so creation is blocked.
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
})

test('session submit button is disabled until all fields are filled', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  // Select a runtime-ready mission but leave everything else empty. Not-ready (Draft)
  // missions render disabled, so we explicitly target the first enabled option.
  const firstMission = missionSelect.locator('option:not([value=""]):not([disabled])').first()
  const missionValue = await firstMission.getAttribute('value')
  if (missionValue) await missionSelect.selectOption(missionValue)
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill title — still missing scheduledAt
  await page.fill('[data-testid="session-title-input"]', 'Test Session')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill scheduledAt — now all fields are filled
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-01T10:00')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeEnabled()
})

// --- Runtime-readiness gating in the dropdown ---

test('not-runtime-ready missions appear disabled with a reason', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()

  // The Draft seed mission ('E2E Not-Ready Mission') is active but not runtime-ready,
  // so it is shown for visibility but disabled and labelled with the reason — the admin
  // can't pick a mission that would 409 at create time. (Uses the dedicated always-Draft
  // fixture, not 'E2E Activatable Mission', which the activate test flips to Ready mid-run.)
  const draftOption = missionSelect.locator('option', { hasText: 'E2E Not-Ready Mission' })
  await expect(draftOption).toBeDisabled()
  await expect(draftOption).toContainText('not runtime-ready')

  // The Ready seed mission stays selectable and is not annotated.
  const readyOption = missionSelect.locator('option', { hasText: 'E2E Seed Mission' })
  await expect(readyOption).toBeEnabled()
  await expect(readyOption).not.toContainText('not runtime-ready')
})

test('runtime-ready missions are listed before not-runtime-ready missions', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  // waitFor() resolves when the <select> attaches, before the async mission fetch
  // populates options — wait for the seed option itself before reading the list.
  await missionSelect.locator('option', { hasText: 'E2E Seed Mission' }).waitFor({ state: 'attached' })

  const options = missionSelect.locator('option:not([value=""])')
  const optionTexts = await options.allTextContents()
  const readyIndex = optionTexts.findIndex((text) => text.includes('E2E Seed Mission'))
  // Dedicated always-Draft fixture — 'E2E Activatable Mission' gets activated mid-run by
  // missions.spec.ts, so it can't be relied on to stay not-runtime-ready here.
  const draftIndex = optionTexts.findIndex((text) => text.includes('E2E Not-Ready Mission'))

  expect(readyIndex).toBeGreaterThanOrEqual(0)
  expect(draftIndex).toBeGreaterThanOrEqual(0)
  expect(readyIndex).toBeLessThan(draftIndex)
})

// --- Happy path (AC #2 + AC #3) ---

test('admin can create a session from an active mission and it appears in the assignment list', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  const firstMissionOption = missionSelect.locator('option:not([value=""]):not([disabled])').first()
  await missionSelect.selectOption((await firstMissionOption.getAttribute('value'))!)

  await page.fill('[data-testid="session-title-input"]', 'E2E Mission Night')
  await page.fill('[data-testid="session-max-time-input"]', '45')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-15T18:00')

  await page.click('[data-testid="session-submit-btn"]')

  // On success the form clears and the new (unassigned) session shows in the list,
  // rendered in its initial Scheduled state.
  const list = page.locator('[data-testid="session-operator-list"]')
  await expect(list).toContainText('E2E Mission Night')
  await expect(list).toContainText('Scheduled')
  await expect(page.locator('[data-testid="session-title-input"]')).toHaveValue('')
})

// --- Reset flow ---

test('admin create form clears after a successful creation', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  await missionSelect.selectOption((await missionSelect.locator('option:not([value=""]):not([disabled])').first().getAttribute('value'))!)

  await page.fill('[data-testid="session-title-input"]', 'First Session')
  await page.fill('[data-testid="session-max-time-input"]', '20')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-22T14:00')
  await page.click('[data-testid="session-submit-btn"]')

  // Form resets and stays visible for the next creation.
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-title-input"]')).toHaveValue('')
})

// AC #4 — 409 surfaced without broken screen
// This test intercepts the POST at the network level to simulate a 409.

test('session form shows error banner on 409 and keeps form intact', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  // Intercept the Server Action's outbound POST to session-operations.
  // Next encodes the action arguments (not the function name) in the POST-to-/dashboard body,
  // so we match the createSession payload by a field unique to it (`maximumTimeMinutes`).
  await page.route('**/dashboard', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      const body = await request.postData()
      if (body && body.includes('maximumTimeMinutes')) {
        await route.fulfill({ status: 409, body: JSON.stringify({ detail: 'Conflict.' }) })
        return
      }
    }
    await route.continue()
  })

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  await missionSelect.selectOption((await missionSelect.locator('option:not([value=""]):not([disabled])').first().getAttribute('value'))!)

  await page.fill('[data-testid="session-title-input"]', '409 Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-form-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})

// --- HU-11/12 regression ---

test('HU-11 trivias panel still renders for admin after sessions integration', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
})

// --- HU-03 regression ---

test('HU-03 operator role chip is visible and correct', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('operator')
})

// --- HU-16 — create form stays intact and surfaces an error when the create fails ---
// This drives the server-action seam (browser -> POST /dashboard), so it locks the UI's
// failure handling (error banner shown, form not torn down), NOT the server-side status
// mapping. The 409/422 -> mission_not_eligible mapping in app/lib/sessions.ts runs inside
// the server action against the gateway and is locked in tests/unit/app/lib/sessions.test.ts.

test('session create form surfaces an error banner and stays intact when the create fails', async ({
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
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})

// --- HU-16 — Canon guard: no trivia/quiz/SessionMode copy in creation UI ---

test('no SessionMode / quiz-as-source / standalone-trivia copy in the session-creation UI', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  // Scope to the create form only — the session list below it is data-driven and
  // may contain session titles with "quiz" / "trivia" from previous test runs.
  const form = page.locator('[data-testid="session-create-form"]')
  await expect(form).toBeVisible()

  // Remove the mission <select> from a DOM clone before reading text.
  // The select options are data-driven; a seeded mission name could contain "quiz".
  const text = await form.evaluate((el) => {
    const clone = el.cloneNode(true) as HTMLElement
    const select = clone.querySelector('[data-testid="session-mission-select"]')
    if (select) select.remove()
    return clone.innerText.toLowerCase()
  })

  expect(text).not.toContain('sessionmode')
  expect(text).not.toContain('session mode')
  expect(text).not.toContain('quiz')
  expect(text).not.toContain('standalone')
  // the only source control is the mission select
  await expect(page.locator('[data-testid="session-mission-select"]')).toBeVisible()
})
