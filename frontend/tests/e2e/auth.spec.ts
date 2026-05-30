import { test, expect } from '../fixtures/auth'

test('unauthenticated user is redirected to login', async ({ page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login')
})

test('operator sees operator dashboard', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toHaveText('operator')
  await expect(page.locator('[data-testid="operator-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="admin-panel"]')).toHaveCount(0)
})

test('admin sees admin dashboard', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toHaveText('admin')
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="operator-panel"]')).toHaveCount(0)
})

test('deactivated user is redirected to deactivated error', async ({ deactivatedPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login?error=deactivated')
  await expect(page.locator('[data-testid="deactivated-chip"]')).toBeVisible()
})

test('logout clears session and redirects to login', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="logout-button"]')
  await expect(page).toHaveURL('/login')
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/login')
})
