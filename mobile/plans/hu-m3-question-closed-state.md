# Plan: HU-M3 Mobile — Question-Closed Participant State

**Ref:** HU-M3 (DES-83)
**Date:** 2026-07-11
**Scope:** Participant-facing question-closed state for the Expo mobile app — react to the
`QuestionClosed` broadcast, lock the answer controls, re-fetch the participant timer snapshot to
resolve into the next question / a waiting state / a terminal state.
**Builds on:** HU-M1 (DES-82) active-question display (`team-space.tsx`, `use-active-question.ts`,
`active-question-stage.tsx`), HU-M2 (DES-84) submit (`use-submit-answer.ts`), the frozen EN-M1
runtime contract (`mobile/docs/en-m1-mobile-trivia-contract.md`).

> This is the **close-state** slice. It is display + state-reconciliation only. It does **not** add or
> change submit behavior — HU-M2/DES-84 owns the submit network path, its rejection handling, and the
> post-submit "Answer submitted" lock. The lock this plan adds is a *display-only* lock driven by the
> close event, threaded independently of the submit hook. It also does **not** reveal correctness or
> score — those are withheld from every realtime surface by design and surface later (HU-35 / HU-37).

---

## Context

The participant's live view is `LiveTeamSpace` inside `src/app/(app)/team-space.tsx`. Post-HU-M2 it
renders `ActiveQuestionStage` (question + options + submit) when `useActiveQuestion` reports
`view.kind === 'active'`, and `QuestionEmptyState` for `waiting` / `none` / `closed`.

**What already exists (do not rebuild):**

- `useActiveQuestion` (`src/lib/realtime/use-active-question.ts`) already subscribes to
  `onQuestionClosed`, `onSubstageAdvanced`, `onStateChanged`, `onQuestionActivated` and already maps
  terminal `Finished` / `Cancelled` states to `{ kind: 'closed' }`.
- `QuestionEmptyState` (`src/components/question-empty-state.tsx`) already renders `waiting`, `none`,
  and `closed` copy, with a `Cancelled`-vs-`Finished` split for the terminal body.
- `SessionsHubClient.onQuestionClosed` and the `QuestionClosedNotificationDto` type already exist
  (`sessions-hub.ts`, `trivia-types.ts`), frozen by EN-M1.
- `useSessionTimer` (`src/lib/realtime/use-session-timer.ts`) already owns the snapshot fetch via
  `getParticipantTimerSnapshot`, keyed on `isReconnected` + `reconnectNonce`, and returns
  `activeQuestion` + `sessionState` that seed `useActiveQuestion`.

**The actual HU-M3 gap.** Today `QuestionClosed` does two things that fall short of DES-83:

1. It **immediately blanks** the active question to `{ kind: 'waiting' }`. There is no deliberate
   *closed lock* — the controls simply disappear rather than being locked with a "question closed"
   affordance. The participant never sees the just-closed question settle.
2. It performs **no REST re-fetch**. The client passively waits for the next `QuestionActivated`
   push. There is no authoritative reconciliation to discover the next question, a genuine waiting
   gap, or a terminal state that a dropped `SessionStateChanged` would otherwise miss.

There is also a latent bug HU-M3 should fix: the close handler transitions on **any** `QuestionClosed`
regardless of index, so a close for question *N* can blank an already-pushed question *N+1*.

HU-M3 replaces "blank straight to waiting" with **lock → re-fetch snapshot → reconcile**:

- On `QuestionClosed` for the *currently displayed* question → keep it on screen, lock its controls,
  and trigger a participant-timer snapshot re-fetch.
- When the re-fetch resolves (or a `QuestionActivated` push arrives first), reconcile to the next
  active question, a waiting gap, or a terminal closed state.

---

## Backend contract (from the frozen EN-M1 spike — no backend change needed)

Everything HU-M3 needs is already frozen in `mobile/docs/en-m1-mobile-trivia-contract.md` and typed in
`trivia-types.ts` / `timer-types.ts`. **No new backend work.**

### `QuestionClosed` — SignalR push (verified)

Method `"QuestionClosed"`, broadcast to group `live-session:{liveSessionId:D}`. Payload
`QuestionClosedNotificationDto`:

```ts
export type QuestionClosedNotificationDto = {
  liveSessionId: string
  questionIndex: number
  closedAt: string          // ISO-8601
  wasExpiredByTimer: boolean
}
```

Contract notes:

- The event keys the closed question by **`questionIndex`** only — no `sequenceOrder`, no substage id.
  Match it against the currently displayed question's `questionIndex` to decide whether the close is
  for what the participant is looking at (vs. a stale close for an already-superseded question).
- `wasExpiredByTimer` distinguishes "timer ran out" from "operator advanced early". HU-M3 may use it
  for copy tone ("Time's up" vs "Question closed") but must not infer correctness from it.
- Filter by `liveSessionId` (the participant is in exactly one session group), same as every other
  handler in `useActiveQuestion`.

### Participant timer snapshot — REST re-fetch (verified, reused)

```
GET /api/sessions/{liveSessionId}/participants/timer?teamId={teamId}&token={token}
```

Already wrapped by `getParticipantTimerSnapshot` and consumed by `useSessionTimer`. Response
`SessionTimerSnapshotDto` nests the reconnect/late-join active question:

```ts
activeQuestion?: ActiveQuestionSnapshotDto | null   // null when no question is active
sessionState: string                                // "Active" | "Paused" | "Finished" | "Cancelled" | ...
```

HU-M3 re-uses this exact call as the close-reconciliation fetch — the snapshot's `activeQuestion`
(next question or `null`) plus `sessionState` (terminal or not) is the authority for what comes after a
close. **No second fetch path is introduced.**

---

## Architecture decisions

- **Reuse the one snapshot fetcher.** The re-fetch is `useSessionTimer`'s existing
  `getParticipantTimerSnapshot`, re-triggered by a nonce — not a new API helper and not a second fetch
  in `useActiveQuestion`. This keeps a single source of snapshot truth that both the timer and the
  active-question hook already read.
- **A close is a re-sync trigger, mirroring reconnect.** `useSessionTimer` already re-fetches on
  `reconnectNonce`. HU-M3 adds a `resyncNonce` that a `QuestionClosed` bumps, driving the same fetch
  effect and re-seeding `useActiveQuestion` through the exact snapshot-prop path a reconnect uses.
  Same proven mechanism, new cause.
- **Lock, don't blank.** On close, the just-closed question stays rendered with disabled controls and
  a close affordance until the reconcile resolves — a deliberate settle, not an instant disappearance.
- **Index-guarded close.** Only a `QuestionClosed` whose `questionIndex` matches the displayed
  question locks it. A stale close for a superseded question is ignored (fixes the latent blank-newer
  bug).
- **Broadcast fast-path stays; re-fetch is the safety net.** `QuestionActivated` remains the quick
  path to the next question; the re-fetch authoritatively covers a missed push, a genuine waiting gap,
  and — critically — a terminal state a dropped `SessionStateChanged` would otherwise miss. Whichever
  resolves first wins; the paths converge on the same view.
- **Display-only lock, submit untouched.** The close lock is a new `isClosed` display flag threaded to
  `ActiveQuestionStage`, independent of the submit hook's `isLocked`. `use-submit-answer.ts` is not
  modified. The backend already rejects a post-close submit with `trivia-answer-requires-active-question`;
  the display lock is the UI guard in front of that authority.
- **No correctness / score.** The closed question shows no right/wrong and no points (contract:
  withheld from realtime by design).
- **Reconcile guards against echo.** If the re-fetched snapshot still reports the just-closed question
  (same `questionIndex`, a backend race), do not "re-open" it — hold the locked/closed state and fall
  to `waiting` rather than unlocking a dead question.
- **Missing/failed re-fetch never strands.** A snapshot error or `null` active question on a
  non-terminal session resolves to `waiting`, never a stale interactive question.

---

## Phases

### Phase 1 — Snapshot re-sync plumbing

**Scope**

- Add an optional `resyncNonce: number` input to `useSessionTimer` and include it in the snapshot-fetch
  effect's dependency array (alongside `reconnectNonce`), so bumping it forces a fresh
  `getParticipantTimerSnapshot`. Leave `onTimerUpdated` and the reconnect path unchanged.
- In `LiveTeamSpace`, lift `const [resyncNonce, setResyncNonce] = useState(0)` and a
  `requestResync = useCallback(() => setResyncNonce(n => n + 1), [])`. Pass `resyncNonce` to
  `useSessionTimer`.

**Gate** — a unit test on `useSessionTimer` proves that bumping `resyncNonce` issues a second
`getParticipantTimerSnapshot` and applies the new `activeQuestion` / `sessionState`; the existing
reconnect-nonce re-fetch and event-driven paths are unregressed.

---

### Phase 2 — Close-driven lock + reconcile in `useActiveQuestion`

**Scope**

- Add a `requestResync?: () => void` prop (the Phase-1 bump) to `useActiveQuestion`, and a
  `resyncNonce` prop it depends on for re-seeding (same treatment as `reconnectNonce`).
- Rework the `onQuestionClosed` handler:
  - Ignore the event unless `notification.questionIndex` matches the currently displayed active
    question's `questionIndex` (stale-close guard). If no question is currently displayed, keep the
    existing `none` behavior.
  - On a matching close: **keep `kind: 'active'`**, set a new closed flag, and call `requestResync()`.
    Do **not** jump straight to `waiting`.
- Reconcile on the re-fetched snapshot (through the existing `snapshotActiveQuestion` /
  `snapshotSessionState` + nonce re-seed effect, now also keyed on `resyncNonce`):
  - Terminal `sessionState` → `{ kind: 'closed' }`.
  - A snapshot `activeQuestion` with a **different** `questionIndex` than the just-closed one →
    `{ kind: 'active', question }` (unlocked, fresh).
  - A snapshot `activeQuestion` echoing the **same** just-closed `questionIndex` → treat as not-yet-
    advanced: hold closed, resolve to `waiting`.
  - No snapshot `activeQuestion`, non-terminal → `{ kind: 'waiting' }`.
- Keep the broadcast convergence intact: a `QuestionActivated` push (different index) still moves
  straight to the next active question and clears the closed flag; `SubstageAdvanced` (→ waiting or,
  on `toSubstageId === null`, closed) and terminal `SessionStateChanged` still apply.
- Surface the closed flag in the result, e.g. extend to
  `{ view, sessionState, isQuestionClosed }` (or fold a `closed: boolean` onto the `active` view —
  pick the lower-churn shape when implementing; the screen only needs one boolean to thread down).

**Gate** — unit tests (extend `active-question-hook.test.ts`):

- Close matching the active question keeps it visible, sets `isQuestionClosed`, and calls
  `requestResync`.
- Re-seed with a new-index snapshot question advances and clears the closed flag.
- Re-seed with no active question + non-terminal state resolves to `waiting`.
- Re-seed with a terminal state resolves to `closed`.
- Re-seed echoing the same just-closed index does **not** re-open — resolves to `waiting`.
- A stale close (index ≠ displayed question) is ignored and does not blank a newer question.
- Existing tests that asserted "close → immediate `waiting`" are updated to the lock-then-reconcile
  behavior.

---

### Phase 3 — Close-state UI

**Scope**

- Thread an `isClosed?: boolean` prop into `ActiveQuestionStage`:
  - Disable option-row interaction (render rows as non-pressable `View`s, `accessibilityState`
    `disabled`), independent of the submit hook's `locked`.
  - Replace the Submit control with a close affordance distinct from the success "Answer submitted"
    chip — neutral/critical tone (`colors.textMuted` / `colors.signalCritical`), copy like
    "Question closed — waiting for the next" (or "Time's up" when `wasExpiredByTimer`, if that flag is
    threaded through; optional).
  - Do not alter the submit `onSubmit` path or the `isLocked`/"Answer submitted" branch — the closed
    branch is additive and takes precedence for display when `isClosed` is true.
- Confirm `QuestionEmptyState` `waiting` / `closed` copy still reads correctly for the post-close
  transition; terminal `Finished` vs `Cancelled` split already exists — reuse as-is.

**Gate** — component test: `ActiveQuestionStage` with `isClosed` renders disabled rows + the close
affordance and hides the interactive Submit button; a normal (non-closed) render is unchanged;
visuals follow `mobile/DESIGN.md` (warm surfaces, restrained accent, full borders, no nested cards).

---

### Phase 4 — Wire into `LiveTeamSpace`

**Scope**

- Pass `requestResync` and `resyncNonce` into `useActiveQuestion`; pass `resyncNonce` into
  `useSessionTimer` (Phase 1).
- Thread the hook's `isQuestionClosed` into `<ActiveQuestionStage isClosed={…} />`.
- Leave the `useSubmitAnswer` wiring and props exactly as HU-M2 shipped them.

**Gate** — drive the flow (RTL + a fake hub client): activate a question → fire `QuestionClosed` →
assert controls lock and the close affordance shows → resolve the re-fetch to (a) a next question
(advances), (b) `null` non-terminal (waiting), (c) terminal (session-closed panel). No change to
submit behavior in any branch.

---

### Phase 5 — Tests

- Unit: `useSessionTimer` resync re-fetch (Phase 1); `useActiveQuestion` lock/reconcile matrix
  (Phase 2); `ActiveQuestionStage` closed-state render (Phase 3).
- Integration: `team-space` activate → close → reconcile into next / waiting / terminal, plus the
  stale-close no-op. Follow the patterns in `src/__tests__/team-space-question-stage.test.tsx` and
  `active-question-hook.test.ts`.

---

## Acceptance criteria

- When `QuestionClosed` arrives for the displayed question, the answer controls lock (options
  non-interactive, Submit replaced by a close affordance) — the question does not vanish instantly.
- The close triggers a participant-timer snapshot re-fetch (`GET …/participants/timer`), reusing the
  existing helper.
- The re-fetch (or an earlier `QuestionActivated` push) reconciles the view: a new question advances
  and unlocks; no active question on a live session shows `waiting`; a terminal `Finished`/`Cancelled`
  state shows the session-closed panel (with the cancelled-vs-finished copy split).
- A stale `QuestionClosed` (index ≠ displayed question) is ignored and never blanks a newer question.
- A re-fetch that echoes the just-closed question does not re-open it; a failed/empty re-fetch on a
  live session falls to `waiting`, never a stale interactive question.
- No correctness or score is shown on close. Submit behavior (network call, rejection handling,
  post-submit lock) is unchanged from HU-M2.
- Visuals follow `mobile/DESIGN.md`: warm surfaces, restrained ember accent, full borders, tabular
  live data, no nested cards.

---

## Out of scope

- Submit / answer network behavior, rejection handling, and the post-submit "Answer submitted" lock
  (HU-M2 / DES-84).
- Revealing correctness, the selected option, or score after close (HU-35 / HU-37 / downstream
  scoring — withheld from realtime by design).
- Any backend change — the `QuestionClosed` shape and the timer-snapshot endpoint are already frozen
  by EN-M1.
- Round/leaderboard transitions, inter-substage results screens, and non-trivia substage close states.
- Local per-second interpolation of the timer (unchanged from HU-22 — pure backend ticks).
