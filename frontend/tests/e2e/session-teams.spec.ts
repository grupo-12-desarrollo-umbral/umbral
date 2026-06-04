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
