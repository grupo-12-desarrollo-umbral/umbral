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

// --- Question authoring: add ---

test('admin can open add-question form from draft quiz detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Question Form Visibility')
  await page.fill('[data-testid="trivia-description-input"]', 'Check add question form appears.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="add-question-btn"]')).toBeVisible()
  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-0"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-1"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-2"]')).toHaveCount(0) // starts with 2
})

test('admin can add a question with 2 options and see it in detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Add Question E2E')
  await page.fill('[data-testid="trivia-description-input"]', 'Test adding a question.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'What is 1+1?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '50')
  await page.fill('[data-testid="question-timer-input"]', '20')
  await page.fill('[data-testid="question-option-text-0"]', '2')
  await page.fill('[data-testid="question-option-text-1"]', '3')
  await page.click('[data-testid="question-option-correct-0"]') // mark first option correct

  await page.click('[data-testid="question-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('What is 1+1?')
})

test('admin can add a question with 4 options', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Four Options Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Testing four options.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Best planet?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '30')

  // Add two more options (starts with 2)
  await page.click('[data-testid="question-add-option-btn"]')
  await page.click('[data-testid="question-add-option-btn"]')

  await page.fill('[data-testid="question-option-text-0"]', 'Earth')
  await page.fill('[data-testid="question-option-text-1"]', 'Mars')
  await page.fill('[data-testid="question-option-text-2"]', 'Venus')
  await page.fill('[data-testid="question-option-text-3"]', 'Jupiter')
  await page.click('[data-testid="question-option-correct-0"]')

  // Add option button should be gone at 4
  await expect(page.locator('[data-testid="question-add-option-btn"]')).toHaveCount(0)

  await page.click('[data-testid="question-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

test('add-question cancel returns to detail without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Cancel Question Test')
  await page.fill('[data-testid="trivia-description-input"]', 'No question saved.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.locator('[data-testid="question-form"] button[type="button"]').filter({ hasText: 'Cancel' }).click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-form"]')).toHaveCount(0)
})

// --- Question authoring: edit ---

test('admin can edit an existing question', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Edit Question Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Will have one question.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Add a question first
  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Original prompt')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '50')
  await page.fill('[data-testid="question-timer-input"]', '20')
  await page.fill('[data-testid="question-option-text-0"]', 'A')
  await page.fill('[data-testid="question-option-text-1"]', 'B')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')

  // Edit the question
  const editBtn = page.locator('[data-testid^="edit-question-btn-"]').first()
  await expect(editBtn).toBeVisible()
  await editBtn.click()

  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-prompt-input"]')).toHaveValue('Original prompt')

  await page.fill('[data-testid="question-prompt-input"]', 'Updated prompt')
  await page.fill('[data-testid="question-score-value-input"]', '75')
  await page.click('[data-testid="question-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Updated prompt')
})

test('edit-question form is pre-filled with current values', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Prefill Check Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Check form pre-fill.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Capital of France?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '45')
  await page.fill('[data-testid="question-explanation-input"]', 'Paris is the capital.')
  await page.fill('[data-testid="question-option-text-0"]', 'Paris')
  await page.fill('[data-testid="question-option-text-1"]', 'Berlin')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')

  await page.locator('[data-testid^="edit-question-btn-"]').first().click()

  await expect(page.locator('[data-testid="question-prompt-input"]')).toHaveValue('Capital of France?')
  await expect(page.locator('[data-testid="question-score-value-input"]')).toHaveValue('100')
  await expect(page.locator('[data-testid="question-timer-input"]')).toHaveValue('45')
  await expect(page.locator('[data-testid="question-explanation-input"]')).toHaveValue('Paris is the capital.')
})

// --- Authorization ---

test('operator sees no add-question or edit-question buttons', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  // Operators do not see trivias nav so they cannot reach the panel.
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Regression: HU-11 quiz flows unaffected ---

test('HU-11 trivia create flow still works after question authoring wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression Quiz')
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
})

test('HU-11 trivia edit flow still works after question authoring wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Edit Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Before edit.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Edit Quiz — Updated')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression Edit Quiz — Updated')
})
