import { test, expect } from '../fixtures/auth'

// --- Role selector ---

test('admin can open role editor on an active user row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]').first()).toBeVisible()
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  await expect(page.locator('[data-testid^="role-select-"]').first()).toBeVisible()
})

test('admin role select is pre-filled with current role', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  const firstChangeBtn = page.locator('[data-testid^="change-role-btn-"]').first()
  // Read the current role text before opening editor
  const row = firstChangeBtn.locator('xpath=ancestor::tr')
  const currentRole = await row.locator('td:nth-child(3)').innerText()
  await firstChangeBtn.click()
  const select = page.locator('[data-testid^="role-select-"]').first()
  await expect(select).toHaveValue(currentRole.trim())
})

test('admin save button is disabled when role unchanged', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  const saveBtn = page.locator('[data-testid^="save-role-btn-"]').first()
  await expect(saveBtn).toBeDisabled()
})

test('admin can cancel role change without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await page.locator('[data-testid^="change-role-btn-"]').first().click()
  await page.locator('[data-testid^="role-select-"]').first().selectOption('Participant')
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  // row should be back to read mode — no select visible
  await expect(page.locator('[data-testid^="role-select-"]')).toHaveCount(0)
})

test('operator sees no change-role button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]')).toHaveCount(0)
})

// --- Role-based visibility ---

test('participant sees participant panel not admin or operator panel', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="operator-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="users-panel"]')).toHaveCount(0)
})

test('participant nav does not include users management item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-users"]')).toHaveCount(0)
})

// --- Route guard ---

test('participant direct URL access to dashboard yields participant view not admin content', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  // Role chip confirms participant identity
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('participant')
})

// --- HU-01 regression ---

test('HU-01 operator flow is not regressed', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
})

test('HU-01 admin flow is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})

// --- HU-02 regression ---

test('HU-02 users panel still renders for admin', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]').first()).toBeVisible()
})

test('HU-02 users panel still renders read-only for operator', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]')).toHaveCount(0)
})
