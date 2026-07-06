# Plan: HU-33A Frontend — Synchronized Trivia Substage Orchestration

**Ref:** HU-33A
**Branch:** feature/hu-33a-trivia-substage-orchestration-realign
**Date:** 2026-07-06
**Builds on:** HU-22 operator live surface (`DashboardClient` operator live panel, `useTriviaRoundState`,
`createSessionStateRealtimeClient`, `OperatorSessionTimerPanel`, `TriviaRoundPanel`, `getSessionTimerSnapshotAction`)

---

## Context

Backend HU-33A is implemented. **There is no new REST contract in this slice (D-3).** The synchronized
active question is already read from the existing operator/participant timer endpoints via
`SessionTimerSnapshotDto.activeQuestion` — its value now tracks the **active substage's** question rather
than a flat session-level question list. The frontend already renders that snapshot's `activeQuestion` +
live countdown in the operator live panel.

Two behavioural facts change on the backend and must be reflected in the UI:

1. **The round advances substage-by-substage.** A new SignalR signal `SubstageAdvanced`
   (`fromSubstageId`, `fromPlayMode`, `toSubstageId?`) is broadcast over the `live-session:{id}` group
   whenever orchestration moves from one substage to the next. `toSubstageId` absent ⇒ the **final**
   substage just completed and the session is about to finish.
2. **The session Finishes only after the final substage.** There is no session-level "round over when the
   question list runs out". Completion is signalled by the existing `SessionStateChanged` → `Finished`.

The existing frontend model has two stale assumptions to remove:

- The mock admin/overview data in `DashboardClient` renders a **flat-list** "N of M questions answered"
  progress and a "Questions answered" metric — the standalone-trivia / questions-exhausted model.
- Copy referring to "Active trivia sessions … round progress" frames a session as a standalone trivia list.

This is a **focused real-time read surface with no new endpoint** — modelled on the small-surface
`hu-03` exemplar. The synchronized-question UI is the existing operator live monitoring panel
(read-only; the operator does not advance substages). See **Out of Scope** for the participant live-play
surface, which does not exist yet and is not created here.

---

## Verified anchors (confirmed against source)

All confirmed by reading the files — not from memory:

- `app/lib/definitions.ts`
  - `TriviaRoundPhase = 'idle' | 'pregame' | 'question-active' | 'between-questions'` (l.400) — extended in P1.
  - `ActiveQuestionSnapshotDto`, `QuestionActivatedNotificationDto`, `QuestionClosedNotificationDto`,
    `SessionStateChangedNotificationDto`, `SessionTimerSnapshotDto` (with `.activeQuestion`) all exist.
    `SessionTimerSnapshotDto`'s doc comment already states `remainingSeconds`/`activeQuestion` track the
    **active substage** — no change needed there.
  - `MissionSubstageDto.playMode: 'TreasureHunt' | 'Trivia'` (l.108) — reuse this literal union for `fromPlayMode`.
- `app/lib/realtime/use-trivia-round-state.ts` — `useTriviaRoundState()` returns state + handlers
  (`handlePregameTimerTick`, `handleQuestionActivated`, `hydrateActiveQuestion`, `handleQuestionClosed`,
  `reset`). `initialState` at l.26. `startQuestionCountdown` / `clearCountdown` are private locals.
- `app/lib/realtime/session-state-client.ts` — `SessionStateClientOptions` (l.22), the `connection.on(...)`
  registrations (l.169–189), and the `normalizeX` helpers pattern (camel ?? Pascal ?? default).
- `app/dashboard/DashboardClient.tsx`
  - Destructures the round handlers at l.257–263; calls `createSessionStateRealtimeClient` at l.337 with
    `onStateChanged` (l.340), `onTimerUpdated`, `onQuestionActivated` (l.395), `onQuestionClosed` (l.413).
  - `onStateChanged` currently does `if (notification.currentState !== 'Active') resetTriviaRound()` (l.354).
  - Effect dep array at l.438–446.
  - `TriviaRoundPanel` rendered at l.844 (operator live hero); `OperatorSessionTimerPanel` at l.838.
  - Mock `Session` type has `questionsAnswered`/`questionsTotal` (l.51–52); the `sessions` array (l.75–124)
    fills them; admin "Live sessions" card renders "N of M questions answered" (l.1086); `adminMetrics`
    has a "Questions answered" tile (l.137); "Active trivia sessions … round progress" copy (l.1071).
- `app/dashboard/TriviaRoundPanel.tsx` — pure presentational, branches on `phase`; question header eyebrow
  is `Question {activeQuestion.sequenceOrder}` (l.61); `between-questions` is the fallthrough (l.84–92).
- `app/dashboard/triviaRoundPanel.module.css` — available classes: `.panel .eyebrow .questionHeader
  .transition .transitionLine .transitionText .prompt .bar .barFill` (reuse `.transition*` for the new banners).
- `app/dashboard/OperatorSessionTimerPanel.tsx` — read-only; no advance control (nothing to remove for
  "operator does not advance").
- `tests/unit/app/dashboard/operator-session-timer-panel.test.ts` — renders component HTML directly with
  `react-dom/server` and asserts substrings; the lightweight unit-test pattern the new tests follow.

---

## Architecture decisions

- **The active-substage question surface already exists — extend it, don't rebuild it.** The operator live
  hero already renders `activeQuestion` + countdown from the timer read and `QuestionActivated`/
  `QuestionClosed`. This slice adds substage awareness and completion semantics on top; it does not touch
  the read path (`getSessionTimerSnapshotAction`) or the endpoints.
- **`SubstageAdvanced` is a new client concept tracked in the round state machine.** `useTriviaRoundState`
  gains a `substageOrdinal` (1-based, client-derived counter) and a `finalizing` flag. Because the signal
  carries only **ids** (`fromSubstageId`, `toSubstageId`), not titles or a sequence number, the UI shows a
  derived ordinal ("Substage 2"), not a substage name — see Open Questions.
- **Ordinal is derived by counting advances, not read.** The session starts in substage 1; no signal fires
  for entering the first substage. So `substageOrdinal` starts at `1` and increments on each
  `SubstageAdvanced` that carries a `toSubstageId`. An advance with **no** `toSubstageId` sets `finalizing`
  (the final substage is done) without incrementing.
- **Completion is a distinct phase driven by `SessionStateChanged` → `Finished`.** Today `onStateChanged`
  calls `resetTriviaRound()` for any non-Active state, which collapses the panel to `idle` (renders null).
  For `Finished` we instead enter a new `complete` phase so the panel shows "Session complete — all
  substages finished" only after the final substage. Other non-Active states (Paused/Cancelled/Scheduled)
  keep the existing `reset` behaviour.
- **`SubstageAdvanced` clears the active-question window in the timer snapshot**, mirroring `onQuestionClosed`
  (a substage boundary means the prior question is gone), so `OperatorSessionTimerPanel` returns to its
  no-active-question state between the advance and the next `QuestionActivated`.
- **Between-questions vs substage-advancing are separate transitions.** `QuestionClosed` → `between-questions`
  ("Next question loading…") is within a substage; `SubstageAdvanced` → `substage-advancing` is across
  substages. The advance signal overrides the brief between-questions state that precedes it.
- **Remove the flat-list question model from mock/overview copy.** The `questionsAnswered/questionsTotal`
  fields, the "N of M questions answered" subtitle, and the "Questions answered" metric encode the
  standalone-trivia / questions-exhausted model the gate forbids. They are demo placeholders (no live data
  behind them), so they are removed / reworded to substage-neutral copy rather than re-plumbed.

---

## Environment

No new environment variables. No new endpoints, no `app/lib/*.ts` fetch additions, no Server Actions.

---

## Phases

### Phase 1 — Types

**Scope:** `app/lib/definitions.ts` only.

Add the `SubstageAdvanced` payload type and extend the round-phase union. Place the DTO next to the other
SignalR payloads (after `QuestionClosedNotificationDto`, l.397).

```ts
// SignalR "SubstageAdvanced" hub event payload (live-session:{id} group).
// Broadcast when orchestration moves from one substage to the next.
// toSubstageId absent/null ⇒ the FINAL substage just completed; the session will Finish next.
export type SubstageAdvancedNotificationDto = {
  liveSessionId: string
  fromSubstageId: string
  fromPlayMode: 'TreasureHunt' | 'Trivia' | string
  toSubstageId: string | null
}
```

Extend the phase union (l.400):

```ts
export type TriviaRoundPhase =
  | 'idle'
  | 'pregame'
  | 'question-active'
  | 'between-questions'
  | 'substage-advancing'
  | 'complete'
```

**Gate:** `pnpm exec tsc --noEmit` may surface exhaustiveness/prop errors in `TriviaRoundPanel` and
`use-trivia-round-state.ts` — expected, resolved in P2/P5.

---

### Phase 2 — Round state machine

**Scope:** `app/lib/realtime/use-trivia-round-state.ts`.

Add substage tracking + two handlers. New state fields and their defaults:

```ts
export type TriviaRoundState = {
  phase: TriviaRoundPhase
  pregameSecondsLeft: number | null
  activeQuestion: QuestionActivatedNotificationDto | null
  questionSecondsLeft: number | null // client-side approximation
  substageOrdinal: number            // 1-based; client-derived (session starts in substage 1)
  finalizing: boolean                // final substage done, session about to Finish
}

const initialState: TriviaRoundState = {
  phase: 'idle',
  pregameSecondsLeft: null,
  activeQuestion: null,
  questionSecondsLeft: null,
  substageOrdinal: 1,
  finalizing: false,
}
```

Every existing `setState({...})` that writes a full object (the two in `startQuestionCountdown` and
`handlePregameTimerTick` use spreads or full literals) must preserve the two new fields. The full-literal
write in `startQuestionCountdown` (l.72) needs `substageOrdinal`/`finalizing` carried from `current`:

```ts
// startQuestionCountdown — was a full literal; carry substage fields forward
setState((current) => ({
  phase: 'question-active',
  pregameSecondsLeft: null,
  activeQuestion: n,
  questionSecondsLeft: initialSecondsLeft,
  substageOrdinal: current.substageOrdinal,
  finalizing: false, // a new question means we're mid-substage, not finalizing
}))
```

Add the handlers (extend `TriviaRoundHandlers` and the return object):

```ts
const handleSubstageAdvanced = useCallback(
  (n: SubstageAdvancedNotificationDto) => {
    clearCountdown()
    setState((current) => ({
      ...current,
      phase: 'substage-advancing',
      activeQuestion: null,
      questionSecondsLeft: null,
      // no next substage ⇒ final substage complete; otherwise advance the ordinal
      substageOrdinal: n.toSubstageId ? current.substageOrdinal + 1 : current.substageOrdinal,
      finalizing: n.toSubstageId == null,
    }))
  },
  [clearCountdown],
)

// SessionStateChanged -> Finished. Distinct from reset(): shows the completion state
// only after the final substage rather than collapsing to idle.
const complete = useCallback(() => {
  clearCountdown()
  setState((current) => ({ ...current, phase: 'complete', activeQuestion: null, questionSecondsLeft: null }))
}, [clearCountdown])
```

Import `SubstageAdvancedNotificationDto` in the type import block. Add `handleSubstageAdvanced` and
`complete` to `TriviaRoundHandlers` and the returned object.

**Unit test** — `tests/unit/app/lib/realtime/use-trivia-round-state.test.ts` (new; follow the existing
lightweight style, drive the reducer transitions via `@testing-library/react`'s `renderHook` or a thin
manual harness). Assert: (a) ordinal starts at 1; (b) `handleSubstageAdvanced` with a `toSubstageId`
increments the ordinal and sets phase `substage-advancing`, `finalizing:false`; (c) `handleSubstageAdvanced`
with `toSubstageId:null` keeps the ordinal and sets `finalizing:true`; (d) `complete()` sets phase
`complete`; (e) `reset()` restores ordinal 1 / `finalizing:false`.

**Gate:** `pnpm exec tsc --noEmit` passes for this file; unit test passes.

---

### Phase 3 — Realtime client signal

**Scope:** `app/lib/realtime/session-state-client.ts`.

Add the `SubstageAdvanced` subscription following the exact pattern of `onQuestionClosed`.

Import `SubstageAdvancedNotificationDto`. Add to `SessionStateClientOptions`:

```ts
onSubstageAdvanced?: (notification: SubstageAdvancedNotificationDto) => void
```

Add the normalizer (camel ?? Pascal ?? default), next to `normalizeQuestionClosed`:

```ts
function normalizeSubstageAdvanced(raw: unknown): SubstageAdvancedNotificationDto {
  const n = raw as SubstageAdvancedNotificationDto & {
    LiveSessionId?: string
    FromSubstageId?: string
    FromPlayMode?: string
    ToSubstageId?: string | null
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    fromSubstageId: n.fromSubstageId ?? n.FromSubstageId ?? '',
    fromPlayMode: n.fromPlayMode ?? n.FromPlayMode ?? '',
    // absent OR null both mean "no next substage" — coalesce to null
    toSubstageId: n.toSubstageId ?? n.ToSubstageId ?? null,
  }
}
```

Destructure `onSubstageAdvanced` in `createSessionStateRealtimeClient` and register the handler beside the
`onQuestionClosed` block (l.185):

```ts
if (onSubstageAdvanced) {
  connection.on('SubstageAdvanced', (raw: unknown) => {
    onSubstageAdvanced(normalizeSubstageAdvanced(raw))
  })
}
```

**Gate:** `pnpm exec tsc --noEmit` passes.

---

### Phase 4 — Wire the signal into `DashboardClient`

**Scope:** `app/dashboard/DashboardClient.tsx`.

1. Destructure the two new handlers from `triviaRound` (l.257–263):

```ts
const {
  reset: resetTriviaRound,
  complete: completeTriviaRound,
  handlePregameTimerTick,
  handleQuestionActivated,
  hydrateActiveQuestion,
  handleQuestionClosed,
  handleSubstageAdvanced,
} = triviaRound
```

2. In `onStateChanged` (l.354), branch Finished → complete, other non-Active → reset:

```ts
if (notification.currentState === 'Finished') {
  completeTriviaRound()
} else if (notification.currentState !== 'Active') {
  resetTriviaRound()
}
```

3. Add `onSubstageAdvanced` to the client options (after `onQuestionClosed`, l.427). Advance the round state
   and clear the active-question window in the timer snapshot (mirror `onQuestionClosed`):

```ts
onSubstageAdvanced: (notification) => {
  if (notification.liveSessionId !== selectedRealtimeSessionId) return
  handleSubstageAdvanced(notification)
  dispatchTimer({
    type: 'patched',
    patch: {
      activeQuestion: null,
      totalSeconds: 0,
      remainingSeconds: 0,
      isAdvancing: false,
    },
  })
},
```

4. Add `completeTriviaRound` and `handleSubstageAdvanced` to the effect dep array (l.438–446).

Note: the timer-load / transition-result hydration paths (`loadTimerSnapshot` l.326, `runTransition` l.523)
already `resetTriviaRound()` when `sessionState !== 'Active'`; a session that hydrates already-`Finished`
will therefore land on `idle`, not `complete`. That is acceptable — the `complete` display is for the live
finish transition observed while operating; a cold-loaded finished session is reviewed read-only. (Left as
a deliberate simplification; if the completion banner is later wanted on cold load, branch those two paths
on `Finished` the same way P4.2 branches `onStateChanged`.)

**Gate:** `pnpm build` passes. Live: a `SubstageAdvanced` push flips the round panel to the advancing banner
and the timer panel to no-active-question; a `SessionStateChanged` → `Finished` shows the completion state.

---

### Phase 5 — Panel rendering

**Scope:** `app/dashboard/TriviaRoundPanel.tsx` (+ pass `substageOrdinal`/`finalizing` from `DashboardClient`).

Extend props and render the two new phases; surface the substage ordinal in the question header so the
operator monitoring view shows **which substage + question** is active.

Props:

```ts
type TriviaRoundPanelProps = {
  phase: TriviaRoundPhase
  pregameSecondsLeft: number | null
  activeQuestion: QuestionActivatedNotificationDto | null
  questionSecondsLeft: number | null
  substageOrdinal: number
  finalizing: boolean
}
```

Question header eyebrow (l.61) — add the substage ordinal:

```tsx
<span className={styles.eyebrow}>Substage {substageOrdinal} · Question {activeQuestion.sequenceOrder}</span>
```

New `substage-advancing` branch (reuse the `.transition*` classes; copy depends on `finalizing`):

```tsx
if (phase === 'substage-advancing') {
  return (
    <div className={styles.panel} data-testid="trivia-round-panel" data-phase="substage-advancing">
      <div className={styles.transition} aria-live="polite">
        <span className={styles.transitionLine} aria-hidden="true" />
        <span className={styles.transitionText}>
          {finalizing ? 'Final substage complete — finishing session…' : 'Advancing to the next substage…'}
        </span>
        <span className={styles.transitionLine} aria-hidden="true" />
      </div>
    </div>
  )
}
```

New `complete` branch (before the `between-questions` fallthrough):

```tsx
if (phase === 'complete') {
  return (
    <div className={styles.panel} data-testid="trivia-round-panel" data-phase="complete">
      <div className={styles.eyebrow}>Session complete</div>
      <p className={styles.prompt} aria-live="polite">All substages finished. This session is complete.</p>
    </div>
  )
}
```

In `DashboardClient` `TriviaRoundPanel` usage (l.844), pass the two new props:

```tsx
<TriviaRoundPanel
  phase={triviaRound.phase}
  pregameSecondsLeft={triviaRound.pregameSecondsLeft}
  activeQuestion={triviaRound.activeQuestion}
  questionSecondsLeft={triviaRound.questionSecondsLeft}
  substageOrdinal={triviaRound.substageOrdinal}
  finalizing={triviaRound.finalizing}
/>
```

**Unit test** — extend/add a `trivia-round-panel.test.ts` (mirror the operator-timer unit test's
render-to-string approach): assert the `question-active` header contains `Substage 2 · Question`, the
`substage-advancing` phase renders the advancing vs finalizing copy per `finalizing`, and `complete`
renders "Session complete".

**Gate:** `pnpm build` passes; the question header shows the active substage ordinal + question number;
advancing and completion phases render their copy.

---

### Phase 6 — Remove the flat-list / standalone-trivia model from copy & types

**Scope:** `app/dashboard/DashboardClient.tsx` mock/overview data only (no live-data path touched).

Remove the flat-list "questions exhausted" framing that the gate forbids:

- **`Session` type (l.51–52):** delete `questionsAnswered` and `questionsTotal`.
- **`sessions` array (l.75–124):** delete those two fields from each entry.
- **Admin "Live sessions" card subtitle (l.1086):** replace
  `{session.subtitle} • {session.questionsAnswered} of {session.questionsTotal} questions answered`
  with substage-neutral copy, e.g. `{session.subtitle} • {session.night}` (round/substage label already present).
- **`adminMetrics` (l.137):** replace the "Questions answered / Scoring" tile with a substage-oriented tile,
  e.g. `{ label: 'Substages in progress', value: '2', hint: 'Across tonight's sessions', pill: 'Live overview' }`
  (placeholder demo value, consistent with the other mock metrics).
- **Panel meta (l.1071):** reword `Active trivia sessions and their current round progress.` →
  `Active sessions and their current substage progress.`

Leave pure product branding ("Trivia night overview", "Tonight's trivia events") — that names the product,
not a standalone-session-ends-when-questions-run-out model.

Grep gate to prove the model is gone (must return no matches in `app/`):

```
grep -rniE "questions answered|questionsAnswered|questionsTotal|questions? (run out|exhaust)" app/
```

**Gate:** `pnpm build` passes; the grep above is empty; no UI type/copy retains a standalone-trivia or
flat-list "questions exhausted → finished" model.

---

### Phase 7 — Full gate

**Scope:** no new code — run the acceptance gates.

- `pnpm exec tsc --noEmit` — clean.
- `pnpm build` — passes.
- `pnpm exec vitest run` — the new state-machine and panel unit tests pass; existing
  `operator-session-timer-panel.test.ts` unaffected.
- Manual/behavioural confirmation of the acceptance criteria:
  - The question UI reflects the active-substage question (from the timer read) and advances on live
    `QuestionActivated` / `QuestionClosed`.
  - `SubstageAdvanced` shows the new-substage transition banner and re-points the ordinal; the timer panel
    drops to no-active-question at the boundary.
  - Completion shows only after the final substage (`SubstageAdvanced` with no `toSubstageId` → finalizing
    banner, then `SessionStateChanged` → `Finished` → "Session complete").
  - No UI type/copy retains a standalone trivia-session or flat-list questions-exhausted model.

**Gate:** all of the above green.

---

## Commit sequence

```
feat(frontend): phase 1 — SubstageAdvanced dto + round-phase union (HU-33A)
feat(frontend): phase 2 — substage tracking + complete in trivia round state (HU-33A)
feat(frontend): phase 3 — SubstageAdvanced signal on session realtime client (HU-33A)
feat(frontend): phase 4 — wire SubstageAdvanced + finished-to-complete in DashboardClient (HU-33A)
feat(frontend): phase 5 — substage-aware trivia round panel (HU-33A)
refactor(frontend): phase 6 — drop flat-list questions-exhausted model from overview copy (HU-33A)

Ref: HU-33A
```

---

## Open Questions

- **Substage identity scalar type.** `SubstageAdvanced` carries `fromSubstageId`/`toSubstageId`, but the
  runtime id type (GUID string vs int — mission-authoring `MissionSubstageDto.id` is `number`) is not
  verifiable from frontend source. Typed as `string` here and used only for a null-check + ordinal counting,
  so the code is correct either way; confirm the scalar against the hub contract before relying on the value.
- **No human-readable substage name/sequence in the payload.** The signal has ids only, so the operator view
  shows a client-derived ordinal ("Substage 2"), not the authored substage title. Surfacing titles would need
  a read model (not in this slice, D-3).
- **Entered play mode unknown.** The payload gives `fromPlayMode` (the substage being left), not the entered
  one. The panel infers Trivia-vs-other from the presence of `activeQuestion`; no entered-play-mode display.
- **Optional timestamp field.** The three listed fields are `fromSubstageId`/`fromPlayMode`/`toSubstageId`.
  If the hub also emits an `advancedAt`, the normalizer can pick it up later; not invented here.

---

## Out of Scope

- **Participant live-play surface.** No participant/team play route exists (the participant dashboard branch
  is a placeholder). Rendering the synchronized question "for all teams in lockstep" is satisfied by the
  operator monitoring surface reading the shared active-substage question; a dedicated participant play view
  is a separate, larger slice and is not created here. The `.../participants/timer` endpoint is noted only as
  the read-source symmetry.
- **Backend changes.** None (D-3).
- **Completion banner on cold-loaded finished sessions.** The `complete` phase is shown for the live finish
  transition; a session hydrated already-`Finished` lands on `idle` (reviewed read-only). See P4 note.
- **New endpoints / Server Actions / `app/lib/*.ts` fetch code.** None — this is a pure real-time read surface.
