import { apiClient, ApiError, authorizedFetch } from './client';
import type { SessionTimerSnapshotDto } from '@/lib/realtime/timer-types';
import type { ParticipantTeamBoardDto } from '@/lib/realtime/team-board-types';
import type { RankingSnapshotDto } from '@/lib/realtime/ranking-types';
import type {
  SubmitTriviaAnswerRequest,
  SubmitTriviaAnswerResultDto,
  TriviaAnswerRejectionReasonCode,
  TriviaTeamQuestionResultDto,
} from '@/lib/realtime/trivia-types';
import type {
  RegisterTargetScanRequest,
  RegisterTargetScanResultDto,
  TargetScanRejectionReasonCode,
} from '@/lib/realtime/target-scan-types';

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

// HU-23 participant team-board snapshot. Mirrors getParticipantTimerSnapshot exactly (same
// { teamId } query + optional token, same no-store/no-cache), returning ParticipantTeamBoardDto.
// A teamId/token mismatch for another team is a 403 (mapped via interpretTimerSnapshotError).
export function getParticipantTeamBoard(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
): Promise<ParticipantTeamBoardDto> {
  const params = new URLSearchParams({ teamId });
  if (token) {
    params.set('token', token);
  }
  return apiClient.get<ParticipantTeamBoardDto>(
    `/api/sessions/${encodeURIComponent(liveSessionId)}/participants/team-board?${params.toString()}`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}

// HU-25B session ranking snapshot. Mirrors getParticipantTeamBoard (same { teamId } query + optional
// token, same no-store/no-cache), returning RankingSnapshotDto.
export function getRanking(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
): Promise<RankingSnapshotDto> {
  const params = new URLSearchParams({ teamId });
  if (token) {
    params.set('token', token);
  }
  return apiClient.get<RankingSnapshotDto>(
    `/api/sessions/${encodeURIComponent(liveSessionId)}/ranking?${params.toString()}`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}

// HU-M4 participant "my team result" read. Mirrors the existing GET reads (apiClient + bearer + no-cache),
// returning TriviaTeamQuestionResultDto for a closed question keyed by its sequenceOrder.
// Source: Api/Controllers/SessionsController.cs line 344 — GET …/trivia/questions/{sequenceOrder}/my-result
export function getTriviaTeamQuestionResult(
  liveSessionId: string,
  sequenceOrder: number,
): Promise<TriviaTeamQuestionResultDto> {
  return apiClient.get<TriviaTeamQuestionResultDto>(
    `/api/sessions/${encodeURIComponent(liveSessionId)}/trivia/questions/${sequenceOrder}/my-result`,
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
// helper maps the response itself to surface the `type` slug as a typed `reasonCode`, going through
// `authorizedFetch` for the shared bearer/401-replay handling. See the contract note.

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
  let response: Response;
  try {
    response = await authorizedFetch(
      `/api/sessions/${encodeURIComponent(liveSessionId)}/participants/answers`,
      { method: 'POST', body: JSON.stringify(request) },
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

// ── Register target scan (HU-31 contract, consumed by #223) ─────────────────────────────────────────
// The mobile QR scanner submits a captured payload here. A correct scan resolves the target and returns
// 200 acceptance metadata (never the score). A wrong/duplicate/out-of-context scan is retained-rejected
// as RFC 7807 (422, `type: "target-scan-rejected"`) with the consistent reason in `detail`; pre-intake
// blocks (non-admitting session, denied participant, etc.) surface as ProblemDetails on other statuses.
// Like `submitTriviaAnswer`, this maps the response itself rather than going through `apiClient` so the
// ProblemDetails `detail`/status — which `apiClient` does not read — reach the UI; the shared
// bearer/401-replay handling still comes from `authorizedFetch`.

// Typed rejection for a non-2xx scan. `reasonCode` is derived from the HTTP status (see the union);
// `detail` carries the backend's reason string, which for a retained rejection (422) is the exact
// message the participant should see. `status` is 0 for a network failure.
export class RegisterTargetScanRejection extends Error {
  constructor(
    public readonly reasonCode: TargetScanRejectionReasonCode,
    public readonly status: number,
    public readonly detail: string,
  ) {
    super(detail);
    this.name = 'RegisterTargetScanRejection';
  }
}

// Maps a failed scan to a stable reason code, keyed off the HTTP status — except at 409, where two
// unrelated conflicts share the status: a session that is not admitting scans, and a lost write race
// the participant should simply retry. Only the ProblemDetails `type` tells them apart, so the 409 arm
// reads it. The three retained-rejection reasons (unknown QR, out-of-context target, duplicate) all
// share status 422 and are distinguished only by the backend's `detail` message, so 422 collapses to a
// single 'retained-rejection' code that surfaces that message verbatim.
function toScanRejectionReasonCode(
  status: number,
  type?: unknown,
): TargetScanRejectionReasonCode {
  switch (status) {
    case 0:
      return 'network';
    case 400:
      return 'invalid-scan';
    case 401:
      return 'unauthorized';
    case 403:
      return 'not-a-participant';
    case 404:
      return 'session-not-found';
    case 409:
      return type === 'concurrent-modification'
        ? 'concurrent-modification'
        : 'session-not-accepting';
    case 422:
      return 'retained-rejection';
    default:
      return 'unknown';
  }
}

// POST /api/sessions/{liveSessionId}/participants/target-scans (Participant policy). Resolves with the
// acceptance metadata on 200; rejects with a `RegisterTargetScanRejection` carrying the typed reasonCode
// and the backend reason on any failure.
export async function registerTargetScan(
  liveSessionId: string,
  request: RegisterTargetScanRequest,
): Promise<RegisterTargetScanResultDto> {
  let response: Response;
  try {
    response = await authorizedFetch(
      `/api/sessions/${encodeURIComponent(liveSessionId)}/participants/target-scans`,
      { method: 'POST', body: JSON.stringify(request) },
    );
  } catch {
    throw new RegisterTargetScanRejection('network', 0, 'Network request failed');
  }

  if (!response.ok) {
    let type: unknown;
    let detail = `HTTP ${response.status}`;
    try {
      const problem = (await response.json()) as { type?: string; detail?: string };
      type = problem.type;
      detail = problem.detail ?? detail;
    } catch {
      // Non-JSON error body: keep the status-derived fallback detail. A 409 with no readable `type`
      // stays 'session-not-accepting', the pre-existing reading of a bare 409.
    }
    throw new RegisterTargetScanRejection(
      toScanRejectionReasonCode(response.status, type),
      response.status,
      detail,
    );
  }

  return response.json() as Promise<RegisterTargetScanResultDto>;
}
