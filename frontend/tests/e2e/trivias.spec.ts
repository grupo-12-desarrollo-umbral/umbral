import { test, expect, Page } from '../fixtures/auth'

// --- Nav visibility ---

test('admin sees trivias nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toBeVisible()
})

test('operator sees trivias nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toBeVisible()
})

// --- Catalog view ---

test('operator trivias panel loads with create button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-trivia-btn"]')).toBeVisible()
})

// --- Create flow ---

test('operator can create a trivia quiz and land on its detail view', async ({ operatorPage: page }) => {
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

test('create form cancel returns to list', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-form"]')).toHaveCount(0)
})

// --- Detail view ---

test('operator can open trivia detail from catalog row', async ({ operatorPage: page }) => {
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

test('created draft quiz appears in catalog list', async ({ operatorPage: page }) => {
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

test('operator can edit a draft trivia quiz', async ({ operatorPage: page }) => {
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

test('edit form cancel returns to detail without saving', async ({ operatorPage: page }) => {
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

test('edit button is disabled for non-Draft quizzes', async ({ operatorPage: page }) => {
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
  const archivedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Capitales del mundo' })
  await expect(archivedRow).toBeVisible()
  await archivedRow.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Capitales del mundo')
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

test('operator can open add-question form from draft quiz detail', async ({ operatorPage: page }) => {
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

test('operator can add a question with 2 options and see it in detail', async ({ operatorPage: page }) => {
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

test('operator can add a question with 4 options', async ({ operatorPage: page }) => {
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

test('add-question cancel returns to detail without network call', async ({ operatorPage: page }) => {
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

test('operator can edit an existing question', async ({ operatorPage: page }) => {
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

test('edit-question form is pre-filled with current values', async ({ operatorPage: page }) => {
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

test('admin sees no trivia authoring controls', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  // Issue #173: admins keep read access to the trivias panel, but authoring is Operator-only.
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-trivia-btn"]')).toHaveCount(0)
})

// --- Regression: HU-11 quiz flows unaffected ---

test('HU-11 trivia create flow still works after question authoring wiring', async ({ operatorPage: page }) => {
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

test('HU-11 trivia edit flow still works after question authoring wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Edit Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Before edit.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Edit — Updated')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression HU-11 Edit — Updated')
})

// ---- Helpers ----

async function createReadyDraftQuiz(page: Page, title: string): Promise<void> {
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', title)
  await page.fill('[data-testid="trivia-description-input"]', 'Lifecycle test quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Add one question to satisfy the readiness check
  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'What is 2+2?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '30')
  await page.fill('[data-testid="question-option-text-0"]', '4')
  await page.fill('[data-testid="question-option-text-1"]', '5')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')
  // now on detail view with one question
}

// ---- isSourceReady display ----

test('draft quiz shows source ready as No in list view', async ({ operatorPage: page }) => {
  // Unique title: these e2e tests persist real rows, so a static title collides across reruns.
  const quizTitle = `Source Ready Test ${Date.now()}`
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', quizTitle)
  await page.fill('[data-testid="trivia-description-input"]', 'Should be not ready.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Wait for the create to land on detail before navigating — the create view has its own
  // "← Back to trivia quizzes" button, so clicking Back mid-submit races the pending transition.
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()

  // Back to list
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: quizTitle })
  const chip = row.locator('[data-testid^="trivia-source-ready-"]')
  await expect(chip).toContainText('No')
})

test('draft quiz detail shows source ready badge as No', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Badge Check Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'Draft badge.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
})

// ---- Publish button readiness gate ----

test('publish button is disabled for empty draft quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Empty Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'No questions.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toBeDisabled()
  await expect(page.locator('[data-testid="trivia-readiness-indicator"]')).toBeVisible()
})

test('publish button is enabled when all readiness conditions are met', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Ready To Publish Quiz')

  await expect(page.locator('[data-testid="publish-trivia-btn"]')).not.toBeDisabled()
  await expect(page.locator('[data-testid="trivia-readiness-indicator"]')).toHaveCount(0)
})

// ---- Publish flow ----

test('operator can publish a ready draft quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Publishable Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('Yes')
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
})

test('published quiz shows source ready as Yes in list view', async ({ operatorPage: page }) => {
  // Unique title: these e2e tests persist real rows, so a static title collides across reruns
  // (many 'Published List Check' rows → strict-mode violation on the source-ready chip).
  const quizTitle = `Published List Check ${Date.now()}`
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, quizTitle)

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: quizTitle })
  await expect(row.locator('[data-testid^="trivia-source-ready-"]')).toContainText('Yes')
})

test('operator can cancel publish confirmation without network call', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Publish Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  // Should be back to showing the trigger buttons — status unchanged
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

// ---- Archive flow ----

test('operator can archive a published quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Archive From Published')

  // Publish first
  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  // Archive
  await page.click('[data-testid="archive-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-archive-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
})

test('operator can archive a draft quiz directly', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archive From Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'Archiving draft directly.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
})

test('operator can cancel archive confirmation without network call', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Archive Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-archive-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
})

// ---- Edit gate after lifecycle transitions ----

test('edit button is disabled for published quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Published No Edit')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()
})

test('edit button is disabled for archived quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archived No Edit')
  await page.fill('[data-testid="trivia-description-input"]', 'Will be archived.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()
})

// ---- Confirm-state mutual exclusion ----

test('opening publish confirmation hides archive trigger', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Mutual Exclusion Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')

  // Publish confirm is visible, archive trigger must be gone
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
})

// ---- Authorization ----

test('admin cannot reach publish or archive controls', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  // Open a seeded Published quiz: an operator would see Archive/Duplicate here; an admin sees neither.
  const publishedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Filosofos de Atenas' })
  await publishedRow.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Regression: HU-14A question authoring unaffected ----

test('HU-14A add-question flow still works after lifecycle wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression: Add Question')
  await page.fill('[data-testid="trivia-description-input"]', 'Question authoring regression.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

test('HU-14A edit-question flow still works after lifecycle wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Regression: Edit Question')

  await page.locator('[data-testid^="edit-question-btn-"]').first().click()
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.fill('[data-testid="question-prompt-input"]', 'Updated prompt after lifecycle wiring')
  await page.click('[data-testid="question-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Updated prompt after lifecycle wiring')
})

// ---- Regression: HU-11 quiz create/edit flows unaffected ----

test('HU-11 trivia create flow still works after publish/archive wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Create')
  await page.fill('[data-testid="trivia-description-input"]', 'HU-11 regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('HU-11 trivia edit flow still works after publish/archive wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Edit')
  await page.fill('[data-testid="trivia-description-input"]', 'Before edit.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Edit — Updated')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression HU-11 Edit — Updated')
})

// ---- Regression: HU-09 missions panel unaffected ----

test('HU-09 missions panel still reachable after lifecycle wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
})

// ---- Helpers ----

async function _createReadyPublishedQuiz(page: Page, title: string): Promise<number> {
  // Reuses createReadyDraftQuiz from HU-12 helpers, then publishes.
  await createReadyDraftQuiz(page, title)
  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  // Extract id from trivia-source-ready testid context — use the URL or data attribute.
  // Alternatively, capture from the back-and-re-list pattern:
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()
  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: title })
  const rowTestId = await row.getAttribute('data-testid')
  return parseInt(rowTestId!.replace('trivia-row-', ''), 10)
}

// ---- Duplicate flow ----

test('operator can duplicate a published quiz and lands on new copy detail', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Original For Duplication')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-duplicate-btn"]')

  // Should now be on the new copy's detail
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toHaveCount(0)
})

test('duplicate copy title matches source title', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Source Title Check')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Source Title Check')
})

test('operator can cancel duplicate confirmation without network call', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Duplicate Quiz')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('duplicate confirmation hides other trigger buttons', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Mutual Exclusion Duplicate')

  await page.click('[data-testid="duplicate-trivia-btn"]')

  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
})

test('archived quiz has no duplicate button', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archived No Duplicate')
  await page.fill('[data-testid="trivia-description-input"]', 'Will be archived.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Lineage cues in list view ----

test('copy shows Copy chip in list provenance column', async ({ operatorPage: page }) => {
  // Unique title so the assertion scopes to this run's copy — a global copy-chip count collides
  // with copies left by reruns and by other duplicate tests running in parallel on the shared DB.
  const sourceTitle = `Lineage List Source ${Date.now()}`
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, sourceTitle)

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  // Navigate back to list
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  // The new copy (same title as its source) should be the row that carries a Copy chip.
  const copyRow = page.locator('[data-testid^="trivia-row-"]')
    .filter({ hasText: sourceTitle })
    .filter({ has: page.locator('[data-testid^="trivia-copy-chip-"]') })
  await expect(copyRow).toHaveCount(1)
})

test('original quiz shows no Copy chip in list', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Original No Copy Chip')
  await page.fill('[data-testid="trivia-description-input"]', 'Original quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const originalRow = page.locator('[data-testid^="trivia-row-"]').filter({
    hasText: 'Original No Copy Chip',
  })
  await expect(originalRow.locator('[data-testid^="trivia-copy-chip-"]')).toHaveCount(0)
})

// ---- Lineage cue in detail view ----

test('copy detail shows source quiz id badge', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Lineage Detail Source')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toBeVisible()
  const badgeText = await page.locator('[data-testid="trivia-source-quiz-id"]').innerText()
  expect(badgeText).toMatch(/Quiz #\d+/i) // .chip CSS uppercases the label ("QUIZ #273")
})

test('original quiz detail shows no source quiz id badge', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Original No Badge')
  await page.fill('[data-testid="trivia-description-input"]', 'Original quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toHaveCount(0)
})

// ---- Retire flow ----
// Data is fetched server-side (Server Action → mission-design service), so browser page.route()
// cannot inject usage history. These use the seeded "Trivia con historial de uso" quiz
// (Published, HasUsageHistory=true) from seed-all.sh instead.

const USED_QUIZ_TITLE = 'Trivia con historial de uso'

test('retire button is visible for a quiz with usage history', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  const usedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: USED_QUIZ_TITLE })
  await expect(usedRow).toBeVisible()
  await usedRow.locator('[data-testid^="view-trivia-btn-"]').click()

  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toBeVisible()
})

test('Used chip appears in list for a quiz with usage history', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  const usedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: USED_QUIZ_TITLE })
  await expect(usedRow).toBeVisible()
  await expect(usedRow.locator('[data-testid^="trivia-usage-chip-"]')).toBeVisible()
  await expect(usedRow.locator('[data-testid^="trivia-copy-chip-"]')).toHaveCount(0)
})

test('retire button is not visible for a quiz without usage history', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Unused No Retire')
  await page.fill('[data-testid="trivia-description-input"]', 'No history.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
})

// ---- Archive regression: unused quizzes still use Archive ----

test('HU-12 archive flow still works for unused quizzes after retire wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Archive Regression After HU-13')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Authorization ----

test('admin cannot reach duplicate or retire controls', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  // Seeded quiz with usage history: an operator would see Retire/Duplicate; an admin sees neither.
  const usedRow = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: USED_QUIZ_TITLE })
  await usedRow.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Regression: HU-12 publish flow unaffected ----

test('HU-12 publish flow unaffected after HU-13 wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'HU-12 Publish Regression')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('Yes')
})

// ---- Regression: HU-14A question authoring unaffected ----

test('HU-14A add-question flow still works after HU-13 wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'HU-14A Regression After HU-13')
  await page.fill('[data-testid="trivia-description-input"]', 'Question authoring regression.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

// ---- Regression: HU-11 quiz create/edit flows unaffected ----

test('HU-11 trivia create flow still works after HU-13 wiring', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'HU-11 Regression After HU-13')
  await page.fill('[data-testid="trivia-description-input"]', 'HU-11 regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toHaveCount(0)
})

// ---- Regression: HU-09 missions panel unaffected ----

test('HU-09 missions panel still reachable after HU-13 wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
})

// --- Question removal (HU-14A follow-up) ---

// Helper: create a Draft quiz and add a question with the given prompt + sequence order.
async function addQuestion(page: Page, prompt: string, order: string) {
  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', prompt)
  await page.fill('[data-testid="question-sequence-order-input"]', order)
  await page.fill('[data-testid="question-score-value-input"]', '50')
  await page.fill('[data-testid="question-timer-input"]', '20')
  await page.fill('[data-testid="question-option-text-0"]', 'A')
  await page.fill('[data-testid="question-option-text-1"]', 'B')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
}

test('operator can remove a question from a draft quiz', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Remove Single Question')
  await page.fill('[data-testid="trivia-description-input"]', 'One question, then removed.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await addQuestion(page, 'Doomed question', '1')
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Doomed question')

  const removeBtn = page.locator('[data-testid^="remove-question-btn-"]').first()
  await removeBtn.click()
  await page.locator('[data-testid^="confirm-remove-question-btn-"]').first().click()

  await expect(page.locator('[data-testid^="question-row-"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('No questions added yet.')
})

test('removing a question reconciles the sequence order of the remaining questions', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Reconcile Order')
  await page.fill('[data-testid="trivia-description-input"]', 'Three questions; remove the first.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await addQuestion(page, 'First question', '1')
  await addQuestion(page, 'Second question', '2')
  await addQuestion(page, 'Third question', '3')

  await expect(page.locator('[data-testid^="question-row-"]')).toHaveCount(3)

  // Remove the row whose Order cell is "1" (the first question).
  const firstRow = page.locator('[data-testid^="question-row-"]').filter({ hasText: 'First question' })
  await firstRow.locator('[data-testid^="remove-question-btn-"]').click()
  await firstRow.locator('[data-testid^="confirm-remove-question-btn-"]').click()

  await expect(page.locator('[data-testid^="question-row-"]')).toHaveCount(2)
  await expect(page.locator('[data-testid="trivia-questions-section"]')).not.toContainText('First question')

  // Remaining questions are renumbered 1..N with no gap. Read the Order cells top-to-bottom.
  const orderCells = page.locator('[data-testid^="question-row-"] td[data-label="Order"]')
  await expect(orderCells).toHaveText(['1', '2'])
  // And the first row is now "Second question" at order 1.
  const topRow = page.locator('[data-testid^="question-row-"]').first()
  await expect(topRow).toContainText('Second question')
  await expect(topRow.locator('td[data-label="Order"]')).toHaveText('1')
})

test('remove confirmation can be cancelled without deleting', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Cancel Remove')
  await page.fill('[data-testid="trivia-description-input"]', 'Removal is cancelled.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await addQuestion(page, 'Keep me', '1')

  await page.locator('[data-testid^="remove-question-btn-"]').first().click()
  await expect(page.locator('[data-testid^="confirm-remove-question-btn-"]').first()).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()

  await expect(page.locator('[data-testid^="question-row-"]')).toHaveCount(1)
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Keep me')
  await expect(page.locator('[data-testid^="remove-question-btn-"]').first()).toBeVisible()
})

test('published quiz shows no remove control', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Publish Then Check Remove')
  await page.fill('[data-testid="trivia-description-input"]', 'Publish, then remove is gone.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // A publishable question (score + timer + valid options already set by the helper).
  await addQuestion(page, 'Publishable question', '1')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  // Question still listed, but no authoring controls.
  await expect(page.locator('[data-testid^="question-row-"]')).toHaveCount(1)
  await expect(page.locator('[data-testid^="remove-question-btn-"]')).toHaveCount(0)
  await expect(page.locator('[data-testid^="edit-question-btn-"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="add-question-btn"]')).toHaveCount(0)
})

test('admin cannot reach any question authoring UI', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  // Admin cannot create a quiz, so there is no draft to add or remove questions on.
  await expect(page.locator('[data-testid="create-trivia-btn"]')).toHaveCount(0)
  // And on a seeded quiz detail, no add-question control is exposed to the admin.
  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Filosofos de Atenas' })
  await row.locator('[data-testid^="view-trivia-btn-"]').click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="add-question-btn"]')).toHaveCount(0)
})

// --- Regression: HU-14A add/edit flow unaffected by the new Remove control ---

test('HU-14A add-question flow still works alongside remove control', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Add Still Works')
  await page.fill('[data-testid="trivia-description-input"]', 'Add after removal wiring.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await addQuestion(page, 'Still addable', '1')
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Still addable')
  // Both Edit and Remove present on the row.
  await expect(page.locator('[data-testid^="edit-question-btn-"]').first()).toBeVisible()
  await expect(page.locator('[data-testid^="remove-question-btn-"]').first()).toBeVisible()
})
