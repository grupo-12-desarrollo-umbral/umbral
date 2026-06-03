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
  | { kind: 'unauthorized' }
  | { kind: 'network-error' }
  | { kind: 'error' };

const NETWORK_ERROR_NAMES = new Set([
  'AbortError',
  'TimeoutError',
  'TypeError',
  'HttpRequestError',
]);

function normalizeMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message.toLowerCase();
  }

  return String(error).toLowerCase();
}

function isFinishedOrCancelledState(message: string): boolean {
  return (
    message.includes("session is 'finished'") ||
    message.includes("session is 'cancelled'")
  );
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

function isLostAccess(message: string): boolean {
  return (
    message.includes('participant') &&
    message.includes('was removed from the live session')
  ) ||
    message.includes('cannot reconnect to') ||
    message.includes('resource not found') ||
    message.includes('entity "livesession"') ||
    message.includes('forbiddenaccessexception');
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

  if (isUnauthorized(error, message)) {
    return { kind: 'unauthorized' };
  }

  if (isNetworkError(error, message)) {
    return { kind: 'network-error' };
  }

  if (
    message.includes("new participant joins are not allowed while the session is 'active'") ||
    message.includes("new participant joins are not allowed while the session is 'preparing'")
  ) {
    return { kind: 'forbidden-late-join' };
  }

  if (isFinishedOrCancelledState(message) || message.includes('is not accepting new participants')) {
    return { kind: 'invalid-session-state' };
  }

  if (
    isLostAccess(message) ||
    message.includes('has reached its capacity') ||
    message.includes('is already connected')
  ) {
    return { kind: 'lost-access' };
  }

  return { kind: 'error' };
}
