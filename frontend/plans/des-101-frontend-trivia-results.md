# DES-101 — Frontend (operator dashboard): post-close answer/points review (HU-36B)

_Scope note: DES-101 spans two client surfaces. The **participant** half (HU-35 reveal +
ranking) lives in the **`mobile/` app** — see `mobile/plans/hu-m4-result-reveal.md`. This
`frontend/` plan covers **only the operator dashboard** half (HU-36B). Ranking is already
served on mobile (podium-leaderboard); it is **not** required in the operator dashboard by
any DES-101 AC._

Backend companion: `backend/plans/des-101-hu35-hu36b-backend-change-list.md`.
Plan shape follows `frontend/plans/hu-03-*.md` (small, code-complete surface).

---

## Context

- `frontend/` is the single-page operator/admin dashboard (`app/dashboard/DashboardClient.tsx`),
  already connected to `/hubs/sessions` via `createSessionStateRealtimeClient`
  (`app/lib/realtime/session-state-client.ts:232`) with `QuestionClosed` already a handled
  client method (line 268). The operator realtime client is constructed and started in the
  `DashboardClient.tsx:577-727` block; the group join itself
  (`JoinLiveSessionAsOperatorAsync`) happens inside the client
  (`session-state-client.ts:297/309`), not in DashboardClient.
- HU-36A shipped the **pre-close** answered board (`AnsweredMonitorPanel.tsx`), which
  deliberately omits option/correctness/points. HU-36B adds the **post-close** review that
  shows them — a sibling panel, same shape.

## Verified Backend Contract

| Concern | Method | Shape | Status |
|---|---|---|---|
| Operator post-close review | `GET /api/sessions/{id}/trivia/questions/{seq}/answer-review` (Operator) | `{ liveSessionId, questionSequenceOrder, teams: [{ teamId, teamCode, displayName, selectedOptionSequenceOrder?, isCorrect?, scoreValue?, answeredAt? }] }` | ⬜ backend §4 (new query) |
| Close trigger | SignalR `QuestionClosed` on `live-session:{id}` | `{ liveSessionId, questionIndex, closedAt, wasExpiredByTimer, ... }` | ✅ handled today (`session-state-client.ts:268`) |

_Endpoint path is a proposal — confirm against the controller once backend §4 lands (O-1)._

## Architecture Decisions

- **A-1 · Signal-triggered fetch.** Operators are already in `live-session:{id}` and receive
  `QuestionClosed`. On that signal, fetch the review via a server action — mirrors the
  existing `getOperatorSessionPanelAction` envelope pattern; keeps authz on the read path.
- **A-2 · Reuse the three-layer data split** — `'server-only'` lib fn in `app/lib/sessions.ts`
  (mirror `getOperatorSessionPanel`, line 216) + `'use server'` discriminated-envelope action
  in `app/actions/sessions.ts` (mirror `getOperatorSessionPanelAction`, line 159:
  `{ data } | { unauthorized: true } | { error }`, enforce `session.role !== 'Operator'`).
- **A-3 · New panel mirrors `AnsweredMonitorPanel.tsx`** — same `<section aria-labelledby>` +
  CSS-module structure; adds the option/correctness/points columns 36A withholds.

## Environment

- `API_GATEWAY_URL` — server-side fetch base (`app/lib/sessions.ts:27`).
- Auth on reads: `verifySession()` (`app/lib/dal.ts:10`) + `getValidAccessToken()`
  (`app/lib/keycloak-tokens.ts`) via `getGatewayHeaders()` (`app/lib/sessions.ts:29`).

## data-testid Contract

| testid | State |
|---|---|
| `trivia-answer-review-panel` | rendered after a question closes |
| `trivia-answer-review-loading` | fetch in flight |
| `trivia-answer-review-unauthorized` | action returns `unauthorized` |
| `trivia-answer-review-row-{teamId}` | one per team |
| `trivia-answer-review-correct-{teamId}` | correct / incorrect / no-answer badge |

---

## Increment 1 — Operator post-close answer/points review (HU-36B) · code-complete

**Scope**
- `app/lib/definitions.ts` — add `TriviaAnswerReviewDto` + `TriviaTeamAnswerReviewDto`
  (mirror `TriviaAnsweredMonitorDto` at line 441, plus the withheld fields
  `selectedOptionSequenceOrder?`, `isCorrect?`, `scoreValue?`).
- `app/lib/sessions.ts` — `getTriviaAnswerReview(liveSessionId, questionSequenceOrder)`
  mirroring `getOperatorSessionPanel` (line 216): same URL build, `getGatewayHeaders()`,
  `cache: 'no-store'`, status→typed-error mapping.
- `app/actions/sessions.ts` — `getTriviaAnswerReviewAction` returning the envelope; enforce
  Operator role.
- `app/dashboard/TriviaAnswerReviewPanel.tsx` (+ `triviaAnswerReviewPanel.module.css`) —
  mirror `AnsweredMonitorPanel.tsx`: per-team rows with option + correctness badge + points,
  loading/unauthorized states, the testids above.
- `app/dashboard/DashboardClient.tsx` — in the already-wired operator realtime block
  (577-727), add an `onQuestionClosed` callback dispatching `getTriviaAnswerReviewAction` for
  the just-closed question; store in reducer state; render the panel in the operator branch.

**Gate**
- `vitest run`: panel renders rows from a fixture DTO; `-unauthorized` on the envelope's
  `unauthorized`; badge reflects correct / incorrect / no-answer.
- Manual: close a question as operator → review table appears with per-team option +
  correctness + points, no reload.

---

## Acceptance Criteria → Test mapping

| AC (DES-101 · HU-36B) | Test |
|---|---|
| Operator sees each team's submitted answer after close | vitest: rows from fixture |
| Operator sees whether it was correct | vitest: badge states |
| Operator sees points awarded | vitest: points column |
| Operator review pushed real-time, no reload | manual: close → panel updates |

_HU-35 participant ACs (reveal, explanation, team correct/incorrect, ranking) are covered by
`mobile/plans/hu-m4-result-reveal.md`, not this plan._

## Open Questions / Dependencies

- **O-1** — Confirm the backend §4 endpoint path/shape once it lands; the URL here is a proposal.
- **Dep** — Increment 1 is blocked only on backend §4 (new operator review query). No frontend
  blocker.

## Out of Scope

- Backend read (companion backend change list, §4).
- Participant reveal + team ranking — those are the mobile app (`mobile/plans/hu-m4-result-reveal.md`);
  mobile ranking already exists (podium-leaderboard).
- Ranking in the operator dashboard — no DES-101 AC requires it.

## Commit Sequence

1. `feat(frontend): operator post-close trivia answer review — HU-36B`
