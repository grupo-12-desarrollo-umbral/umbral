# HU-28 Trivia clue release — full cross-workload handoff — 2026-07-14

## Purpose

Complete implementation of operator-triggered clue release for **trivia** substages.
Previously, a `HiddenUntilOperatorRelease` clue authored on a trivia substage was dead
data — snapshotted into the live session but unreachable by any code path.

The work spans Phases 1–4 (backend, done in the preceding session) and Phase 5
(frontend + mobile, done this session). This handoff records what Phase 5 did;
the plan at `backend/docs/hu28-trivia-clue-release-plan-2026-07-14.md` has the full
rationale, design decisions (D-1 through D-4), and test plan. The backend
implementation state is captured by
`backend/docs/hu28-trivia-clue-release-backend-implementation-handoff-2026-07-14.md`.

## What this session did (Phase 5)

### Frontend dashboard

- **`frontend/app/lib/definitions.ts`** — Renamed DTOs to match the new exactly-one-of
  subject model: `ReleasableTargetDto` → `ReleasableClueDto`, `ReleasableTargetsDto` →
  `ReleasableCluesDto`. `ReleaseClueRequest` and `ReleaseClueResultDto` now carry
  optional `targetId`/`clueId`. Trivia clues pass `targetName: null` (frontend
  renders `Pista {n}` per D-3).

- **`frontend/app/lib/sessions.ts`** — Renamed `getReleasableTargets` →
  `getReleasableClues`. `releaseClue` body serialization now emits whichever
  subject key the selected row carries.

- **`frontend/app/actions/sessions.ts`** — Updated imports and action name.

- **`frontend/app/dashboard/DashboardClient.tsx`** — Reducer/state/load function
  renamed from target-centric to clue-centric; prop name changed.

- **`frontend/app/dashboard/OperatorClueReleasePanel.tsx`** — Accepts
  `ReleasableClueDto[]`. The select key is whichever id is present
  (`targetId ?? clueId`). Picker label shows `targetName` for treasure-hunt
  targets, `Pista {sequenceOrder}` for trivia clues. Requests send the
  matching subject id.

- **`frontend/tests/unit/app/dashboard/operator-clue-release-panel.test.ts`** —
  Updated types, added a trivia clue fixture, new test for trivia release path.

### Mobile (participant board payload)

- **`mobile/src/lib/realtime/team-board-types.ts`** — Added
  `clueSnapshotId?: string | null` to `VisibleClueDto` (D-4). Introduced
  `'substage'` as a fourth `ClueKind`. Updated `clueKind()` and `clueKey()`.
  No UI changes needed — `targetSnapshotId == null` filter and
  `targetName ?? 'CLUE'` label already handle substage clues correctly.

### Docs

- **`frontend/docs/hu-28-manual-test.md`** — Fixed "seeded target clues" →
  "seeded target clue" (plural mismatch after the seed was corrected).

## Post-handoff fixes (second session, same date)

### 1. Countdown numeral cropped by inherited lineHeight

The 48px countdown numeral in `mobile/src/components/substage-countdown.tsx:21` spreads
`typography.headline` (`lineHeight: 20`) and overrides `fontSize: 48` without overriding
`lineHeight` — the line box is 20px tall but the glyph is 48px, cropping the number on top and
bottom and giving it a squashed look. Fixed by adding `lineHeight: 52` inline.

### 2. Toast fires during pre-game countdown

The trivia substage-initial clue arrives in `board.visibleClues` at the same moment the 5s pre-game
countdown starts. `OperativeClueSurface` raised the arrival toast immediately, overlapping the
"GET READY 5→1" countdown. Fixed in two places:

- **`mobile/src/components/operative-clue-surface.tsx`** — Added optional `suppressToast` prop. When
  true, the fresh-clue detection effect returns without marking the clue as toasted, so it stays
  "fresh" and will toast once suppression lifts.
- **`mobile/src/app/(app)/team-space.tsx:448`** — Passes
  `suppressToast={pregameSecondsLeft != null}` so the toast is deferred while the countdown is
  showing. When countdown ends, `suppressToast` flips to `false`, the effect re-runs, and deferred
  clues toast.

### 3. Clues tab ordering not newest-first

`mobile/src/components/treasure-hunt-board.tsx` reorders `visibleClues` to put non-target
(operative/mission) clues above target clues, but target clues kept their backend
`SequenceOrder` (ascending) — the first-authored target was at the top of the target group, not
the last-released. Fixed by appending `.reverse()` to the target-clue filter so the last in
SequenceOrder (closest to "last released") appears at the top of the target group. Operatives are
unaffected — they already arrive ordered by `CreatedAt DESC` from the backend.

## No changes needed on

- Backend (Phases 1–4 already committed in the prior session)
- Release panel `onReleased` toast label in `DashboardClient.tsx` was updated
  from `Target {target}` to a dynamic label (`Pista {n}` or `{targetName}`)

## Verification

All pass:

| Gate | Result |
|---|---|
| `npm test` (frontend) | 17 suites, 121 passed |
| `npm run build` (frontend)  | Compiled successfully |
| `npx tsc --noEmit` (frontend) | Clean |
| `npm test` (mobile) | 28 suites, 246 passed |
| `make -C backend build SVC=session-operations-service` | Passed |
| `make -C backend test SVC=session-operations-service` | 1,061 passed, 0 failed |

## Working-tree state

The tree is dirty from both sessions. Files span backend, frontend, mobile, and
seed/manual-test changes. Use `git status` before staging; never `git commit -a`.
The handoff doc that would live at root `HANDOFF.md` is gitignored — this file
replaces it in `backend/docs/`.

## Suggested skills

- **`commit-work`** / conventional-commits guidance — if the next session is
  asked to stage or commit. The tree has work from two sessions; each should
  be reviewed and committed separately by path.
- **`vercel-react-best-practices`** — if any React/Next.js code in the
  dashboard is revised.
- **`aspnet-backend-testing`** — if backend regression coverage changes.
- **`receiving-code-review`** — if a PR review surfaces changes needed in
  this work.
