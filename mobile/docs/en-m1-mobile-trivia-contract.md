# EN-M1 — Mobile trivia runtime contract (DES-81)

Freezes the participant-facing realtime + REST contract for `team-space` so `HU-M1` (display),
`HU-M3` (closed state) and `HU-M2` (submit) build against verified `session-operations-service`
shapes instead of guessed DTOs. Every shape below was transcribed from backend source on 2026-07-10;
nothing here is invented. Types live in `mobile/src/lib/realtime/trivia-types.ts` (plus a drift fix in
`timer-types.ts`); the hub handlers in `sessions-hub.ts`; the submit helper in `src/lib/api/sessions.ts`.

Backend paths are relative to `backend/services/session-operations-service/src/`.

## Serialization conventions

- .NET records serialize to **camelCase** JSON (matches every existing mobile DTO, e.g. `liveSessionId`).
- `Guid` → `string`; `DateTimeOffset` → ISO-8601 `string`; `IReadOnlyList<string>` → `readonly string[]`.
- Broadcasts are pushed to the participant-visible group `live-session:{liveSessionId:D}`.

## Display contract (verified)

| Mobile type | Transport | Method / route | Backend source |
|---|---|---|---|
| `QuestionActivatedNotificationDto` | SignalR push | `QuestionActivated` | `Application/Sessions/Common/Notifications/QuestionActivatedNotificationDto.cs`; broadcast `Api/Hubs/SignalRSessionQuestionBroadcaster.cs:24-31` |
| `QuestionClosedNotificationDto` | SignalR push | `QuestionClosed` | `.../Notifications/QuestionClosedNotificationDto.cs`; broadcast `SignalRSessionQuestionBroadcaster.cs:33-40` |
| `SubstageAdvancedNotificationDto` | SignalR push | `SubstageAdvanced` | `.../Notifications/SubstageAdvancedNotificationDto.cs`; broadcast `SignalRSessionQuestionBroadcaster.cs:42-49` |
| `ActiveQuestionSnapshotDto` | REST | `GET /api/sessions/{liveSessionId}/participants/timer?teamId=&token=` | `Application/Dtos/Sessions/SessionTimerSnapshotDto.cs` (nested record) |
| `SessionTimerUpdatedNotificationDto` | SignalR push | `SessionTimerUpdated` | already typed in `timer-types.ts` — **no drift** |
| `SessionStateChangedNotificationDto` | SignalR push | `SessionStateChanged` | already typed in `sessions-hub-types.ts` — **no drift** |

**Steady-state vs reconnect.** `QuestionActivated` pushes `prompt` + `options` on activation, so the
question renders with no REST round-trip. The REST timer snapshot's `activeQuestion` is the
**reconnect / late-join** path — it adds `remainingSeconds` and is `null` when no question is active.
(The original spike's "no `QuestionOpened` broadcast, prompt/options only reachable via REST" gap does
**not** exist in the current code.)

Field lists (all camelCase on the wire):

- `QuestionActivatedNotificationDto`: `liveSessionId`, `questionIndex`, `sequenceOrder`, `prompt`,
  `options`, `timeLimitSeconds`, `activatedAt`.
- `QuestionClosedNotificationDto`: `liveSessionId`, `questionIndex`, `closedAt`, `wasExpiredByTimer`.
- `SubstageAdvancedNotificationDto`: `liveSessionId`, `fromSubstageId`, `fromPlayMode`, `toSubstageId`
  (nullable — `null` when the final substage completed and the session finished), `advancedAt`.
- `ActiveQuestionSnapshotDto`: `liveSessionId`, `questionIndex`, `sequenceOrder`, `prompt`, `options`,
  `timeLimitSeconds`, `remainingSeconds`, `activatedAt`.

## Submit contract (verified — HU-34 / DES-46, consumed by HU-M2)

- **Endpoint** `POST /api/sessions/{liveSessionId}/participants/answers`, `Participant` policy —
  `Api/Controllers/SessionsController.cs:162-180`.
- **Request** `SubmitTriviaAnswerRequest` — `SessionsController.cs:247-252`:
  `teamId`, `triviaSubstageSnapshotId`, `questionSequenceOrder`, `selectedOptionSequenceOrder`, `token?`.
- **200 response** `SubmitTriviaAnswerResultDto` — `Application/Dtos/Sessions/SubmitTriviaAnswerResultDto.cs`:
  `liveSessionId`, `teamId`, `triviaSubstageSnapshotId`, `questionSequenceOrder`, `answeredAt`.
  **Acceptance metadata only** — no `isCorrect`, no `scoreValue`, no selected option. Correctness and
  points are withheld from every realtime surface by design (fairness) and surface only after close
  (HU-35) / downstream scoring (HU-37).

### Rejections — RFC 7807 ProblemDetails, `type` = the stable reason slug

The global handler (`Api/Services/ProblemDetailsExceptionHandler.cs`) maps each `DomainException` to a
`ProblemDetails` whose **`type`** is the exception's `ErrorCode` — kebab-cased from the type name by
`DomainException.DeriveErrorCode` (`Domain/Exceptions/DomainException.cs`). All seven verified 1:1
against the exceptions in `Domain/Exceptions/`. HTTP status derives from each exception's `Category`.

| `reasonCode` (ProblemDetails `type`) | HTTP | Backend exception (`Domain/Exceptions/…`) |
|---|---|---|
| `late-trivia-answer` | 409 | `LateTriviaAnswerException` (Conflict) |
| `duplicate-trivia-answer` | 409 | `DuplicateTriviaAnswerException` (Conflict) |
| `trivia-answer-requires-active-question` | 409 | `TriviaAnswerRequiresActiveQuestionException` (Conflict) |
| `trivia-answer-requires-active-session` | 409 | `TriviaAnswerRequiresActiveSessionException` (Conflict) |
| `trivia-answer-requires-trivia-substage` | 409 | `TriviaAnswerRequiresTriviaSubstageException` (Conflict) |
| `invalid-trivia-answer-option` | 400 | `InvalidTriviaAnswerOptionException` (Validation) |
| `answer-submitter-is-not-session-participant` | 403 | `AnswerSubmitterIsNotSessionParticipantException` (Forbidden) |

Note: a stale answer against an already-advanced/closed question is folded into
`trivia-answer-requires-active-question` on purpose (`ActiveTriviaQuestionLink.cs:41-46`).

### ProblemDetails vs the shared `apiClient` (drift)

The shared `apiClient` (`src/lib/api/client.ts`) reads `body.code` / `body.message`, but RFC 7807
carries `type` / `title` / `detail` — so it cannot surface the reason slug. `submitTriviaAnswer` in
`sessions.ts` therefore does its own `fetch`, reads `type`, and narrows it to the typed
`TriviaAnswerRejectionReasonCode` union (falling back to `'unknown'` for a network failure or any
unrecognized slug), throwing a typed `SubmitTriviaAnswerRejection`. HU-M2 may later choose to unify
this into `apiClient`; kept local here to avoid changing shared error behavior in a spike.

## `TeamAnswered` is operator-only — participant client does NOT consume it

`TeamAnsweredNotificationDto` (method `TeamAnswered`) is broadcast to the **operator-only** group, not
the participant `live-session:{id}` group, and deliberately carries no correctness / score / option.
It belongs to the web operator monitor (DES-49). The mobile participant client must **not** wire to
it — no handler is exposed for it here, by design, so HU-M2 cannot subscribe to it by mistake.

## Drift found and fixed

`timer-types.ts`'s `SessionTimerSnapshotDto` was missing the `activeQuestion` field the backend nests
in it (`SessionTimerSnapshotDto.cs:15`, added with HU-33A). Added as
`activeQuestion?: ActiveQuestionSnapshotDto | null`. `SessionTimerUpdatedNotificationDto` and
`SessionStateChangedNotificationDto` match backend exactly — no drift.

## Open decision — sourcing `TriviaSubstageSnapshotId` for submit

Submit **requires** `triviaSubstageSnapshotId`, and the backend enforces that it equals the session's
active substage snapshot id (`ActiveTriviaQuestionLink.cs:38`; the accepted result echoes it from
`submission.ActiveSubstageId`, `SubmitTriviaAnswerCommandHandler.cs:74`). **No participant-facing
surface currently carries that id:**

- `QuestionActivatedNotificationDto` and `ActiveQuestionSnapshotDto` key the question by
  `(questionIndex, sequenceOrder)` only — **no substage id**.
- `SubstageAdvancedNotificationDto` *does* carry `toSubstageId`, but it fires **only on a
  substage-to-substage transition**. The first substage is entered by a direct pointer set
  (`LiveSession.cs:556`, `ActiveSubstageId ??= …`) that raises **no** `SubstageAdvancedEvent`, so a
  participant playing the first substage never receives the id — and a late-joiner / reconnecting
  participant misses any advance that fired before they connected. Insufficient as a sole source.
- `MissionRuntimeDto` (`MissionRuntimeSubstageDto`) exposes only `title` / `sequenceOrder` / `playMode`
  — **no snapshot Guid** — and is operator-facing (returned by `CreateSessionCommandHandler`).
- Lobby / select-team / reconnect participant DTOs carry no substage id.

### Chosen strategy (recommended): add `triviaSubstageSnapshotId` to the active-question shapes

The spike recommends a small downstream **backend** ticket adding `triviaSubstageSnapshotId` to both
`QuestionActivatedNotificationDto` and `ActiveQuestionSnapshotDto`. The value is already in hand at
both emit sites (`session.ActiveSubstageId` in `TriviaRoundOrchestratorFacade.ActivateQuestionAsync`
and in `SessionTimerSnapshotDtoFactory`), so it is a pure additive field, not new behavior.

**Why this over the alternatives:**

- It co-travels with the exact question the participant answers, on **both** the steady-state
  (broadcast) and reconnect (REST) paths — the only option that covers first-substage entry, mid-round
  join, and reconnect uniformly.
- It keeps the mobile client stateless about substage identity: read the id straight off the active
  question it is already rendering, echo it back on submit. No cross-event correlation, no cache that
  can go stale across an advance.

**Trade-off:** it needs a backend change before HU-M2 can submit — mobile cannot unblock submit purely
client-side today. That change is deferred to HU-M2's window (this is a display-first spike; submit is
gated behind HU-M2 anyway), and it is additive, so it does not disturb any frozen shape here.

### Interim fallback (documented, not adopted)

If HU-M2 must ship before that backend field lands, the client can capture `toSubstageId` from
`SubstageAdvanced` **when the advanced-into substage is trivia** and reuse it as
`triviaSubstageSnapshotId` for that substage's questions. This is why `onSubstageAdvanced` is exposed on
`SessionsHubClient` and `SubstageAdvancedNotificationDto` is typed. It is **not** adopted as the primary
strategy because it silently fails for the first substage and for anyone who connects after the advance
fired, forcing extra "no substage id yet → cannot submit" empty states. Recommended only as a stopgap.

## Assumptions

- ProblemDetails `type` is a **bare slug** (e.g. `late-trivia-answer`), not a URI —
  `ProblemDetailsExceptionHandler.Problem` sets `Type` directly to the `ErrorCode`. If a future
  `ProblemDetailsOptions` prepends a base URI, `toRejectionReasonCode` must strip it first.
- The `/api` route prefix is the gateway's, mirrored from the existing timer-snapshot helper.
