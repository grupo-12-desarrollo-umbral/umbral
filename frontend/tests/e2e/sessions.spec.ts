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

// AC #1 — only Published quizzes appear

test('session quiz selector contains only published quizzes', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const select = page.locator('[data-testid="session-quiz-select"]')
  await expect(select).toBeVisible()
  // All non-placeholder options must correspond to Published quizzes.
  // Verify there are no options with "Draft" or "Archived" in their text
  // (relies on the test environment having known fixture quizzes).
  const options = select.locator('option:not([value=""])')
  const count = await options.count()
  for (let i = 0; i < count; i++) {
    const text = await options.nth(i).textContent()
    expect(text).not.toContain('Draft')
    expect(text).not.toContain('Archived')
  }
})

// --- Submit button disabled state ---

test('session submit button is disabled when no quiz is selected', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
})

test('session submit button is disabled until all fields are filled', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  // Select a mission but leave everything else empty
  const firstMission = missionSelect.locator('option:not([value=""])').first()
  const missionValue = await firstMission.getAttribute('value')
  if (missionValue) await missionSelect.selectOption(missionValue)
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Select a quiz but leave title empty
  const quizSelect = page.locator('[data-testid="session-quiz-select"]')
  const firstQuiz = quizSelect.locator('option:not([value=""])').first()
  const quizValue = await firstQuiz.getAttribute('value')
  if (quizValue) await quizSelect.selectOption(quizValue)
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill title — still missing scheduledAt
  await page.fill('[data-testid="session-title-input"]', 'Test Session')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill scheduledAt — now all fields are filled
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-01T10:00')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeEnabled()
})

// --- Happy path (AC #2 + AC #3) ---

test('admin can create a session from a published quiz and it appears in the assignment list', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  const firstMissionOption = missionSelect.locator('option:not([value=""])').first()
  await missionSelect.selectOption((await firstMissionOption.getAttribute('value'))!)

  const quizSelect = page.locator('[data-testid="session-quiz-select"]')
  const firstQuizOption = quizSelect.locator('option:not([value=""])').first()
  const optionValue = await firstQuizOption.getAttribute('value')
  expect(optionValue).not.toBeNull()
  await quizSelect.selectOption(optionValue!)

  await page.fill('[data-testid="session-title-input"]', 'E2E Trivia Night')
  await page.fill('[data-testid="session-max-time-input"]', '45')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-15T18:00')

  await page.click('[data-testid="session-submit-btn"]')

  // On success the form clears and the new (unassigned) session shows in the list.
  await expect(page.locator('[data-testid="session-operator-list"]')).toContainText('E2E Trivia Night')
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
  await missionSelect.selectOption((await missionSelect.locator('option:not([value=""])').first().getAttribute('value'))!)

  const quizSelect = page.locator('[data-testid="session-quiz-select"]')
  await quizSelect.selectOption((await quizSelect.locator('option:not([value=""])').first().getAttribute('value'))!)
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
  // In Playwright, intercept the Next.js server action call (POST to /dashboard).
  await page.route('**/dashboard', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      const body = await request.postData()
      if (body && body.includes('createTriviaSession')) {
        await route.fulfill({ status: 409, body: JSON.stringify({ detail: 'Quiz is not published.' }) })
        return
      }
    }
    await route.continue()
  })

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  await missionSelect.selectOption((await missionSelect.locator('option:not([value=""])').first().getAttribute('value'))!)

  const quizSelect = page.locator('[data-testid="session-quiz-select"]')
  await quizSelect.selectOption((await quizSelect.locator('option:not([value=""])').first().getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', '409 Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-form-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})

// AC4 — mission-not-eligible-for-session surfaces the correct copy
test('session form shows mission-not-eligible error on 409 with the mission type', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  await page.route('**/dashboard', async (route) => {
    const request = route.request()
    if (request.method() === 'POST') {
      const body = await request.postData()
      if (body && body.includes('createTriviaSession')) {
        await route.fulfill({
          status: 409,
          body: JSON.stringify({ type: 'mission-not-eligible-for-session', detail: 'Mission 1 is inactive.' }),
        })
        return
      }
    }
    await route.continue()
  })

  const missionSelect = page.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  await missionSelect.selectOption((await missionSelect.locator('option:not([value=""])').first().getAttribute('value'))!)

  const quizSelect = page.locator('[data-testid="session-quiz-select"]')
  await quizSelect.selectOption((await quizSelect.locator('option:not([value=""])').first().getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', 'Mission Error Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-form-error"]')).toContainText('inactive or not runtime-ready')
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
