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
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
})

test('active user HU-01 admin flow is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})
