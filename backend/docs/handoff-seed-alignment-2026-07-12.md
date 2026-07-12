# Seed alignment handoff — 2026-07-12

## Purpose

This work aligned the canonical backend seeder, Playwright global setup, and the
HU-171 manual-test fixture with the current MissionDesign trivia schema. The
changes are still in the working tree; inspect `git diff` before continuing.

## What changed

- `backend/scripts/seed-all.sh`
  - Removed the deleted `TriviaQuestions.SequenceOrder` column from every trivia
    question insert. `TriviaOptions.SequenceOrder` remains valid.
  - Added the dedicated, published, two-question quiz
    `HU-171 Progreso de substages`.
  - Before rebuilding the quiz catalog with fresh IDs, deletes every mission
    containing a trivia substage. This prevents dangling or silently remapped
    `MissionSubstages.TriviaQuizId` values. The operation is intentionally
    destructive; Playwright recreates its E2E missions afterward.
- `backend/scripts/README.md`
  - Corrected the catalog count to 7 quizzes: 5 Published, 1 Draft, 1 Archived.
  - Documented the destructive mission cleanup required by quiz-ID recreation.
- `backend/services/mission-design-service/src/Infrastructure/Persistence/Migrations/20260712182332_AddTargetCoordinates.Designer.cs`
  - Removed stale target-model metadata that incorrectly reintroduced trivia
    question `SequenceOrder` after its removal migration.
- `frontend/tests/setup/global-setup.ts`
  - Replaced obsolete `seed-dev-data.sh` guidance with `seed-all.sh`.
  - Its three trivia missions already rebind or rebuild their quiz selections on
    every run; that existing behavior was verified and retained.
- `frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`
  - Resolves the dedicated HU-171 quiz by `Title + Published`, never by ID.
  - Recreates its mission and deletes the prior `Substage Progress E2E` live
    session before creating a replacement, preventing duplicate titled fixtures.
- `frontend/docs/hu-171-manual-test.md`
  - Matches the dedicated two-question quiz and fixed-position chip label
    `2· Treasure Hunt`.
  - Clarifies ownership: backend seed supplies quizzes; Playwright global setup
    supplies E2E identities/team membership.
  - Its embedded seed-spec appendix exactly matches the source spec.

## Why

Migration `20260712180948_RemoveTriviaQuestionSequenceOrder` removed authored
question ordering; runtime question order is derived from persisted question IDs.
The old seed SQL still inserted the removed column. Separately, `seed-all.sh`
recreates quiz rows with new IDs, while `MissionSubstages.TriviaQuizId` has no
database foreign key, so retained missions could silently point at deleted or
different quizzes. The cleanup and title/status resolution close those gaps.

## End-to-end verification performed

The full local workflow was executed twice:

1. `backend/scripts/seed-all.sh`
2. `pnpm exec playwright test tests/e2e/session-substage-progress-manual-seed.spec.ts`
3. Cross-database invariant queries

Both final passes succeeded. After pass 2:

- quiz catalog: 7 total / 5 Published / 1 Draft / 1 Archived;
- dedicated HU-171 quiz: 2 questions;
- dangling trivia-substage quiz references: 0;
- each named E2E/HU-171/live-trivia mission: exactly 1;
- HU-171 sessions: exactly 1 in `Preparing`;
- SMOKE sessions: exactly 7;
- Seeded Live Trivia sessions: exactly 1;
- duplicate `op-1` / `participant-1` application emails: 0;
- `participant-1` → Gilded Owls memberships: exactly 1;
- both Playwright runs: passed.

The first attempt correctly failed because PostgreSQL and Keycloak had been
stopped and the live database had not applied the two latest MissionDesign
migrations. After starting dependencies and restarting services, migration
history ended at `20260712182332_AddTargetCoordinates`, the removed column was
absent, and both complete passes succeeded. This is useful diagnostic context if
a future seed run reports a missing/non-null `SequenceOrder` column.

Static verification also passed: `bash -n`, ESLint on both changed test files,
`git diff --check`, and exact source/appendix comparison.

## Suggested skills

- `review` — review the working-tree changes against repository standards and
  this handoff.
- `commit-work` — stage only the intended seed-alignment files and create logical
  commits when requested.
- `diagnose` — use if a future live-stack seed run fails before changing schema
  or seed behavior.
