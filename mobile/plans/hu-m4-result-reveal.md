# Plan: HU-M4 Mobile — Result reveal + explanation

**Ref:** HU-35 (DES-101) · mobile slice **HU-M4** (see `plans/post-hu-34a-mobile-trivia-breakdown.md`)
**Date:** 2026-07-15
**Scope:** Participant-facing result reveal for the Expo mobile app — after a trivia question
closes, show the correct option, the optional explanation, and whether the team's answer was
correct (with points). Consumes the reveal data the backend adds for DES-101.
**Builds on:** HU-M1 (active-question display — `team-space.tsx`, `use-active-question.ts`,
`active-question-stage.tsx`), HU-M2 (submit — `use-submit-answer.ts`), HU-M3 (close-state lock +
snapshot re-fetch — `use-active-question.ts`), the EN-M1 runtime contract
(`mobile/docs/en-m1-mobile-trivia-contract.md`).

> This is the **reveal** slice. It renders outcome data that today is withheld by design
> (`trivia-types.ts`: `SubmitTriviaAnswerResultDto` "omits isCorrect, scoreValue ... before
> reveal (HU-35)"). It does **not** change submit (HU-M2) or the close lock (HU-M3) — it adds a
> new *reveal* state after HU-M3's closed state. Ranking already exists
> (`use-ranking.ts` + `podium-leaderboard.tsx`) — **not in scope here** (HU-35 AC4 already met).

---

## Context

The participant live view is `LiveTeamSpace` in `src/app/(app)/team-space.tsx`, rendering
`ActiveQuestionStage` (`src/components/active-question-stage.tsx`) when `useActiveQuestion`
(`src/lib/realtime/use-active-question.ts`) reports `view.kind === 'active'`.

**What already exists (do not rebuild):**
- `useActiveQuestion` already subscribes to `onQuestionClosed` and, post-HU-M3, holds the
  just-closed question on screen with a display lock + snapshot re-fetch.
- The close affordance in `active-question-stage.tsx` (~line 288: the `closed ?` branch,
  "Question closed — waiting for the next") is the **insertion point** for the reveal.
- `SessionsHubClient.onQuestionClosed` + `QuestionClosedNotificationDto` exist
  (`sessions-hub.ts`, `trivia-types.ts`), keyed by `questionIndex`.
- Reads go through `src/lib/api/sessions.ts` (`expo/fetch`, `apiBaseUrl()`, bearer via
  `getAccessToken()` in `src/lib/api/client.ts`); DTOs typed in `src/lib/realtime/*-types.ts`.

**The HU-M4 gap.** On close today, mobile shows only "Question closed" — no correct option,
no explanation, no team result. All three are absent from the mobile contract and must be
added, sourced from the backend reveal the DES-101 change adds.

## Backend contract (DES-101 — being added; see `backend/plans/des-101-hu35-hu36b-backend-change-list.md`)

Two backend items feed this slice. **Confirm the exact shapes when the backend lands (O-1);**
the shapes below are the DES-101 proposal.

### 1. Enriched `QuestionClosed` push (backend §1) — question-level reveal

`QuestionClosedNotificationDto` gains two fields (same channel/group, no new subscription):

```ts
// EXTEND src/lib/realtime/trivia-types.ts
export type QuestionClosedNotificationDto = {
  liveSessionId: string
  questionIndex: number
  closedAt: string
  wasExpiredByTimer: boolean
  correctOptionSequenceOrder: number   // NEW — 1-based, matches option row index+1
  explanation: string | null           // NEW — optional quiz-snapshot text
}
```

### 2. Participant "my team result" read (backend §2) — team-specific

```
GET /api/sessions/{liveSessionId}/trivia/questions/{sequenceOrder}/my-result   (Participant)
```
```ts
// NEW type in src/lib/realtime/trivia-types.ts
export type TriviaTeamQuestionResultDto = {
  selectedOptionSequenceOrder: number | null   // null if the team never answered
  isCorrect: boolean | null
  scoreValue: number | null
  correctOptionSequenceOrder: number
  explanation: string | null
}
```

New read fn in `src/lib/api/sessions.ts` mirroring the existing GET reads (`apiBaseUrl()` +
bearer header + ProblemDetails handling), called on close, keyed by `sequenceOrder`.

## Architecture Decisions

- **A-1 · Reveal is a new view kind.** Extend `useActiveQuestion`'s view union with
  `{ kind: 'reveal', ... }`, entered when a `QuestionClosed` for the *currently displayed*
  question arrives (reuse HU-M3's index-match). Question-level reveal (correct option +
  explanation) comes straight off the enriched push; team result is fetched (A-2).
- **A-2 · Team result via read, triggered on close.** On entering reveal, fire the
  `my-result` GET (§2) — mirrors HU-M3's snapshot re-fetch pattern. Render correctness/points
  when it resolves; show the question-level reveal immediately from the push so the UI never
  blocks on the fetch.
- **A-3 · Reveal → next reconciles as today.** HU-M3's snapshot re-fetch / `QuestionActivated`
  path still drives the transition out of reveal into the next question or waiting state.
- **A-4 · No correctness inference from `wasExpiredByTimer`** — correctness comes only from
  the backend (`isCorrect`), never from close reason.

## Increment 1 — Reveal state + UI · code-complete

**Scope**
- `src/lib/realtime/trivia-types.ts` — extend `QuestionClosedNotificationDto`; add
  `TriviaTeamQuestionResultDto`.
- `src/lib/api/sessions.ts` — add `getTriviaTeamQuestionResult(liveSessionId, sequenceOrder)`.
- `src/lib/realtime/use-active-question.ts` — add the `reveal` view kind; on index-matched
  `QuestionClosed`, capture `correctOptionSequenceOrder` + `explanation`, trigger the
  `my-result` fetch, expose `{ correctOptionSequenceOrder, explanation, teamResult }`.
- `src/components/active-question-stage.tsx` — in the `closed` branch (~line 288), when the
  view is `reveal`: highlight the option whose `index+1 === correctOptionSequenceOrder`
  (reuse the option-row styling), show a team correct/incorrect chip + points once
  `teamResult` resolves, and render `explanation` when non-null. Keep controls locked.
- `src/app/(app)/team-space.tsx` — thread the new reveal fields from `useActiveQuestion` into
  `ActiveQuestionStage`.

**testIDs** (React Native `testID`, per repo convention):
`question-reveal`, `reveal-correct-option`, `reveal-team-outcome` (`correct`/`incorrect`/`no-answer`),
`reveal-points`, `reveal-explanation`.

**Gate** (`jest` via `jest-expo`, beside `team-space-question-close.test.tsx`):
- On `QuestionClosed` for the displayed question, the stage enters reveal, highlights the
  correct option, and renders the explanation when present / omits it when null.
- When `my-result` resolves correct → outcome chip = correct + points; incorrect → incorrect;
  no answer → no-answer variant.
- Reveal does not leak before close; controls stay locked; transition to next question still
  works (HU-M3 regression intact).

## Acceptance Criteria → Test mapping (HU-35)

| AC (DES-101 · HU-35) | Covered by | Test |
|---|---|---|
| Reveal correct option on close | Inc 1 (push §1) | jest: highlight correct option |
| Explanation visible if configured | Inc 1 (push §1) | jest: explanation present/absent |
| Team told correct/incorrect | Inc 1 (read §2) | jest: outcome chip variants |
| Team sees updated ranking after close | **already shipped** (`podium-leaderboard`, `use-ranking.ts`) | existing |
| Reveal broadcast real-time, no reload | Inc 1 (existing `QuestionClosed` channel) | jest: enters reveal on push |

## Open Questions / Dependencies

- **O-1 (dependency)** — backend §1 (enriched `QuestionClosed`) + §2 (`my-result` read) must
  land first; confirm exact field names / endpoint path against the controller before wiring.
- **O-2** — team result delivery: read (A-2, default) vs. folding it into a per-`team:{id}`
  push. Default to the read unless the extra round-trip is visible on close.

## Out of Scope

- Backend work (companion backend change list).
- Ranking (already shipped on mobile — HU-35 AC4 already met).
- Operator review — that's the `frontend/` dashboard (`frontend/plans/des-101-frontend-trivia-results.md`).

## Commit Sequence

1. `feat(mobile): result reveal + explanation after question close — HU-35`
