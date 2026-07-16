// Participant-facing trivia runtime contract (EN-M1 / DES-81). Every shape here mirrors a verified
// `session-operations-service` DTO, broadcast, or endpoint; see `mobile/docs/en-m1-mobile-trivia-contract.md`
// for the field-by-field source map. Guids are serialized as strings and DateTimeOffset as ISO-8601.

// SignalR `QuestionActivated` push to the participant `live-session:{id}` group. Steady-state path:
// carries prompt + options so the question renders without a REST round-trip.
// Source: Application/Sessions/Common/Notifications/QuestionActivatedNotificationDto.cs
export type QuestionActivatedNotificationDto = {
  liveSessionId: string;
  questionIndex: number;
  sequenceOrder: number;
  prompt: string;
  options: readonly string[];
  timeLimitSeconds: number;
  activatedAt: string;
  triviaSubstageSnapshotId: string;
};

// SignalR `QuestionClosed` push to the participant group. `wasExpiredByTimer` distinguishes a
// timer-expiry close from an operator/exhaustion close.
// Source: Application/Sessions/Common/Notifications/QuestionClosedNotificationDto.cs
export type QuestionClosedNotificationDto = {
  liveSessionId: string;
  questionIndex: number;
  closedAt: string;
  wasExpiredByTimer: boolean;
  correctOptionSequenceOrder: number; // NEW — DES-101 §1 (HU-35)
  explanation: string | null;          // NEW — DES-101 §1 (HU-35)
};

// SignalR `SubstageAdvanced` push to the participant group. Fires only on a substage-to-substage
// transition (never on first-substage entry — that pointer is set directly by the domain), so it is
// NOT a reliable source for `triviaSubstageSnapshotId`; see the contract note. `toSubstageId` is null
// when the final substage completed and the session finished. `fromPlayMode` is a SubstagePlayMode name.
// Source: Application/Sessions/Common/Notifications/SubstageAdvancedNotificationDto.cs
export type SubstageAdvancedNotificationDto = {
  liveSessionId: string;
  fromSubstageId: string;
  fromPlayMode: string;
  toSubstageId: string | null;
  advancedAt: string;
};

// Nested in `SessionTimerSnapshotDto` (see timer-types.ts). Reconnect / late-join path: the same
// active-question fields as the broadcast plus `remainingSeconds`. Null when no question is active.
// Source: Application/Dtos/Sessions/SessionTimerSnapshotDto.cs (record ActiveQuestionSnapshotDto)
export type ActiveQuestionSnapshotDto = {
  liveSessionId: string;
  questionIndex: number;
  sequenceOrder: number;
  prompt: string;
  options: readonly string[];
  timeLimitSeconds: number;
  remainingSeconds: number;
  activatedAt: string;
  triviaSubstageSnapshotId: string;
};

// Request body for `POST /sessions/{liveSessionId}/participants/answers` (Participant policy). The
// snapshotted question is keyed by (triviaSubstageSnapshotId + questionSequenceOrder); the option by
// its sequence order — the frozen snapshot carries no per-item Guids. `token` is the optional
// runtime-participation credential re-checked by the backend guard.
// Source: Api/Controllers/SessionsController.cs (record SubmitTriviaAnswerRequest, line 247)
export type SubmitTriviaAnswerRequest = {
  teamId: string;
  triviaSubstageSnapshotId: string;
  questionSequenceOrder: number;
  selectedOptionSequenceOrder: number;
  token?: string | null;
};

// 200 response. Acceptance metadata ONLY — deliberately omits isCorrect, scoreValue, and the selected
// option so correctness/points never leak to the participant before reveal (HU-35) / scoring (HU-37).
// Source: Application/Dtos/Sessions/SubmitTriviaAnswerResultDto.cs
export type SubmitTriviaAnswerResultDto = {
  liveSessionId: string;
  teamId: string;
  triviaSubstageSnapshotId: string;
  questionSequenceOrder: number;
  answeredAt: string;
};

// GET /api/sessions/{liveSessionId}/trivia/questions/{sequenceOrder}/my-result (Participant).
// Reveals the team's own result for a closed question — correctness, points, and the correct answer.
// Source: Application/Dtos/Sessions/TriviaTeamQuestionResultDto.cs
export type TriviaTeamQuestionResultDto = {
  selectedOptionSequenceOrder: number | null; // null if the team never answered
  isCorrect: boolean | null;
  scoreValue: number | null;
  correctOptionSequenceOrder: number;
  explanation: string | null;
};

// Stable RFC 7807 `type` slugs a rejected submit can carry (ProblemDetails.type == DomainException
// ErrorCode, kebab-cased from the exception name). Verified 1:1 against the seven trivia-answer
// exceptions in Domain/Exceptions. HTTP status per slug is noted in the contract note.
export type TriviaAnswerRejectionReasonCode =
  | 'late-trivia-answer' // 409 — answer arrived after the timer window closed
  | 'duplicate-trivia-answer' // 409 — this team already answered the question
  | 'trivia-answer-requires-active-question' // 409 — no active (or stale) question
  | 'trivia-answer-requires-active-session' // 409 — session is not Active
  | 'trivia-answer-requires-trivia-substage' // 409 — active substage is not trivia
  | 'invalid-trivia-answer-option' // 400 — selected option order not valid for the question
  | 'answer-submitter-is-not-session-participant'; // 403 — forbidden / not a participant of this session
