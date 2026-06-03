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

  // The backend rejects via a HubException carrying `{"code":"...","message":"..."}`.
  // The policy keys on the code, so the tests pin the code→outcome mapping.
  function hubError(code: string, message = 'human readable detail'): Error {
    return new Error(JSON.stringify({ code, message }));
  }

  test('maps the late-join code to forbidden-late-join', () => {
    expect(interpretHubError(hubError('LATE_JOIN_NOT_ALLOWED'))).toEqual({
      kind: 'forbidden-late-join',
    });
  });

  test('maps the team-unavailable code to invalid session state', () => {
    expect(interpretHubError(hubError('TEAM_UNAVAILABLE'))).toEqual({
      kind: 'invalid-session-state',
    });
  });

  test('maps the participant-removed code to lost access', () => {
    expect(interpretHubError(hubError('PARTICIPANT_REMOVED'))).toEqual({
      kind: 'lost-access',
    });
  });

  test('maps the already-connected code to its own outcome', () => {
    expect(interpretHubError(hubError('ALREADY_CONNECTED'))).toEqual({
      kind: 'already-connected',
    });
  });

  test('maps the wrong-team code to its own outcome', () => {
    expect(interpretHubError(hubError('WRONG_TEAM'))).toEqual({
      kind: 'wrong-team',
    });
  });

  test('extracts the code even when SignalR wraps the message in dev', () => {
    const wrapped = new Error(
      "An unexpected error occurred invoking 'ReconnectAsync'. HubException: " +
        JSON.stringify({ code: 'WRONG_TEAM', message: 'detail' }),
    );
    expect(interpretHubError(wrapped)).toEqual({ kind: 'wrong-team' });
  });

  test('falls back to generic error for an unknown code', () => {
    expect(interpretHubError(hubError('SOMETHING_NEW'))).toEqual({
      kind: 'error',
    });
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
