import { HttpError, TimeoutError } from '@microsoft/signalr';
import {
  interpretHubError,
  toReconnectedOutcome,
  toUpdatedReconnectContext,
} from '@/lib/realtime/reconnect-policy';

describe('reconnect-policy', () => {
  test('maps a reconnect result to the success outcome', () => {
    const result = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      teamDisplayName: 'Red',
      sessionParticipantId: 'participant-1',
      participantDisplayName: 'Nova',
      sessionState: 'Active',
      isReconnect: true,
      joinedAt: '2026-06-03T12:00:00.000Z',
      lastSeenAt: '2026-06-03T12:05:00.000Z',
    };

    expect(toReconnectedOutcome(result)).toEqual({
      kind: 'reconnected',
      result,
    });
  });

  test('refreshes persisted reconnect context with the latest server values', () => {
    const context = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Nova',
      teamCapacity: 4,
      token: null,
    };
    const result = {
      liveSessionId: 'session-2',
      teamId: 'team-2',
      teamDisplayName: 'Blue',
      sessionParticipantId: 'participant-1',
      participantDisplayName: 'Nova Prime',
      sessionState: 'Active',
      isReconnect: true,
      joinedAt: '2026-06-03T12:00:00.000Z',
      lastSeenAt: '2026-06-03T12:05:00.000Z',
    };

    expect(toUpdatedReconnectContext(context, result)).toEqual({
      ...context,
      liveSessionId: 'session-2',
      teamId: 'team-2',
      displayName: 'Nova Prime',
      lastSeenAt: '2026-06-03T12:05:00.000Z',
    });
  });

  test('maps active-session late join hub errors', () => {
    expect(
      interpretHubError(
        new Error(
          "New participant joins are not allowed while the session is 'Active'.",
        ),
      ),
    ).toEqual({ kind: 'forbidden-late-join' });
  });

  test('maps finished-session hub errors to invalid session state', () => {
    expect(
      interpretHubError(
        new Error(
          "New participant joins are not allowed while the session is 'Finished'.",
        ),
      ),
    ).toEqual({ kind: 'invalid-session-state' });
  });

  test('maps removed-participant hub errors to lost access', () => {
    expect(
      interpretHubError(
        new Error(
          "Participant 'participant-1' was removed from the live session.",
        ),
      ),
    ).toEqual({ kind: 'lost-access' });
  });

  test('maps negotiate 401 errors to unauthorized', () => {
    expect(
      interpretHubError(new HttpError('Unauthorized', 401)),
    ).toEqual({ kind: 'unauthorized' });
  });

  test('maps connection timeouts to network error', () => {
    expect(interpretHubError(new TimeoutError())).toEqual({
      kind: 'network-error',
    });
  });

  test('falls back to generic error for unknown exceptions', () => {
    expect(interpretHubError(new Error('boom'))).toEqual({ kind: 'error' });
  });
});
