import {
  buildReconnectContext,
  resolveReconnectContext,
} from '@/lib/realtime/reconnect-context-resolution';
import type { ReconnectContext } from '@/lib/realtime/sessions-hub-types';

describe('reconnect-context-resolution', () => {
  test('builds reconnect context from complete route data', () => {
    expect(
      buildReconnectContext({
        liveSessionId: 'session-1',
        teamId: 'team-1',
        displayName: 'Alex',
      }),
    ).toEqual({
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Alex',
      token: null,
    });
  });

  test('returns null when required route data is incomplete', () => {
    expect(
      buildReconnectContext({
        liveSessionId: 'session-1',
        teamId: 'team-1',
        displayName: '',
      }),
    ).toBeNull();
  });

  test('falls back to persisted context when matching route identity cannot build a route context', () => {
    const persisted: ReconnectContext = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Alex',
      token: null,
    };

    expect(
      resolveReconnectContext(
        {
          liveSessionId: 'session-1',
          teamId: 'team-1',
          displayName: '',
        },
        persisted,
      ),
    ).toEqual(persisted);
  });

  test('prefers complete route identity over stale persisted context', () => {
    const persisted: ReconnectContext = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Alex',
      token: null,
    };

    expect(
      resolveReconnectContext(
        {
          liveSessionId: 'session-2',
          teamId: 'team-9',
          displayName: 'Alex',
        },
        persisted,
      ),
    ).toEqual({
      liveSessionId: 'session-2',
      teamId: 'team-9',
      displayName: 'Alex',
      token: null,
    });
  });

  test('uses persisted context on resume when there are no route params', () => {
    const persisted: ReconnectContext = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Alex',
      token: null,
    };

    expect(
      resolveReconnectContext(
        {
          liveSessionId: '',
          teamId: '',
          displayName: '',
        },
        persisted,
      ),
    ).toEqual(persisted);
  });
});
