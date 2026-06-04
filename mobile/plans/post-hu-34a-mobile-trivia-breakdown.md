# Post-HU-34A Mobile Trivia Breakdown

Concrete mobile HU / enabler sequence for the participant app after `HU-34A` exists in
`session-operations-service`.

Date: 2026-06-04

---

## Why this starts after HU-34A

`HU-34A` is the first point where the backend can honestly support participant trivia
gameplay on mobile:

- `HU-22` provides the authoritative timer.
- `HU-33A` provides active-question orchestration.
- `HU-34A` provides first-valid-answer acceptance for a team.

Before that, the mobile app can admit a participant into a live team space, but it does
not yet have a verified trivia-runtime contract for rendering questions or submitting
answers.

Today the mobile baseline is:

- participant login
- session-code entry
- team lobby
- team-space admission / reconnect shell

It does **not** yet display trivia questions, answer options, reveal state, or ranking.

---

## Recommended order

1. `EN-M1` — mobile gameplay contract spike
2. `HU-M1` — active question display + countdown
3. `HU-M2` — submit first answer from mobile
4. `HU-M3` — post-submit / question-closed state
5. `HU-M4` — result reveal + explanation
6. `HU-M5` — live score / ranking

---

## EN-M1 — Mobile gameplay contract spike

**Depends on:** `HU-34A`

### Goal

Freeze the participant-facing runtime contract for `team-space` so mobile can implement
gameplay against verified backend shapes instead of guessed DTOs and event names.

### Backend contract to freeze

- current active question snapshot
- remaining time
- whether the caller's team already answered
- submit-answer invoke / endpoint shape
- question-closed event shape
- reveal event shape
- score / ranking event shape if available

### Mobile deliverables

- typed DTOs in `mobile/src/lib/realtime/`
- explicit runtime vocabulary for trivia state in the hub client
- a short contract note if the shapes are not yet documented elsewhere

### Notes

This is the gating enabler for the rest of the mobile gameplay work. Without it, question
rendering and answer submission would be speculative.

---

## HU-M1 — Active question display + countdown

**Depends on:** `EN-M1`, `HU-22`, `HU-33A`

### Goal

Replace the current team-space placeholder shell with a real participant gameplay screen
that shows the active trivia question.

### Mobile scope

- extend `team-space.tsx` to render:
  - question text
  - answer options
  - remaining time
- add explicit empty states:
  - reconnecting
  - waiting for next question
  - no active question
  - session no longer accepting participation
- keep timer display authoritative to the backend runtime, not device-local invention

### Acceptance criteria

- participant who enters team-space during an active question sees the active question
- countdown updates from the verified session runtime
- participant who enters between questions sees a waiting state instead of stale question UI

---

## HU-M2 — Submit first answer from mobile

**Depends on:** `HU-M1`, `HU-34A`

### Goal

Allow the participant team to submit an answer from the mobile client during an active
question.

### Mobile scope

- option selection UI
- submit action wired to the verified backend contract
- local pending state while submission is in flight
- interaction lock after successful submission
- rejection handling for:
  - late answer
  - duplicate answer
  - invalid session/question state
  - forbidden / wrong-team access
  - network failure

### Acceptance criteria

- a team can submit one answer from the app during an active question
- the screen prevents accidental double-submit while awaiting the backend result
- backend rejection reasons map to participant-facing UI without inventing domain rules

---

## HU-M3 — Post-submit / question-closed participant state

**Depends on:** `HU-M2`, close semantics from `HU-33A`

### Goal

Keep the participant experience coherent after a submission succeeds and when the question
window closes.

### Mobile scope

- “answer submitted” waiting state after accepted submission
- “question closed” state when the timer expires or the backend closes the question
- immediate interaction lock once the question is closed
- clear distinction between:
  - team already answered
  - question closed before this team answered
  - waiting for reveal / next state

### Acceptance criteria

- after a successful answer, the participant sees a stable waiting state
- after close, answer controls are no longer interactive
- reconnecting into a closed question restores the correct closed/waiting state

### Notes

This is the minimum honest finish for the `HU-34A` era. It completes the playable
question loop even before reveal and ranking land.

---

## HU-M4 — Result reveal + explanation

**Depends on:** `HU-35`, likely `HU-33B`

### Goal

Show participants the outcome of the closed question.

### Mobile scope

- reveal correct / incorrect outcome
- highlight the correct option
- display optional explanation text from the quiz snapshot
- transition cleanly from closed state to reveal state

### Acceptance criteria

- participant sees whether the team answer was correct
- participant sees the correct option after reveal
- explanation appears when the backend provides it

---

## HU-M5 — Live score / ranking

**Depends on:** `HU-37A`, `HU-37B`, `HU-39B`

### Goal

Expose score and ranking feedback to participants once scoring and ranking are available as
verified contracts.

### Mobile scope

- current team score
- optional session leaderboard / ranking table
- real-time updates when score or ranking changes

### Acceptance criteria

- participant sees current team score after scoring updates land
- participant sees ranking updates if the backend exposes them in this slice

### Notes

Do not start this early. It depends on scoring and ranking contracts, not just answer
submission.

---

## Smallest playable mobile trivia cut

If the goal is the smallest playable participant experience immediately after `HU-34A`,
build only:

1. `EN-M1`
2. `HU-M1`
3. `HU-M2`
4. `HU-M3`

That yields the minimal honest loop:

`login → session code → team lobby → team-space → active question → submit answer → wait for close`

Leave reveal and ranking for the next slices once their backend contracts are real.

---

## Recommended repo touchpoints

Primary mobile files likely involved:

- `mobile/src/app/(app)/team-space.tsx`
- `mobile/src/lib/realtime/sessions-hub.ts`
- `mobile/src/lib/realtime/sessions-hub-types.ts`
- `mobile/src/lib/realtime/use-reconnect.ts`

Likely new mobile modules:

- gameplay-specific hook(s) beside the existing membership / reconnect hooks
- typed trivia-runtime DTOs
- tests for active-question, submit, and closed-state behavior

Keep `team-lobby` as the admission screen. Do not move trivia gameplay UI into the lobby.
