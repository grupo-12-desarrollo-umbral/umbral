import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('admin sees trivias nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toBeVisible()
})

test('operator does not see trivias nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Catalog view ---

test('admin trivias panel loads with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-trivia-btn"]')).toBeVisible()
})

// --- Create flow ---

test('admin can create a trivia quiz and land on its detail view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()

  await page.fill('[data-testid="trivia-title-input"]', 'Geography Basics')
  await page.fill('[data-testid="trivia-description-input"]', 'Test your knowledge of world capitals.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Geography Basics')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('create form cancel returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-form"]')).toHaveCount(0)
})

// --- Detail view ---

test('admin can open trivia detail from catalog row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  const viewBtn = page.locator('[data-testid^="view-trivia-btn-"]').first()
  await expect(viewBtn).toBeVisible()
  await viewBtn.click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
})

// --- Draft changes are queryable (integration gate) ---

test('created draft quiz appears in catalog list', async ({ adminPage: page }) => {
  const quizTitle = `Draft Visibility ${Date.now()}`
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')

  await page.fill('[data-testid="trivia-title-input"]', quizTitle)
  await page.fill('[data-testid="trivia-description-input"]', 'Verifying draft is in catalog.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Verify detail shows
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText(quizTitle)

  // Navigate fresh to verify it appears in the catalog
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator(`[data-testid^="trivia-row-"]`).filter({ hasText: quizTitle })).toBeVisible()
})

// --- Edit flow ---

test('admin can edit a draft trivia quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  // Create a fresh draft
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Edit Target Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'To be edited.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()
  await page.fill('[data-testid="trivia-title-input"]', 'Updated Quiz Title')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Updated Quiz Title')
})

test('edit form cancel returns to detail without saving', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Cancel Edit Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Cancel me.')
  await page.click('[data-testid="trivia-submit-btn"]')

  const originalTitle = await page.locator('[data-testid="trivia-detail-title"]').innerText()

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Should Not Be Saved')
  await page.locator('[data-testid="trivia-form"] button[type="button"]').click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText(originalTitle.trim())
})

// --- Edit gate for non-Draft quizzes ---

test('edit button is disabled for non-Draft quizzes', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  // Check Published quiz has Edit disabled
  const publishedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Filosofos de Atenas' })
  await expect(publishedRow).toBeVisible()
  await publishedRow.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Filosofos de Atenas')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()

  // Check Archived quiz has Edit disabled
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()
  const archivedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Guitarristas mas queridos' })
  await expect(archivedRow).toBeVisible()
  await archivedRow.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Guitarristas mas queridos')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()
})

// --- Authorization guard ---

test('participant does not see trivias nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Regression: missions panel still works ---

test('HU-09 missions panel still reachable for admin after trivias wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-mission-btn"]')).toBeVisible()
})

test('HU-09 admin mission create still works after trivias wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
})
