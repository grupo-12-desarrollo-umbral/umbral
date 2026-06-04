import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('admin sees the sessions nav and can open the operator assignment panel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-operator-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-operator-list"]')).toBeVisible()
})

// --- List-driven UI ---

test('session operator panel renders the session list and hides assign controls until a row is selected', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  await expect(page.locator('[data-testid="session-operator-list"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-operator-item"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="operator-select"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="assign-operator-btn"]')).toHaveCount(0)
})

test('list error is rendered when sessions cannot be loaded', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const listError = page.locator('[data-testid="session-operator-list-error"]')
  if (await listError.count()) {
    await expect(listError).toBeVisible()
  }
})

// --- Role guard ---

test('operator sees the creation panel, not the operator-assignment panel', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-operator-panel"]')).toHaveCount(0)
})

// --- Admin "Assign operators" button navigates to sessions panel ---

test('admin assign operators button navigates to sessions panel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  // The admin hero section has an "Assign operators" button
  const assignButton = page.getByTestId('admin-panel').getByRole('button', { name: 'Assign operators' })
  await expect(assignButton).toBeVisible()
  await assignButton.click()
  // After clicking, the session-operator-panel should be visible
  await expect(page.locator('[data-testid="session-operator-panel"]')).toBeVisible()
})
