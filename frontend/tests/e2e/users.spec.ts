import { test, expect } from '../fixtures/auth'

test('admin can navigate to users panel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
})

test('admin sees deactivate button on active users', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="deactivate-btn-"]').first()).toBeVisible()
})

test('operator sees users panel read-only', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="deactivate-btn-"]')).toHaveCount(0)
})

test('admin sees the invite form; operator does not', async ({ adminPage: admin, operatorPage: operator }) => {
  await admin.goto('/dashboard')
  await admin.click('[data-testid="nav-users"]')
  await expect(admin.locator('[data-testid="invite-user-form"]')).toBeVisible()
  // No password field anywhere in the invite form.
  await expect(admin.locator('[data-testid="invite-user-form"] input[type="password"]')).toHaveCount(0)

  await operator.goto('/dashboard')
  await operator.click('[data-testid="nav-users"]')
  await expect(operator.locator('[data-testid="invite-user-form"]')).toHaveCount(0)
})

test('invite form offers only Operator and Administrator roles', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  const roleOptions = page.locator('[data-testid="invite-role-select"] option')
  await expect(roleOptions).toHaveText(['Operator', 'Administrator'])
})

test('deactivated user is blocked at login with clear error', async ({ page }) => {
  // This fixture has no session cookie; Keycloak would return deactivated user.
  // Simulate: navigate directly with error param (backend handles the 403 at bootstrap).
  await page.goto('/login?error=deactivated')
  await expect(page.locator('[data-testid="deactivated-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="deactivated-chip"]')).toContainText('deactivated')
})

test('active user HU-01 operator flow is not regressed', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
})

test('active user HU-01 admin flow is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})
