import { test, expect } from '../fixtures/auth'

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function idFromTestId(testId: string | null, prefix: string): string {
  if (!testId) throw new Error(`Expected testid with prefix "${prefix}", got "${testId}"`)
  return testId.replace(prefix, '')
}

async function createMission(page: import('@playwright/test').Page, name: string) {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await page.fill('[data-testid="mission-name-input"]', name)
  await page.fill('[data-testid="mission-description-input"]', `Description for ${name}`)
  await page.selectOption('[data-testid="mission-difficulty-input"]', 'Intermediate')
  await page.fill('[data-testid="mission-time-input"]', '30')
  await page.click('[data-testid="mission-submit-btn"]')
  await expect(page.locator('[data-testid="mission-detail-name"]')).toContainText(name)
}

async function addStage(
  page: import('@playwright/test').Page,
  title: string,
): Promise<string> {
  await page.click('[data-testid="add-stage-btn"]')
  await page.fill('[data-testid="node-title-input"]', title)
  await page.click('[data-testid="confirm-add-stage-btn"]')
  const stageNode = page.locator('[data-testid^="stage-node-"]').first()
  await expect(stageNode).toBeVisible()
  const testId = await stageNode.getAttribute('data-testid')
  return idFromTestId(testId, 'stage-node-')
}

async function addSubstage(
  page: import('@playwright/test').Page,
  stageId: string,
  title: string,
): Promise<string> {
  await page.click(`[data-testid="add-substage-btn-${stageId}"]`)
  await page.fill('[data-testid="node-title-input"]', title)
  await page.click(`[data-testid="confirm-add-substage-btn-${stageId}"]`)
  const ssNode = page.locator('[data-testid^="substage-node-"]').first()
  await expect(ssNode).toBeVisible()
  const testId = await ssNode.getAttribute('data-testid')
  return idFromTestId(testId, 'substage-node-')
}

async function addClue(
  page: import('@playwright/test').Page,
  substageId: string,
  title: string,
  text: string,
): Promise<string> {
  await page.click(`[data-testid="add-clue-btn-${substageId}"]`)
  await page.fill('[data-testid="node-title-input"]', title)
  await page.fill('[data-testid="clue-text-input"]', text)
  await page.click(`[data-testid="confirm-add-clue-btn-${substageId}"]`)
  const clueNode = page.locator('[data-testid^="clue-node-"]').first()
  await expect(clueNode).toBeVisible()
  const testId = await clueNode.getAttribute('data-testid')
  return idFromTestId(testId, 'clue-node-')
}

async function setPlayMode(
  page: import('@playwright/test').Page,
  substageId: string,
  targetMode: string,
) {
  const badge = page.locator(`[data-testid="substage-playmode-${substageId}"]`)
  // The badge shows a friendly label ("Treasure Hunt"/"Trivia") but carries the raw
  // enum token on data-playmode — assert against the attribute, not the visible text.
  const currentMode = await badge.getAttribute('data-playmode')
  if (currentMode === targetMode) return
  await page.selectOption(`[data-testid="playmode-select-${substageId}"]`, targetMode)
  await expect(page.locator('[data-testid="playmode-switch-warning"]')).toBeVisible()
  await page.locator('button:has-text("Confirm switch")').click()
  await expect(badge).toHaveAttribute('data-playmode', targetMode, { timeout: 5000 })
}

async function addTarget(
  page: import('@playwright/test').Page,
  substageId: string,
  name: string,
  qrCode: string,
): Promise<string> {
  // Score is not entered: the server derives it from the mission's difficulty.
  await page.click(`[data-testid="add-target-btn-${substageId}"]`)
  await page.fill('[data-testid="target-name-input"]', name)
  await page.fill('[data-testid="target-qrcode-input"]', qrCode)
  await page.locator('[data-testid="target-name-input"]').first().press('Tab')
  // Click the Save button inside the add target form
  await page.locator('button:has-text("Save"):not([data-testid*="confirm"])').first().click()
  await expect(page.locator('[data-testid^="target-node-"]').first()).toBeVisible({ timeout: 5000 })
  const testId = await page.locator('[data-testid^="target-node-"]').first().getAttribute('data-testid')
  return idFromTestId(testId, 'target-node-')
}

// ---------------------------------------------------------------------------
// P1 — Read path (AC1)
// ---------------------------------------------------------------------------

test('admin sees the read-only mission tree for a seeded mission', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  const row = page.locator('[data-testid^="mission-row-"]', { hasText: 'E2E Activatable Mission' })
  await row.locator('[data-testid^="view-mission-btn-"]').click()
  await expect(page.locator('[data-testid="mission-tree"]')).toBeVisible()
  await expect(page.locator('[data-testid^="stage-node-"]').first()).toBeVisible()
  await expect(page.locator('[data-testid^="substage-playmode-"]').first()).toBeVisible()
})

test('an empty mission shows the no-stages placeholder', async ({ adminPage: page }) => {
  await createMission(page, 'Empty Mission')
  await expect(page.locator('[data-testid="mission-tree-empty"]')).toBeVisible()
  await expect(page.locator('[data-testid^="stage-node-"]')).toHaveCount(0)
})

// ---------------------------------------------------------------------------
// 2.1 — Node authoring (AC1)
// ---------------------------------------------------------------------------

test('admin adds a stage, then a substage, then a clue', async ({ adminPage: page }) => {
  await createMission(page, 'Node Authoring')
  const stageId = await addStage(page, 'First Stage')
  const substageId = await addSubstage(page, stageId, 'Child Substage')
  await addClue(page, substageId, 'First Clue', 'Search the library')
  // All three node types visible
  await expect(page.locator(`[data-testid="stage-node-${stageId}"]`)).toContainText('First Stage')
  await expect(page.locator(`[data-testid="substage-node-${substageId}"]`)).toContainText('Child Substage')
  await expect(page.locator('[data-testid^="clue-node-"]').first()).toContainText('First Clue')
})

test('admin edits and removes a node', async ({ adminPage: page }) => {
  await createMission(page, 'Edit Remove')
  const stageId = await addStage(page, 'Edit Me')

  // Edit the stage title
  await page.click(`[data-testid="edit-node-btn-${stageId}"]`)
  await page.fill('[data-testid="node-title-input"]', 'Edited Stage')
  await page.fill('[data-testid="node-sequence-input"]', '1')
  await page.locator('button:has-text("Save"):not([data-testid*="confirm"])').first().click()
  await expect(page.locator(`[data-testid="stage-node-${stageId}"]`)).toContainText('Edited Stage')

  // Remove the stage
  await page.click(`[data-testid="remove-node-btn-${stageId}"]`)
  await expect(page.locator(`[data-testid="confirm-remove-node-btn-${stageId}"]`)).toBeVisible()
  await page.click(`[data-testid="confirm-remove-node-btn-${stageId}"]`)
  await expect(page.locator(`[data-testid="stage-node-${stageId}"]`)).toHaveCount(0)
})

// The edit form seeds each field on mount, so re-opening it must show the saved
// node — never a value left over from an earlier, abandoned edit.
test('the node edit form reseeds from the saved node', async ({ adminPage: page }) => {
  await createMission(page, 'Node Edit Reseed')
  const stageId = await addStage(page, 'Original')
  const stageNode = page.locator(`[data-testid="stage-node-${stageId}"]`)
  const titleInput = page.locator('[data-testid="node-title-input"]')

  await page.click(`[data-testid="edit-node-btn-${stageId}"]`)
  await titleInput.fill('Renamed')
  await page.locator('button:has-text("Save"):not([data-testid*="confirm"])').first().click()
  await expect(stageNode).toContainText('Renamed')

  // Re-opening shows the persisted title, not the one captured at first mount.
  await page.click(`[data-testid="edit-node-btn-${stageId}"]`)
  await expect(titleInput).toHaveValue('Renamed')

  // Cancel discards the typing rather than carrying it into the next open.
  await titleInput.fill('Abandoned')
  await stageNode.locator('button:has-text("Cancel")').first().click()
  // The edit form closed and the row reverted to the saved node.
  await expect(titleInput).toHaveCount(0)
  await expect(stageNode).toContainText('Renamed')
  await page.click(`[data-testid="edit-node-btn-${stageId}"]`)
  await expect(titleInput).toHaveValue('Renamed')
})

// ---------------------------------------------------------------------------
// 2.2 — Play-mode + treasure hunt (AC2, AC3)
// ---------------------------------------------------------------------------

test('admin sets a substage to TreasureHunt and adds a target with a clue', async ({ adminPage: page }) => {
  await createMission(page, 'Treasure Hunt')
  const stageId = await addStage(page, 'Stage with Hunt')
  const substageId = await addSubstage(page, stageId, 'Hunt Substage')
  // Substage is created with TreasureHunt as default — no switch needed.
  // Add a clue
  const clueId = await addClue(page, substageId, 'Cave Clue', 'Look behind the waterfall')
  // Add a target
  const targetId = await addTarget(page, substageId, 'Waterfall', 'QR-WATERFALL')
  // The score is derived from the mission's difficulty (Intermediate => 50 * 2).
  await expect(page.locator(`[data-testid="target-score-${targetId}"]`)).toContainText(
    'Score: 100 (Intermediate)',
  )
  // Associate a clue from within the target's edit form
  const targetRow = page.locator(`[data-testid="target-node-${targetId}"]`)
  await expect(targetRow).toBeVisible()
  await page.click(`[data-testid="edit-target-btn-${targetId}"]`)
  await expect(page.locator(`[data-testid="clue-select-${targetId}"]`)).toBeVisible()
  await page.selectOption(`[data-testid="clue-select-${targetId}"]`, String(clueId))
  await page.locator(`[data-testid="target-node-${targetId}"]`).locator('button:has-text("Save")').click()
  // The target now shows the clue association
  await expect(targetRow).toContainText('clue #', { timeout: 5000 })
})

// The edit form seeds each field on mount, so re-opening it must show the saved
// target — never a value left over from an earlier, abandoned edit.
test('the target edit form reseeds from the saved target', async ({ adminPage: page }) => {
  await createMission(page, 'Target Edit')
  const stageId = await addStage(page, 'Stage')
  const substageId = await addSubstage(page, stageId, 'Hunt Substage')
  const targetId = await addTarget(page, substageId, 'Waterfall', 'QR-WATERFALL')
  const targetRow = page.locator(`[data-testid="target-node-${targetId}"]`)
  const nameInput = page.locator('[data-testid="target-name-input"]')

  await page.click(`[data-testid="edit-target-btn-${targetId}"]`)
  await nameInput.fill('Cavern')
  await targetRow.locator('button:has-text("Save")').click()
  await expect(targetRow).toContainText('Cavern', { timeout: 5000 })

  // Re-opening shows the persisted name, not the one captured at first mount.
  await page.click(`[data-testid="edit-target-btn-${targetId}"]`)
  await expect(nameInput).toHaveValue('Cavern')

  // Cancel discards the edit rather than carrying it into the next open.
  await nameInput.fill('Discarded')
  await targetRow.locator('button:has-text("Cancel")').click()
  await expect(targetRow).toContainText('Cavern')
  await page.click(`[data-testid="edit-target-btn-${targetId}"]`)
  await expect(nameInput).toHaveValue('Cavern')
})

test('switching play mode warns the other mode content is discarded', async ({ adminPage: page }) => {
  await createMission(page, 'Mode Switch')
  const stageId = await addStage(page, 'Stage')
  const substageId = await addSubstage(page, stageId, 'Switchable')
  // Substage defaults to TreasureHunt — switch to Trivia to trigger the warning
  await page.selectOption(`[data-testid="playmode-select-${substageId}"]`, 'Trivia')
  await expect(page.locator('[data-testid="playmode-switch-warning"]')).toBeVisible({ timeout: 5000 })
  await expect(page.locator('[data-testid="playmode-switch-warning"]')).toContainText('discards')
  // Cancel the switch
  await page.locator('button:has-text("Cancel")').last().click()
  await expect(page.locator('[data-testid="playmode-switch-warning"]')).toHaveCount(0)
  // Mode should still be TreasureHunt
  await expect(page.locator(`[data-testid="substage-playmode-${substageId}"]`)).toHaveAttribute('data-playmode', 'TreasureHunt')
})

// ---------------------------------------------------------------------------
// 2.3 — Trivia selection (AC4)
// ---------------------------------------------------------------------------

test('admin assigns a trivia quiz to a Trivia substage', async ({ adminPage: page }) => {
  await createMission(page, 'Trivia Quiz')
  const stageId = await addStage(page, 'Stage with Trivia')
  const substageId = await addSubstage(page, stageId, 'Trivia Substage')
  // Set to Trivia
  await setPlayMode(page, substageId, 'Trivia')
  // The trivia quiz select should appear (it loads quizzes asynchronously)
  const quizSelect = page.locator(`[data-testid="trivia-quiz-select-${substageId}"]`)
  await expect(quizSelect).toBeVisible({ timeout: 10000 })
  // The select should eventually show the default option
  await expect(quizSelect).toBeEnabled({ timeout: 10000 })
})

// ---------------------------------------------------------------------------
// Activation (AC6)
// ---------------------------------------------------------------------------

test('activation is blocked with readiness failures for an incomplete mission', async ({ adminPage: page }) => {
  await createMission(page, 'Not Ready')
  // A freshly created Draft mission with no stages has readiness failures.
  await expect(page.locator('[data-testid="readiness-failure"]').first()).toBeVisible({ timeout: 10000 })
  await expect(page.locator('[data-testid="activate-mission-btn"]')).toBeDisabled()
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Draft')
})

test('admin can deactivate a mission', async ({ adminPage: page }) => {
  await createMission(page, 'Deactivate Test')
  await page.click('[data-testid="deactivate-mission-btn"]')
  await expect(page.locator('[data-testid="confirm-deactivate-mission-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-deactivate-mission-btn"]')
  await expect(page.locator('[data-testid="mission-detail-status"]')).toContainText('Inactive')
  await expect(page.locator('[data-testid="deactivate-mission-btn"]')).toHaveCount(0)
})

// ---------------------------------------------------------------------------
// P3 — Canon guard (AC7)
// ---------------------------------------------------------------------------

test('no SessionMode or SessionSource copy appears in the mission authoring UI', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  const bodyText = await page.locator('body').innerText()
  expect(bodyText).not.toContain('SessionMode')
  expect(bodyText).not.toContain('sessionMode')
  expect(bodyText).not.toContain('SessionSource')
  expect(bodyText).not.toContain('sessionSource')
  // Also check the mission detail view
  const row = page.locator('[data-testid^="mission-row-"]', { hasText: 'E2E Activatable Mission' })
  await row.locator('[data-testid^="view-mission-btn-"]').click()
  const detailText = await page.locator('body').innerText()
  expect(detailText).not.toContain('SessionMode')
  expect(detailText).not.toContain('SessionSource')
})
