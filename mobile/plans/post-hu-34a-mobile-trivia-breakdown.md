# Post-HU-34 Mobile Trivia Breakdown

Concrete mobile HU / enabler sequence for participant trivia gameplay against
`session-operations-service`.

Date: 2026-06-04 · Revised 2026-07-09 against live tickets DES-81/82/83/84.
`HU-34A` was merged into plain `HU-34` (DES-46) on 2026-07-09; it absorbed `HU-34B`.
The filename keeps the old `hu-34a` spelling because all four tickets cite it as Parent.

---

## What actually gates what

`HU-22` (timer) and `HU-33A` (active-question orchestration) have shipped, so the
**display** half of trivia gameplay is buildable now. `HU-34` (DES-46, first-valid-answer
acceptance) is still unstarted, and it gates the **submit** half only.

- `HU-22` (DES-77, Done) — authoritative timer.
- `HU-33A` (DES-78, Done) — active-question orchestration.
- `HU-33B` (DES-45, Done) — question close + final results.
- `HU-34` (DES-46, Todo) — first-valid-answer acceptance. **Gates `HU-M2` alone.**

The submit-answer shape does not exist yet; `HU-34` authors it. No mobile slice may
freeze it in advance — `HU-M2` consumes it once it is real.

Today the mobile baseline is:

- participant login
- session-code entry
- team lobby
- team-space admission / reconnect shell

It does **not** yet display trivia questions, answer options, reveal state, or ranking.

---

## Recommended order

This is a branch, not a chain. `HU-M3` does not wait on `HU-M2`.

1. `EN-M1` — mobile gameplay contract spike — *startable now*
2. `HU-M1` — active question display + countdown — *after `EN-M1`*
3. `HU-M3` — question-closed participant state — *after `HU-M1`; needs no backend work*
   `HU-M2` — submit first answer from mobile — *after `HU-M1` **and** `HU-34` (DES-46)*
4. `HU-M4` — result reveal + explanation — *scoring/reveal era*
   `HU-M5` — live score / ranking — *scoring/reveal era*

---

## EN-M1 — Mobile gameplay contract spike

**Depends on:** nothing. Display-only scope; the submit contract is deferred to `HU-M2`.

### Goal

Freeze the participant-facing runtime contract for `team-space` so mobile can implement
gameplay against verified backend shapes instead of guessed DTOs and event names.

### Backend contract to freeze

- current active question snapshot
- remaining time
- question-closed event shape

Deferred, not frozen here — no backend shape exists yet:

- whether the caller's team already answered → `HU-34` (DES-46)
- submit-answer invoke / endpoint shape → `HU-34` (DES-46)
- reveal event shape → `HU-35` (DES-48)
- score / ranking event shape → `HU-37` / `HU-39` (DES-51 / DES-54)

### Mobile deliverables

- typed DTOs in `mobile/src/lib/realtime/`
- explicit runtime vocabulary for trivia state in the hub client
- a short contract note if the shapes are not yet documented elsewhere

### Notes

This is the gating enabler for `HU-M1` and everything downstream. Without it, question
rendering would be speculative. Answer submission is out of scope by design.

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

**Depends on:** `HU-M1`, `HU-34` (DES-46). Consumes the submit contract `HU-34` authors —
it is not frozen by `EN-M1`.

### Goal

Allow the participant team to submit an answer from the mobile client during an active
question.

### Mobile scope

- option selection UI
- submit action wired to the verified backend contract
- local pending state while submission is in flight
- interaction lock after successful submission
- “answer submitted” waiting state after an accepted submission
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

## HU-M3 — Question-closed participant state

**Depends on:** `HU-M1`. Close facts from `HU-33B` (DES-45, Done). **Not blocked by
`HU-M2`** — close-state UI stands on its own.

### Goal

Keep the participant experience coherent when the question window closes. Post-submit
waiting state belongs to `HU-M2`, which owns submission.

### Mobile scope

- “question closed” state when the timer expires or the backend closes the question
- immediate interaction lock once the question is closed
- re-fetch the participant-timer snapshot after close to discover the next question
- clear distinction between:
  - team already answered — only reachable once `HU-M2` ships
  - question closed before this team answered — the only observable variant until then
  - waiting for reveal / next state

### Acceptance criteria

- after close, answer controls are no longer interactive
- after close, the next active question or a waiting state is rendered from the snapshot
- reconnecting into a closed question restores the correct closed/waiting state

### Notes

With `HU-M1` this completes the playable display loop without any new backend work.
`HU-M2` closes the interaction loop once `HU-34` lands.

---

## HU-M4 — Result reveal + explanation

**Depends on:** `HU-35` (DES-48). Close facts from `HU-33B` (DES-45, Done).

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

**Depends on:** `HU-37` (DES-51), `HU-39` (DES-54). `HU-37B` and `HU-39B` were canceled on
2026-07-09 and folded into `HU-39`; do not cite them.

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

Two cuts, split by whether `HU-34` (DES-46) has landed.

**Display cut — buildable today, no backend work:**

1. `EN-M1`
2. `HU-M1`
3. `HU-M3`

`login → session code → team lobby → team-space → active question + countdown → question closes → next question`

Participants watch the trivia run without answering. This is the honest ceiling until
`HU-34` ships.

**Interaction cut — adds `HU-M2` once `HU-34` (DES-46) lands:**

`… → active question → submit answer → wait for close`

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
