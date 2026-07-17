import { test, expect } from '../fixtures/auth'

// --- Catalog view ---

test('admin sees missions nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-missions"]')).toBeVisible()
})

test('operator does not see missions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-missions"]')).toHaveCount(0)
})

test('admin missions panel loads with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-mission-btn"]')).toBeVisible()
})

// --- Create flow ---

test('admin can create a mission and land on its detail view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()

  await page.fill('[data-testid="mission-name-input"]', 'Test Mission Alpha')
  await page.fill('[data-testid="mission-description-input"]', 'Navigate to the relay point.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Advanced')
  await page.fill('[data-testid="mission-time-input"]', '45')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText('Test Mission Alpha')
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')
  // RF-02: the entered maximum time must survive the full round trip, not merely render as some time.
  await expect(page.locator('[data-testid="mission-detail-time"]')).toContainText('45 min')
})

test('create form cancel returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-form"]')).toHaveCount(0)
})

// --- Detail/Edit flow ---

test('admin can open mission detail from catalog row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // Use nth(1) to avoid the first row which may be inactive from prior runs
  const viewBtn = page.locator('[data-testid^="view-mission-btn-"]').nth(1)
  await expect(viewBtn).toBeVisible()
  await viewBtn.click()

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-difficulty"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-time"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="mission-detail-status"]')).not.toBeEmpty()
})

test('admin can edit a mission and changes are reflected in detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // Create a fresh mission to ensure we have an active one to edit
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Edit Target Mission')
  await page.fill('[data-testid="mission-description-input"]', 'To be edited.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Intermediate')
  await page.fill('[data-testid="mission-time-input"]', '30')
  await page.click('[data-testid="mission-submit-btn"]')

  // Now on detail view, click Edit
  await page.click('[data-testid="edit-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
  await page.fill('[data-testid="mission-name-input"]', 'Updated Mission Name')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText('Updated Mission Name')
})

test('edit form cancel returns to detail without saving', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // Create a fresh mission to ensure we have an active one
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Cancel Edit Mission')
  await page.fill('[data-testid="mission-description-input"]', 'To be cancelled.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Beginner')
  await page.fill('[data-testid="mission-time-input"]', '15')
  await page.click('[data-testid="mission-submit-btn"]')

  const originalName = await page.locator('[data-testid="mission-detail-name"]').innerText()

  await page.click('[data-testid="edit-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Should Not Be Saved')
  await page.locator('[data-testid="mission-form"] button[type="button"]').click()

  await expect(page.locator('[data-testid="mission-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText(originalName.trim())
})

// --- Deactivate flow ---

// Deactivation is terminal: it retires a mission, keeping its history (HU-09.3) and barring it from
// new sessions (HU-09.4). Neither editing nor reactivation is offered afterwards, so a retired
// mission cannot drift from the record sessions were built on. This is a panel-level rule — the
// domain's UpdateMission has no active-state guard — so it is asserted here or nowhere.
test('admin cannot edit a mission once it has been deactivated', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Retire Target Mission')
  await page.fill('[data-testid="mission-description-input"]', 'To be retired.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Intermediate')
  await page.fill('[data-testid="mission-time-input"]', '30')
  await page.click('[data-testid="mission-submit-btn"]')

  // Editable while it is still live.
  await expect(page.locator('[data-testid="edit-mission-btn"]')).toBeEnabled()

  await page.click('[data-testid="deactivate-mission-btn"]')
  await page.click('[data-testid="confirm-deactivate-mission-btn"]')
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Inactive')

  // Retired: no edit, and no way back into play.
  await expect(page.locator('[data-testid="edit-mission-btn"]')).toBeDisabled()
  await expect(page.locator('[data-testid="activate-mission-btn"]')).toHaveCount(0)
})

test('admin deactivate flow shows confirm step then marks mission inactive', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // Create a fresh mission to ensure we have an active one to deactivate
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Deactivate Target Mission')
  await page.fill('[data-testid="mission-description-input"]', 'To be deactivated.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Advanced')
  await page.fill('[data-testid="mission-time-input"]', '60')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="deactivate-mission-btn"]')).toBeVisible()
  await page.click('[data-testid="deactivate-mission-btn"]')
  await expect(page.locator('[data-testid="confirm-deactivate-mission-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-deactivate-mission-btn"]')

  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Inactive')
  await expect(page.locator('[data-testid="deactivate-mission-btn"]')).toHaveCount(0)
})

test('admin deactivate confirm cancel dismisses without change', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // Create a fresh mission to ensure we have an active one
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Cancel Deactivate Mission')
  await page.fill('[data-testid="mission-description-input"]', 'Cancel me.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Beginner')
  await page.fill('[data-testid="mission-time-input"]', '10')
  await page.click('[data-testid="mission-submit-btn"]')

  await page.click('[data-testid="deactivate-mission-btn"]')
  await page.locator('button:has-text("Cancel")').last().click()

  await expect(page.locator('[data-testid="confirm-deactivate-mission-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="mission-detail-status"]')).toHaveText('Draft')
})

// --- Activate flow ---

test('admin can activate a runtime-ready draft mission', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // The seeded "E2E Activatable Mission" is Draft but has a complete runtime plan.
  const row = page.locator('[data-testid^="mission-row-"]', { hasText: 'E2E Activatable Mission' })
  await row.locator('[data-testid^="view-mission-btn-"]').click()

  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText('E2E Activatable Mission')
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')

  // ActivationBar loads readiness asynchronously — wait for the button to be enabled.
  await expect(page.locator('[data-testid="activate-mission-btn"]')).toBeEnabled({ timeout: 10000 })

  await page.click('[data-testid="activate-mission-btn"]')

  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Ready')
  await expect(page.locator('[data-testid="activate-mission-btn"]')).toHaveCount(0)
})

test('activating a mission with no runtime plan surfaces readiness errors', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')

  // A freshly created mission has no stages, so it cannot be activated yet.
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', 'Unready Mission')
  await page.fill('[data-testid="mission-description-input"]', 'No stages yet.')
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Beginner')
  await page.fill('[data-testid="mission-time-input"]', '20')
  await page.click('[data-testid="mission-submit-btn"]')

  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')

  // ActivationBar loads readiness asynchronously — wait for failures to appear.
  await expect(page.locator('[data-testid="readiness-failure"]').first()).toBeVisible({ timeout: 10000 })
  // Button is disabled because readiness fails.
  await expect(page.locator('[data-testid="activate-mission-btn"]')).toBeDisabled()
  // Mission stays Draft.
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')
})

// --- Status badge in catalog ---

test('mission status chip reflects activation state', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  // At least one row must have a status chip
  await expect(page.locator('[data-testid^="mission-status-"]').first()).toBeVisible()
})

// --- Regression guard ---

test('HU-01 admin session is not broken by missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('admin')
  await expect(page.locator('[data-testid="admin-panel"]')).toBeVisible()
})

test('HU-02 users panel still reachable for admin after missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid="users-panel"]')).toBeVisible()
})

test('HU-04 teams panel still reachable for admin after missions wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
})
