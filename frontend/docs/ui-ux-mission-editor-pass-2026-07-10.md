# Handoff — Mission editor UI/UX pass + #146 (2026-07-10)

Session goal: *"improve the UI/UX of the edit-mission flow — it's too layered"* plus
*"this should fix issue #146"* (preview the selected quiz's questions in a Trivia substage).

**Status: changes are uncommitted (working tree only).** On branch `docs/serialized-order-des87`,
stacked on top of the earlier dashboard pass (`frontend/docs/ui-ux-dashboard-pass-2026-07-10.md`).
Lint clean, `tsc` clean for touched files, **36/36 unit tests pass**, `next build` succeeds.
Not driven in a live browser this session (needs the full stack — see below).

Design system: `frontend/DESIGN.md` (Umbral Command Center — ember/parchment). All new CSS uses
existing tokens; no hardcoded colors.

## The problem being fixed

The mission editor (`app/dashboard/mission/`) rendered **every substage fully expanded at once**:
each showed its play-mode dropdown, its targets section, an always-inline add-target form
(name/score/active/QR/generate/preview dumped together), and its clues section — simultaneously.
With more than one substage it became a wall of forms. The play-mode "Confirm switch" warning also
crammed in beside the dropdown. And per **#146**, a Trivia substage gave a bare quiz dropdown with
no way to see the quiz's questions/answers without leaving for the Trivias panel.

## Chosen direction

Presented three options to the user via a question; they picked **Accordion tree** (lowest-risk,
keeps the tree mental model). Rejected: focused-modals and two-pane master/detail.

## What changed

Files (all under `frontend/app/`):
`dashboard/TriviaQuestionList.tsx` (new), `dashboard/TriviasPanel.tsx`,
`dashboard/mission/MissionTree.tsx`, `dashboard/mission/SubstageEditor.tsx`,
`dashboard/dashboard.module.css`.

1. **Shared `TriviaQuestionList`** (new file). Extracted the read-only questions/options table out of
   `TriviasPanel.renderQuestionsSection` into an exported presentational component. Props:
   `questions` + optional `renderRowActions(q)` (adds an Actions column) + `actionsHeader`. Omitting
   `renderRowActions` renders it read-only. Satisfies #146's "extract, export, use in both" AC.
   `TriviasPanel` keeps its `trivia-questions-section` wrapper, `add-question-btn` header, and
   `edit-question-btn-{id}` (passed via `renderRowActions`) — so `trivias.spec.ts` is unaffected.

2. **Accordion tree** (`MissionTree.tsx`). Each substage header is now a `<button>` toggle
   (`▸`/`▾`, `aria-expanded`/`aria-controls`). State: a single `openSubstageId` — **at most one
   substage body open at a time** ("others fold"). Collapsed substages show a one-line summary via
   `substageSummary()` (`"3 targets · 2 clues"` / `"Quiz selected · 1 clue"`). The playmode chip
   stays in the collapsed header. **Newly-added substages auto-open** (the `AddSubstageControl`
   `onMutated` wrapper diffs old vs new substage ids and sets `openSubstageId`).

3. **#146 — Trivia quiz preview** (`SubstageEditor.tsx > TriviaSelectionControl`). A collapsible
   **"▸ Preview questions"** panel under the quiz dropdown renders quiz title + status chip +
   `<TriviaQuestionList>` (read-only). Fetches `getTriviaQuiz(current)` **lazily on first expand**,
   caches in `preview` state (trusted only when `preview.id === current`), and **refreshes when the
   selection changes** (`save()` drops the cache and refetches if open). `data-testid`:
   `trivia-quiz-preview-{substageId}` (container) and `trivia-quiz-preview-toggle-{substageId}`.

4. **Play-mode switch warning** (`SubstageEditor.tsx > PlayModeControl`). Moved out of the cramped
   inline flex row into a full-width `.playModeWarning` notice below the selector. Kept the
   `playmode-switch-warning` test id and the "Confirm switch" button text (e2e depends on both).

5. **CSS** (`dashboard.module.css`, appended after `.treeTargetInfo`). New classes: `.substageToggle`
   (+`:hover`/`:focus-visible`), `.substageToggleCaret`, `.substageSummary`, `.treeSubstageBody`,
   `.playModeControl`, `.playModeWarning`(+`Actions`), `.triviaSelect`, `.triviaPreview`(+`Body`/`Meta`),
   `.triviaOptionList` / `.triviaOptionCorrect` (replaced the old inline styles in the extracted table).

## Load-bearing decision — why the e2e suite still passes untouched

`tests/e2e/mission-hierarchy.spec.ts` interacts with `playmode-select-*`, `add-target-btn-*`,
`add-clue-btn-*`, `trivia-quiz-select-*` **immediately after adding a substage** — those live in the
substage *body*, which now only renders when open. Two facts keep the tests green with **zero test
edits**: (a) newly-added substages auto-open, and (b) every existing test creates exactly one
substage per mission and acts on it right away (none interleaves two substages, so single-open
accordion never collapses the one under test). If a future test creates two substages and revisits
the first, it must click `toggle-substage-{id}` first.

## Not exercised live (worth a click-through once the full stack is up)

- The whole flow was validated by build/typecheck/lint/unit only. Drive it against the real Next dev
  server + identity-access-service `:5002` (+ gateway `:8000` for operator flows). The `/verify`
  harness recipe is in `frontend/docs/ui-ux-dashboard-pass-2026-07-10.md` (mint HS256 session cookie,
  role Administrator, load `/dashboard`).
- Specifically eyeball: accordion open/fold + auto-open on add; the #146 preview fetch/cache/refresh;
  the play-mode warning banner.

## Suggested skills for the next session

- **`/run`** — launch the app to eyeball the accordion + #146 preview.
- **`/verify`** — drive the trivia-preview and accordion end-to-end (recipe in the prior handoff).
- **`/code-review`** — before committing this pass (and the stacked dashboard pass).

## Pointers

- Issue: **#146** (`gh api repos/{owner}/{repo}/issues/146`) — trivia substage quiz preview.
- Prior stacked work: `frontend/docs/ui-ux-dashboard-pass-2026-07-10.md`.
- Next.js here is non-standard — read `frontend/AGENTS.md` before coding.
- Repo gotcha (memory): `gh pr edit --title/--body` silently aborts in this repo; use `gh api` REST PATCH.
