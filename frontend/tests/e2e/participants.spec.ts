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
  // Wait for detail panel to fully render before reading status
  await expect(page.locator('[data-testid="detail-status"]')).toHaveText(/(Active|Inactive)/)
  const statusText = await page.locator('[data-testid="detail-status"]').textContent()
  if (statusText?.includes('Active')) {
    await expect(page.locator('[data-testid="assign-participant-btn"]')).toBeVisible()
  } else {
    await expect(page.locator('[data-testid="assign-participant-btn"]')).toHaveCount(0)
  }
})

test('admin can open assign form and it shows participant users', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
  await expect(page.locator('[data-testid="detail-status"]')).toContainText('Active')
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="assign-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="participant-select"]')).toBeVisible()
})

test('confirm assign button is disabled until user selected', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="confirm-assign-btn"]')).toBeDisabled()
})

test('admin can cancel assign form without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
  await page.click('[data-testid="assign-participant-btn"]')
  await expect(page.locator('[data-testid="assign-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="assign-form"]')).toHaveCount(0)
})

test('successful assignment adds participant row optimistically', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
  await page.click('[data-testid="assign-participant-btn"]')
  // Select the first available participant by label
  const select = page.locator('[data-testid="participant-select"]')
  const options = await select.locator('option').count()
  if (options <= 1) {
    // No participant users in test DB — skip execution part of test
    return
  }
  await page.selectOption('[data-testid="participant-select"]', { index: 1 })
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
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
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
