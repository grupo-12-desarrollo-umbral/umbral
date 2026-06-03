import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('operator sees sessions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('admin does not see sessions nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
})

test('participant does not see sessions nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
})

// --- Panel rendering ---

test('operator sessions panel loads and shows create form', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})

// AC #1 — only Published quizzes appear

test('session quiz selector contains only published quizzes', async ({ operatorPage: page }) => {
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

test('session submit button is disabled when no quiz is selected', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
})

test('session submit button is disabled until all fields are filled', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  // Select a quiz but leave title empty
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  if (optionValue) await select.selectOption(optionValue)
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill title — still missing scheduledAt
  await page.fill('[data-testid="session-title-input"]', 'Test Session')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill scheduledAt
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-01T10:00')
  // Now submit should be enabled
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeEnabled()
})

// --- Happy path (AC #2 + AC #3) ---

test('operator can create a session from a published quiz and sees created card', async ({
  operatorPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  expect(optionValue).not.toBeNull()
  await select.selectOption(optionValue!)

  await page.fill('[data-testid="session-title-input"]', 'E2E Trivia Night')
  await page.fill('[data-testid="session-max-time-input"]', '45')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-15T18:00')

  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-created-card"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-title"]')).toContainText('E2E Trivia Night')
  await expect(page.locator('[data-testid="session-state"]')).toContainText('Scheduled')
})

// AC #3 — created session shows source quiz binding

test('created session card shows source quiz binding', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  await select.selectOption(optionValue!)
  await page.fill('[data-testid="session-title-input"]', 'Binding Check')
  await page.fill('[data-testid="session-max-time-input"]', '30')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-20T09:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-source-quiz-id"]')).toHaveText(optionValue!)
  await expect(page.locator('[data-testid="session-code"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="session-question-count"]')).not.toBeEmpty()
})

// --- Reset flow ---

test('operator can create another session after a successful creation', async ({
  operatorPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  await select.selectOption((await firstOption.getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', 'First Session')
  await page.fill('[data-testid="session-max-time-input"]', '20')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-22T14:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-created-card"]')).toBeVisible()

  await page.click('[data-testid="session-create-another-btn"]')

  // Form resets and is visible
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-card"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="session-title-input"]')).toHaveValue('')
})

// AC #4 — 409 surfaced without broken screen
// This test intercepts the POST at the network level to simulate a 409.

test('session form shows error banner on 409 and keeps form intact', async ({
  operatorPage: page,
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
        await route.fulfill({ status: 409, body: JSON.stringify({ type: 'quiz_not_published' }) })
        return
      }
    }
    await route.continue()
  })

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  await select.selectOption((await firstOption.getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', '409 Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-form-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-card"]')).toHaveCount(0)
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
