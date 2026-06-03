import { HttpError, TimeoutError } from '@microsoft/signalr';
import type {
  ReconnectContext,
  ReconnectParticipantResultDto,
} from './sessions-hub-types';

export type ReconnectOutcome =
  | { kind: 'reconnected'; result: ReconnectParticipantResultDto }
  | { kind: 'forbidden-late-join' }
  | { kind: 'invalid-session-state' }
  | { kind: 'lost-access' }
  | { kind: 'already-connected' }
  | { kind: 'wrong-team' }
  | { kind: 'unauthorized' }
  | { kind: 'network-error' }
  | { kind: 'error' };

const NETWORK_ERROR_NAMES = new Set([
  'AbortError',
  'TimeoutError',
  'TypeError',
  'HttpRequestError',
]);

// The backend's DomainExceptionHubFilter rejects via a HubException whose message
// carries a stable, machine-readable code: `{"code":"...","message":"..."}`. In
// production (EnableDetailedErrors off) the client receives that JSON verbatim; in
// development SignalR prepends "An unexpected error occurred invoking '...'.
// HubException: ", so we extract the code from wherever it sits rather than parsing
// the whole message. We key on the code, never the (environment-dependent) prose.
const HUB_ERROR_CODE = /"code"\s*:\s*"([A-Z_]+)"/;

// Maps each backend code to a UI outcome. Codes the reconnect path can raise plus the
// shared hub codes; anything unmapped falls through to the safe generic `error`.
const CODE_TO_OUTCOME: Record<string, ReconnectOutcome> = {
  LATE_JOIN_NOT_ALLOWED: { kind: 'forbidden-late-join' },
  TEAM_UNAVAILABLE: { kind: 'invalid-session-state' },
  FORBIDDEN: { kind: 'lost-access' },
  PARTICIPANT_REMOVED: { kind: 'lost-access' },
  NOT_FOUND: { kind: 'lost-access' },
  ALREADY_CONNECTED: { kind: 'already-connected' },
  WRONG_TEAM: { kind: 'wrong-team' },
  UNAUTHORIZED: { kind: 'unauthorized' },
  VALIDATION_FAILED: { kind: 'error' },
  ERROR: { kind: 'error' },
};

function normalizeMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message.toLowerCase();
  }

  return String(error).toLowerCase();
}

function hubErrorCode(error: unknown): string | null {
  const raw = error instanceof Error ? error.message : String(error);
  return HUB_ERROR_CODE.exec(raw)?.[1] ?? null;
}

function isNetworkError(error: unknown, message: string): boolean {
  if (error instanceof HttpError) {
    return error.statusCode >= 500;
  }

  if (error instanceof TimeoutError) {
    return true;
  }

  if (error instanceof Error && NETWORK_ERROR_NAMES.has(error.name)) {
    return true;
  }

  return (
    message.includes('failed to fetch') ||
    message.includes('network request failed') ||
    message.includes('failed to complete negotiation') ||
    message.includes('websocket failed to connect') ||
    message.includes('the connection was stopped') ||
    message.includes('a timeout occurred')
  );
}

function isUnauthorized(error: unknown, message: string): boolean {
  return (
    (error instanceof HttpError && error.statusCode === 401) ||
    message.includes("status code '401'") ||
    message.includes('unauthorized')
  );
}

export function toReconnectedOutcome(
  result: ReconnectParticipantResultDto,
): ReconnectOutcome {
  return { kind: 'reconnected', result };
}

export function toUpdatedReconnectContext(
  context: ReconnectContext,
  result: ReconnectParticipantResultDto,
): ReconnectContext {
  return {
    ...context,
    liveSessionId: result.liveSessionId,
    teamId: result.teamId,
    displayName: result.participantDisplayName,
    lastSeenAt: result.lastSeenAt,
  };
}

export function interpretHubError(error: unknown): ReconnectOutcome {
  const message = normalizeMessage(error);

  // Transport-level failures (negotiate/connection) arrive as HttpError/TimeoutError
  // before the hub invoke, so they carry no domain code — classify them first.
  if (isUnauthorized(error, message)) {
    return { kind: 'unauthorized' };
  }

  if (isNetworkError(error, message)) {
    return { kind: 'network-error' };
  }

  // Domain/validation rejections from the hub: key on the stable code.
  const code = hubErrorCode(error);
  if (code) {
    return CODE_TO_OUTCOME[code] ?? { kind: 'error' };
  }

  return { kind: 'error' };
}
