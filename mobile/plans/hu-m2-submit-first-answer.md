# Plan: HU-M2 Mobile — Submit First Answer

**Ref:** HU-M2 (DES-84)
**Date:** 2026-07-11
**Scope:** Participant submits one trivia answer during an active question on the Expo mobile app.
**Builds on:** HU-M1 team-space.tsx (DES-82), HU-22 session timer, HU-34 first-valid-answer backend
contract.

> This plan extends the `LiveTeamSpace` rendered by `team-space.tsx` (post-HU-M1). The existing screen
> already renders question prompt + options via `ActiveQuestionStage` and drives display from
> `useActiveQuestion` (SignalR `QuestionActivated` + REST snapshot `ActiveQuestionSnapshotDto`).
> This plan adds an answer-submission path: select an option, submit once, see acceptance, lock out.
>
> It also includes a **mandatory backend prerequisite** (Phase 0): the contract requires
> `triviaSubstageSnapshotId` on submit, but no participant-facing shape carries it today. A small
> additive DTO change remedies this before any mobile submit code is written.

---

## Context

The participant's live view renders `LiveTeamSpace` inside `src/app/(app)/team-space.tsx`. Post-HU-M1,
an active question renders prompt + options via `ActiveQuestionStage`, which today shows options as
static text with an `accessibilityHint="Answering is not yet available."`. The existing data flow:

```
useSessionTimer ──► activeQuestion (ActiveQuestionSnapshotDto | null)
      │
      ▼
useActiveQuestion ──► view.kind === 'active' → <ActiveQuestionStage question={view.question} ...>
```

`view.question` is typed as `ActiveQuestion` (questionIndex, sequenceOrder, prompt, options,
timeLimitSeconds). The question identity includes `questionIndex` + `sequenceOrder` but **no**
`triviaSubstageSnapshotId` — yet submit requires it (`POST /sessions/{liveSessionId}/participants/answers`
body: `{ teamId, triviaSubstageSnapshotId, questionSequenceOrder, selectedOptionSequenceOrder, token? }`).

The contract doc (`mobile/docs/en-m1-mobile-trivia-contract.md`) documents this gap and recommends a
backend change: add `triviaSubstageSnapshotId` to `QuestionActivatedNotificationDto` and
`ActiveQuestionSnapshotDto`. The value (`session.ActiveSubstageId`) is already in hand at both emit
sites. This plan adopts that recommendation.

---

## Backend contract (verified against session-operations-service source)

### Submit endpoint

```
POST /api/sessions/{liveSessionId}/participants/answers  (Participant policy)
```

Request `SubmitTriviaAnswerRequest` (SessionsController.cs:267):
```
{ teamId: Guid, triviaSubstageSnapshotId: Guid, questionSequenceOrder: int,
  selectedOptionSequenceOrder: int, token?: string }
```

200 returns `SubmitTriviaAnswerResultDto` (acceptance metadata only — **no** isCorrect, scoreValue,
or selected option):

```ts
{ liveSessionId, teamId, triviaSubstageSnapshotId, questionSequenceOrder, answeredAt }
```

### Rejections — RFC 7807 ProblemDetails

The global handler maps each `DomainException` to a ProblemDetails whose `type` is the exception's
kebab-cased `ErrorCode`:

| reasonCode | HTTP | Cause |
|---|---|---|
| `late-trivia-answer` | 409 | Answer arrived after the timer window closed |
| `duplicate-trivia-answer` | 409 | This team already answered the question |
| `trivia-answer-requires-active-question` | 409 | No active question (or declared question is stale) |
| `trivia-answer-requires-active-session` | 409 | Session is not Active |
| `trivia-answer-requires-trivia-substage` | 409 | Active substage is not trivia |
| `invalid-trivia-answer-option` | 400 | Selected option order not valid for the question |
| `answer-submitter-is-not-session-participant` | 403 | Forbidden / not a participant of this session |

The existing `submitTriviaAnswer` helper in `sessions.ts` already handles the ProblemDetails parse
and throws a typed `SubmitTriviaAnswerRejection` with the `reasonCode` slug — no new API client work
needed.

### SignalR — no new push needed

`TeamAnsweredNotificationDto` (method `TeamAnswered`) is broadcast to the **operator-only** group by
design (DES-49). The participant client must not subscribe to it. After a successful submit, the
participant sees the acceptance state locally; correctness/points are withheld until the question
close (HU-35) and downstream scoring (HU-37).

### The `triviaSubstageSnapshotId` gap (open decision resolved)

| Shape | Has `triviaSubstageSnapshotId`? |
|---|---|
| `QuestionActivatedNotificationDto` | **No** — only `(questionIndex, sequenceOrder)` |
| `ActiveQuestionSnapshotDto` | **No** — same keys |
| `SubstageAdvancedNotificationDto.toSubstageId` | Yes, but fires only on transition (misses first substage + late-join) |

**Chosen fix:** add `TriviaSubstageSnapshotId` to both `QuestionActivatedNotificationDto` and
`ActiveQuestionSnapshotDto`. The value is `session.ActiveSubstageId`, already in scope at both emit
sites:

- `TriviaRoundOrchestratorFacade.ActivateQuestionAsync` — the active session's `ActiveSubstageId`
- `SessionTimerSnapshotDtoFactory.Create` — same, via the already-resolved `LiveSession` aggregate

This is a **pure additive** field — no new logic, no behavior change, no migration. It co-travels
with the exact question the participant answers on both the steady-state (broadcast) and reconnect
(REST) paths, so the mobile client reads the id straight off the active question it is already
rendering and echoes it back on submit.

---

## Architecture decisions

- **First-write-wins at the backend.** The backend enforces exactly one answer per team per question
  (`DuplicateTriviaAnswerException`). Mobile reflects this with a **submit interaction lock**: once
  submitted, the option list becomes non-interactive and the submit button disappears. The lock is
  local (useState) — there is no persistent submission state across app restarts (a re-joining
  participant who already submitted can try again, and the backend will reject with
  `duplicate-trivia-answer`).
- **Option selection is local state.** A `selectedOptionSequenceOrder: number | null` in
  `useSubmitAnswer`. No animation delay — tap updates state immediately. Haptics on iOS
  (selection + success/error, following the existing `fireHaptic` pattern).
- **Submit in-flight state is visual, not a blocker.** Show a spinner on the submit button while
  `isSubmitting` is true. The interaction lock (`isLocked`) activates only on successful submit
  (200), not during the request — the button is disabled while in-flight anyway.
- **Rejection is displayed as a dismissible banner.** On a `SubmitTriviaAnswerRejection`, show the
  reason-specific copy in a warning panel below the options. The banner clears when the user
  changes their selection, when a new question activates, or when the session closes — it is
  reaction-scoped, not persisted.
- **No `triviaSubstageSnapshotId` interpolation or caching.** Read it from the `ActiveQuestion`
  already in the view. The backend adds it to both DTOs; mobile types mirror it. No cross-event
  correlation, no stale cache.
- **The submit button renders only when an active question is visible and the team has not yet
  submitted.** During `submit` → lock transitions, the button is replaced by a submitted-state chip.
- **Network-failure copy is first-class.** `'unknown'` (status 0 or unrecognized slug) maps to a
  generic "couldn't reach the server" message, distinct from the seven domain rejections.

---

## Phases

### Phase 0 — Backend: add `triviaSubstageSnapshotId` to active-question DTOs

**Services:** `session-operations-service` only

**Scope**

- `Application/Sessions/Common/Notifications/QuestionActivatedNotificationDto.cs` — add
  `Guid TriviaSubstageSnapshotId` as the **last** parameter (preserve binary compat — pure additive
  positional parameter at end).
- `Application/Dtos/Sessions/SessionTimerSnapshotDto.cs` — add
  `Guid TriviaSubstageSnapshotId` to `ActiveQuestionSnapshotDto` as the last parameter.
- `Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs` — populate
  `TriviaSubstageSnapshotId` in the `QuestionActivatedNotificationDto` constructor call at line
  ~118: `session.ActiveSubstageId.Value`.
- `Application/Sessions/Common/SessionTimerSnapshotDtoFactory.cs` — populate
  `TriviaSubstageSnapshotId` in the `ActiveQuestionSnapshotDto` constructor call at line ~53:
  `liveSession.ActiveSubstageId.Value`.

**What does NOT change:** no new logic, no new exception, no migration, no controller changes, no
SignalR hub changes. The field is already `Guid` on the wire — the backend just wasn't populating
the two participant-facing shapes.

**Gate** — `make -C backend test SVC=session-operations-service` passes. New DTO property is
reflected in serialization (add a focused test: build the DTO, serialize to JSON, assert
`triviaSubstageSnapshotId` present and non-empty). All existing tests pass unmodified (additive
field is backwards-compat).

---

### Phase 1 — Mobile types: add `triviaSubstageSnapshotId`

**Scope**

- `src/lib/realtime/trivia-types.ts`:
  - Add `triviaSubstageSnapshotId: string` to `QuestionActivatedNotificationDto`.
  - Add `triviaSubstageSnapshotId: string` to `ActiveQuestionSnapshotDto`.
- `src/lib/realtime/active-question-types.ts`:
  - Add `triviaSubstageSnapshotId: string` to `ActiveQuestion`.
  - Update `toActiveQuestion` to plumb the new field through from both source types.

**Gate** — TypeScript compiles without errors. Existing unit tests (`active-question-types.test.ts`)
updated to assert the field is carried through `toActiveQuestion` from both
`QuestionActivatedNotificationDto` and `ActiveQuestionSnapshotDto` inputs.

---

### Phase 2 — Rejection reason → participant-facing copy

**Scope**

- New `src/lib/realtime/trivia-answer-rejection-copy.ts`:
  - Export `triviaAnswerRejectionMessage(reasonCode: TriviaAnswerRejectionReasonCode | 'unknown'): string`
    returning concise, participant-appropriate copy for each code.
  - Export `triviaAnswerRejectionTitle(reasonCode: TriviaAnswerRejectionReasonCode | 'unknown'): string`
    for the banner heading.

Copy table:

| reasonCode | Title | Body |
|---|---|---|
| `late-trivia-answer` | Too late | The time window to answer closed before your submission arrived. |
| `duplicate-trivia-answer` | Already answered | Your team has already submitted an answer for this question. |
| `trivia-answer-requires-active-question` | No active question | This question is no longer open. |
| `trivia-answer-requires-active-session` | Session not active | Answering isn't available right now. |
| `trivia-answer-requires-trivia-substage` | Not a trivia round | The current stage doesn't accept answers. |
| `invalid-trivia-answer-option` | Invalid selection | The selected option isn't valid for this question. |
| `answer-submitter-is-not-session-participant` | Not authorized | Your team isn't registered to answer in this session. |
| `unknown` (network) | Connection issue | Couldn't reach the server. Check your connection and try again. |

**Gate** — unit test asserts each of the 7 known codes + the unknown case maps to a non-empty
string.

---

### Phase 3 — `useSubmitAnswer` hook

**Scope**

- New `src/lib/realtime/use-submit-answer.ts`:
  `useSubmitAnswer({ client, liveSessionId, teamId, token })`.
- State: `selectedOptionSequenceOrder: number | null`, `isSubmitting: boolean`, `isLocked: boolean`,
  `rejection: { reasonCode, title, message } | null`.
- Behaviour:
  - `selectOption(sequenceOrder)` — updates selection, clears any existing rejection. Fires haptic
    (`light` impact) on iOS.
  - `submit()` — if no selection or already locked/submitting, no-op. Calls
    `submitTriviaAnswer(liveSessionId, { teamId, triviaSubstageSnapshotId,
    questionSequenceOrder, selectedOptionSequenceOrder, token? })`. On 200: set `isLocked = true`,
    fire success haptic. On `SubmitTriviaAnswerRejection`: set `rejection` with the mapped title +
    message, fire error haptic.
  - `clearRejection()` — sets `rejection = null`.
  - The hook requires `triviaSubstageSnapshotId` and `questionSequenceOrder` — callers pass them
    from the active `ActiveQuestion` already in the view.
  - Reset lock + rejection when a new question activates (consumer coerces via a `questionKey`:
    `${triviaSubstageSnapshotId}:${questionSequenceOrder}`).
- Expose: `{ selectedOptionSequenceOrder, isSubmitting, isLocked, rejection, selectOption, submit,
  clearRejection }`.

**Gate** — unit tests:
- Selecting an option stores it and clears a prior rejection.
- `submit` with no selection is a no-op (doesn't call `submitTriviaAnswer`).
- `submit` sets `isSubmitting` → resolves locked on 200.
- `rejection` populates with the mapped reason from `SubmitTriviaAnswerRejection`.
- `isLocked` prevents a second submit call.
- Changing `questionKey` resets lock + rejection.

---

### Phase 4 — Option selection UI in `active-question-stage.tsx`

**Scope**

- Modify `ActiveQuestionStage` to accept submit props:
  ```ts
  { question: ActiveQuestion; sessionState: string; score: number;
    timerDisplay: TimerDisplay;
    selectedOptionSequenceOrder: number | null;
    isSubmitting: boolean;
    isLocked: boolean;
    rejection: TriviaAnswerRejectionDisplay | null;
    onSelectOption: (sequenceOrder: number) => void;
    onSubmit: () => void;
    onDismissRejection: () => void; }
  ```
- **Option rows** become `Pressable` with:
  - Default: existing visual (letter pill, text). The `accessibilityHint` changes from
    "Answering is not yet available." to "Select to submit as your team's answer."
  - Selected (`selectedOptionSequenceOrder === index`): `emberAccentSoft` background,
    letter pill fill changes to `emberAccent`, border to `emberAccentStrong`.
  - Locked (`isLocked`): all rows lose `onPress` handler, `accessibilityHint` changes to
    "Answer already submitted."
  - `accessibilityRole="radio"`, `accessibilityState={{ selected, disabled: isLocked }}`.
- **Submit button** (between options and the rejection banner):
  - `Button` component, `variant="primary"`, label "Submit answer".
  - Disabled when `selectedOptionSequenceOrder === null || isSubmitting || isLocked`.
  - When `isSubmitting`: show `ActivityIndicator` in place of label (or alongside muted label).
  - When `isLocked` (answer accepted): replace button with a success chip:
    `View` with `signalSuccess` dot + "Answer submitted" label.
- **Rejection banner** (below submit button area, only when `rejection` exists):
  - `Panel` with `signalWarning` semitransparent bg, title + body text, dismiss `Pressable` (X).
  - Dismiss clears via `onDismissRejection`.
  - `accessibilityRole="alert"`.
- **`ActiveQuestion`** type now includes `triviaSubstageSnapshotId` — used as part of the submit
  request, not displayed.
- The `StageHeader` / `ActiveQuestionStageHeader` export is unchanged.

**Gate** — component tests (following `active-question-stage.test.tsx` patterns):
- Rendering an active question with options shows tappable rows.
- Tapping a row sets selection (assert `onSelectOption` called).
- Selected state renders `emberAccentSoft` bg + filled pill.
- `isLocked` disables all option taps and shows the success chip instead of submit button.
- `isSubmitting` shows spinner on the button.
- Rejection banner renders with title + body; dismiss calls `onDismissRejection`.
- `accessibilityRole="radio"` and `accessibilityState` are set correctly.

---

### Phase 5 — Wiring into `team-space.tsx`

**Scope**

- In `LiveTeamSpace` (`team-space.tsx:228`):
  - Import `useSubmitAnswer` and the rejection copy utilities.
  - Call `useSubmitAnswer` with `{ client, liveSessionId: result.liveSessionId, teamId:
    referenceTeamId, token }` and the current question's `triviaSubstageSnapshotId` +
    `questionSequenceOrder`.
  - When `view.kind === 'active'`, extract `triviaSubstageSnapshotId` and `questionSequenceOrder`
    from `view.question` and pass them + the submit hook state to `ActiveQuestionStage`.
  - In the non-active views (`waiting`, `none`, `closed`), no submit hook is rendered — it only
    lives while a question is active.
  - The `questionKey` used for reset is `${view.question.triviaSubstageSnapshotId}:
    ${view.question.questionSequenceOrder}`.

**Gate** — wire test (extend `team-space-question-stage.test.tsx`):
- An active question renders the selectable option UI and a "Submit answer" button.
- Tapping an option + submit calls `submitTriviaAnswer` with the correct request shape.
- A successful submit locks the options and shows the success chip.

---

### Phase 6 — Tests

**Unit tests:**
- `active-question-types.test.ts`: `toActiveQuestion` carries `triviaSubstageSnapshotId` from both
  source DTOs (updated existing test).
- `trivia-answer-rejection-copy.test.ts`: each of 8 codes (7 known + unknown) maps to non-empty
  title + body strings.
- `use-submit-answer.test.ts`: selection state, no-op submit without selection, lock on success,
  rejection mapping on failure, questionKey change resets lock + rejection.
- `sessions-api.test.ts`: existing `submitTriviaAnswer` tests (already present?) — verify 200 path
  and each rejection slug.

**Component tests:**
- `active-question-stage.test.tsx`: update existing tests to pass the new submit props; add tests
  for selection visual, lock visual, submitting visual, rejection banner, dismiss.
- `team-space-question-stage.test.tsx`: existing wire test covers the data flow from hooks through
  `ActiveQuestionStage`; update to include submit hook props.

**Integration test (manual / E2E):**
- Join a live session with an active trivia question. Select an option, submit — see acceptance.
- Attempt a second submit — see "Already answered" rejection.
- Let the timer expire, attempt to submit — see "Too late" rejection.
- Disconnect network, attempt submit — see "Connection issue".

---

## Acceptance criteria

- During an active trivia question, the participant sees tappable option rows with a letter pill
  and a "Submit answer" button.
- Tapping an option highlights it (ember accent bg + filled pill). Tapping a different option
  changes the selection. Selection clears any visible rejection.
- Tapping "Submit answer" sends a `POST /sessions/{liveSessionId}/participants/answers` with the
  correct `teamId`, `triviaSubstageSnapshotId`, `questionSequenceOrder`, and
  `selectedOptionSequenceOrder`.
- On success (200), the options lock, the submit button is replaced by a "Submitted" chip, and the
  participant cannot double-submit.
- On rejection, a dismissible banner shows the reason-specific title + message. The severity
  matches the error (warning style for recoverable, but the participant can re-select and retry).
- Network failure shows the "Connection issue" copy.
- The submit button is disabled when no option is selected or a submission is in-flight.
- The `triviaSubstageSnapshotId` reaches the mobile client via both SignalR
  `QuestionActivated` and the REST timer snapshot without any client-side correlation or caching.
- All seven rejection codes + the network case have distinct, participant-appropriate messages.
- When a new question activates, the submit state resets (selection clears, lock releases,
  rejection dismissed).
- Visuals follow `mobile/DESIGN.md`: warm surfaces, ember accent on selection and primary button,
  tonal layering, tabular live data, no nested cards, no dark mode.

---

## Out of scope

- Correctness feedback or score display after answering (gated behind HU-35 question close
  + HU-37 scoring).
- Viewing other teams' answer status (operator-only `TeamAnswered`).
- Bulk answer submission or multi-select.
- Answer editing after submission (first-write-wins).
- Persisting submit state across app restarts.
- `triviaSubstageSnapshotId` for `SubstageAdvancedNotificationDto` (not needed for submit; the
  `ActiveQuestion` variant covers both steady-state and reconnect paths).

---

## Dependencies

| Upstream | What mobile needs | Status |
|----------|-------------------|--------|
| Phase 0 (this plan) | `triviaSubstageSnapshotId` in `QuestionActivatedNotificationDto` + `ActiveQuestionSnapshotDto` | Blocking — must ship before Phase 3+ |
| HU-34 (DES-46) | `POST …/participants/answers` endpoint + rejection exceptions | In production; contract frozen |
| HU-M1 (DES-82) | `ActiveQuestionStage`, `useActiveQuestion`, `team-space.tsx` wiring | Shipped |
| HU-22 | `SessionTimerUpdated`, timer snapshot, `live-session:{id}` group membership | Shipped |
