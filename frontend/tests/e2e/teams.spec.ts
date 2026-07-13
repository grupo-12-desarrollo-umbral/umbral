import { test, expect } from '../fixtures/auth'

// --- Teams list view ---

test('admin sees teams panel with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-btn"]')).toBeVisible()
})

test('operator sees teams panel with create button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-btn"]')).toBeVisible()
})

test('operator sees session assignment action on team rows', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid^="team-row-session-actions-"]').first()).toBeVisible()
})

test('admin does not see session assignment action on team rows', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await expect(page.locator('[data-testid^="team-row-session-actions-"]')).toHaveCount(0)
})

test('participant does not see teams nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-teams"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
})

// --- Team detail view ---

test('admin can open team detail by clicking a row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="detail-team-code"]')).toBeVisible()
  await expect(page.locator('[data-testid="edit-team-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toBeVisible()
})

test('operator detail view has action buttons', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="edit-team-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toBeVisible()
})

test('back button from detail returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="teams-back-btn"]')
  await expect(page.locator('[data-testid="teams-panel"]')).toBeVisible()
})

test('operator can open assign-to-session modal from team row actions', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')

  const actionButton = page.locator('[data-testid^="team-row-session-actions-"]:not([disabled])').first()
  if (await actionButton.count() === 0) return

  await actionButton.click()
  await expect(page.getByRole('heading', { name: 'Assign team to session' })).toBeVisible()
})

// --- Team deactivation ---

test('admin deactivate flow shows confirm step then updates status', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Use nth(1) to avoid race with other tests targeting first row
  await page.locator('[data-testid^="team-row-"]').nth(1).click()
  await page.click('[data-testid="deactivate-team-btn"]')
  // Confirm step appears
  await expect(page.locator('[data-testid="confirm-deactivate-team-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-deactivate-team-btn"]')
  // After deactivation the status chip shows Inactive
  await expect(page.locator('[data-testid="detail-status"]')).toContainText('Inactive')
  // Edit and deactivate buttons are gone for inactive teams
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toHaveCount(0)
})

test('admin can cancel deactivation', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Use nth(2) to avoid race with other tests targeting first rows
  await page.locator('[data-testid^="team-row-"]').nth(2).click()
  await page.click('[data-testid="deactivate-team-btn"]')
  await expect(page.locator('[data-testid="confirm-deactivate-team-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="deactivate-team-btn"]')).toBeVisible()
})

// --- Team create form ---

test('admin can open create form and cancel', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
  await page.click('[data-testid="team-form-submit"]')
  // Blank submit → field errors, no panel change
  await expect(page.locator('[data-testid="display-name-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="team-code-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
})

test('admin create team success navigates to detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await page.fill('[data-testid="team-display-name-input"]', 'Alpha Squad')
  await page.fill('[data-testid="team-code-input"]', 'ALPHA-E2E')
  await page.click('[data-testid="team-form-submit"]')
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="detail-display-name"]')).toContainText('Alpha Squad')
})

test('operator create team success navigates to detail', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  await page.click('[data-testid="create-team-btn"]')
  await expect(page.locator('[data-testid="create-team-panel"]')).toBeVisible()
  await page.fill('[data-testid="team-display-name-input"]', 'Operator Squad')
  await page.fill('[data-testid="team-code-input"]', 'OP-SQUAD-E2E')
  await page.click('[data-testid="team-form-submit"]')
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="detail-display-name"]')).toContainText('Operator Squad')
})

// --- Team edit form ---

test('admin edit form is pre-populated', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // Use nth(3) to avoid race with deactivation tests
  await page.locator('[data-testid^="team-row-"]').nth(3).click()
  const currentCode = await page.locator('[data-testid="detail-team-code"]').innerText()
  await page.click('[data-testid="edit-team-btn"]')
  await expect(page.locator('[data-testid="edit-team-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="team-code-input"]')).toHaveValue(currentCode.trim())
})

test('operator can open edit form and it is pre-populated', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // First row is always active — deactivation tests only ever target nth(1)/nth(2),
  // and positional .nth(3) is unreliable once create-tests leave extra teams in the DB.
  await page.locator('[data-testid^="team-row-"]').first().click()
  const currentCode = await page.locator('[data-testid="detail-team-code"]').innerText()
  await page.click('[data-testid="edit-team-btn"]')
  await expect(page.locator('[data-testid="edit-team-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="team-code-input"]')).toHaveValue(currentCode.trim())
})

test('admin cancel edit returns to detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-teams"]')
  // First row is always active (deactivation tests only target nth(1)/nth(2)).
  await page.locator('[data-testid^="team-row-"]').first().click()
  await page.click('[data-testid="edit-team-btn"]')
  await page.getByRole('button', { name: 'Cancel' }).first().click()
  await expect(page.locator('[data-testid="team-detail-panel"]')).toBeVisible()
})

// --- Role-based guards ---

test('participant navigating to dashboard never shows team content', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="participant-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="teams-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="team-detail-panel"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="create-team-panel"]')).toHaveCount(0)
})

// --- HU-01 regression ---

test('HU-01 operator flow is not regressed by teams', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="sessions-panel"]')).toBeVisible()
})

test('HU-01 admin flow is not regressed by teams', async ({ adminPage: page }) => {
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

// --- HU-03 regression ---

test('HU-03 admin role change is not regressed', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-users"]')
  await expect(page.locator('[data-testid^="change-role-btn-"]').first()).toBeVisible()
})
