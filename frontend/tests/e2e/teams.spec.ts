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

test('operator associates a team to a scheduled session end-to-end', async ({
  adminPage: admin,
  operatorPage: operator,
}) => {
  // Set up a scheduled session assigned to the operator (op-1 / "Operator One"),
  // so it shows up as an assignable session in the team row's "assign to session" modal.
  await admin.goto('/dashboard')
  await admin.click('[data-testid="nav-sessions"]')

  const missionSelect = admin.locator('[data-testid="session-mission-select"]')
  await missionSelect.waitFor()
  const firstMissionOption = missionSelect.locator('option:not([value=""]):not([disabled])').first()
  await missionSelect.selectOption((await firstMissionOption.getAttribute('value'))!)

  const sessionTitle = 'Team Association E2E Session'
  await admin.fill('[data-testid="session-title-input"]', sessionTitle)
  await admin.fill('[data-testid="session-max-time-input"]', '30')
  await admin.fill('[data-testid="session-scheduled-at-input"]', '2026-12-20T09:00')
  await admin.click('[data-testid="session-submit-btn"]')
  await expect(admin.locator('[data-testid="session-operator-list"]')).toContainText(sessionTitle)

  const sessionItem = admin
    .locator('[data-testid="session-operator-item"]')
    .filter({ hasText: sessionTitle })
  await sessionItem.getByRole('button', { name: 'Asignar operador' }).click()

  const operatorSelect = admin.locator('[data-testid="operator-select"]')
  const operatorOptionValue = await operatorSelect
    .locator('option', { hasText: 'Operator One' })
    .getAttribute('value')
  await operatorSelect.selectOption(operatorOptionValue!)
  await admin.click('[data-testid="assign-operator-btn"]')
  await expect(admin.locator('[data-testid="assign-operator-error"]')).toHaveCount(0)

  // Create an active team as the operator to associate with that session.
  await operator.goto('/dashboard')
  await operator.click('[data-testid="nav-teams"]')
  await operator.click('[data-testid="create-team-btn"]')
  const teamName = 'Association E2E Squad'
  await operator.fill('[data-testid="team-display-name-input"]', teamName)
  await operator.fill('[data-testid="team-code-input"]', 'ASSOC-E2E-TEAM')
  await operator.click('[data-testid="team-form-submit"]')
  await expect(operator.locator('[data-testid="team-detail-panel"]')).toBeVisible()
  await operator.click('[data-testid="teams-back-btn"]')

  const teamRow = operator.locator('[data-testid^="team-row-"]').filter({ hasText: teamName })
  const actionButton = teamRow.locator('[data-testid^="team-row-session-actions-"]')
  await expect(actionButton).toBeEnabled()
  await actionButton.click()
  await expect(operator.getByRole('heading', { name: 'Asignar equipo a sesión' })).toBeVisible()

  // Confirm the association inside the modal — this is the actual mutation, not just opening it.
  const sessionCard = operator
    .locator('[data-testid="team-session-list"] article')
    .filter({ hasText: sessionTitle })
  await sessionCard.getByRole('button', { name: 'Asignar a la sesión' }).click()

  // Modal closes on a successful association.
  await expect(operator.getByRole('heading', { name: 'Asignar equipo a sesión' })).toHaveCount(0)

  // Verify the association actually landed by checking the session's associated-teams list.
  await operator.click('[data-testid="nav-sessions"]')
  const sessionButton = operator
    .locator('[data-testid="assigned-session-button"]')
    .filter({ hasText: sessionTitle })
  await sessionButton.click()

  await expect(operator.locator('[data-testid="session-associated-teams-list"]')).toContainText(teamName)
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
  await expect(page.locator('[data-testid="detail-status"]')).toContainText('Inactivo')
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
  await page.getByRole('button', { name: 'Cancelar' }).first().click()
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
  await page.getByRole('button', { name: 'Cancelar' }).first().click()
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
