import { fetch } from 'expo/fetch';
import { getAccessToken } from '@/lib/auth/token-store';
import { apiBaseUrl } from '@/lib/host';
import { apiClient, ApiError } from './client';
import type { SessionTimerSnapshotDto } from '@/lib/realtime/timer-types';
import type {
  SubmitTriviaAnswerRequest,
  SubmitTriviaAnswerResultDto,
  TriviaAnswerRejectionReasonCode,
} from '@/lib/realtime/trivia-types';

export type TimerSnapshotError =
  | 'network-error'
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'timer-unavailable'
  | 'error';

export function interpretTimerSnapshotError(error: unknown): TimerSnapshotError {
  if (error instanceof ApiError) {
    if (error.status === 0) return 'network-error';
    if (error.status === 401) return 'unauthorized';
    if (error.status === 403) return 'forbidden';
    if (error.status === 404) return 'not-found';
    if (error.status === 409) return 'timer-unavailable';
    return 'error';
  }
  return 'error';
}

export function getParticipantTimerSnapshot(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
): Promise<SessionTimerSnapshotDto> {
  const params = new URLSearchParams({ teamId });
  if (token) {
    params.set('token', token);
  }
  return apiClient.get<SessionTimerSnapshotDto>(
    `/api/sessions/${encodeURIComponent(liveSessionId)}/participants/timer?${params.toString()}`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}

// ── Submit trivia answer (HU-34 / DES-46 contract, consumed by HU-M2) ──────────────────────────────
// The backend rejects a submit with RFC 7807 ProblemDetails whose `type` is the stable rejection slug.
// The shared `apiClient` only reads `code`/`message`, which ProblemDetails does NOT carry, so this
// helper fetches directly to surface the `type` slug as a typed `reasonCode`. See the contract note.

// Runtime guard mirroring the TriviaAnswerRejectionReasonCode union: narrows an arbitrary `type`
// string from the wire to a known slug, falling back to 'unknown' for anything unrecognized.
const TRIVIA_ANSWER_REJECTION_REASON_CODES: readonly TriviaAnswerRejectionReasonCode[] = [
  'late-trivia-answer',
  'duplicate-trivia-answer',
  'trivia-answer-requires-active-question',
  'trivia-answer-requires-active-session',
  'trivia-answer-requires-trivia-substage',
  'invalid-trivia-answer-option',
  'answer-submitter-is-not-session-participant',
];

function toRejectionReasonCode(type: unknown): TriviaAnswerRejectionReasonCode | 'unknown' {
  return TRIVIA_ANSWER_REJECTION_REASON_CODES.includes(type as TriviaAnswerRejectionReasonCode)
    ? (type as TriviaAnswerRejectionReasonCode)
    : 'unknown';
}

// Typed rejection for a non-2xx submit. `reasonCode` is the domain slug the UI maps to a message;
// `status` is the HTTP status (0 for a network failure). 'unknown' covers a network failure or any
// ProblemDetails `type` outside the known trivia set.
export class SubmitTriviaAnswerRejection extends Error {
  constructor(
    public readonly reasonCode: TriviaAnswerRejectionReasonCode | 'unknown',
    public readonly status: number,
    public readonly detail: string,
  ) {
    super(detail);
    this.name = 'SubmitTriviaAnswerRejection';
  }
}

// POST /api/sessions/{liveSessionId}/participants/answers (Participant policy). Resolves with the
// acceptance metadata on 200; rejects with a `SubmitTriviaAnswerRejection` carrying the typed
// reasonCode on any failure.
export async function submitTriviaAnswer(
  liveSessionId: string,
  request: SubmitTriviaAnswerRequest,
): Promise<SubmitTriviaAnswerResultDto> {
  const token = await getAccessToken();
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(
      `${apiBaseUrl()}/api/sessions/${encodeURIComponent(liveSessionId)}/participants/answers`,
      { method: 'POST', headers, body: JSON.stringify(request) },
    );
  } catch {
    throw new SubmitTriviaAnswerRejection('unknown', 0, 'Network request failed');
  }

  if (!response.ok) {
    let type: unknown;
    let detail = `HTTP ${response.status}`;
    try {
      const problem = (await response.json()) as { type?: string; detail?: string };
      type = problem.type;
      detail = problem.detail ?? detail;
    } catch {
      // Non-JSON error body: fall back to reason 'unknown' keyed off the status.
    }
    throw new SubmitTriviaAnswerRejection(toRejectionReasonCode(type), response.status, detail);
  }

  return response.json() as Promise<SubmitTriviaAnswerResultDto>;
}
