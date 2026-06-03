import { ApiError } from '@/lib/api/client';
import { interpretLobbyError } from '@/lib/membership/use-team-lobby';

describe('interpretLobbyError', () => {
  test('status 0 → network copy', () => {
    expect(
      interpretLobbyError(
        new ApiError(0, 'network_error', 'Network request failed'),
      ),
    ).toEqual({
      kind: 'network-error',
      message: 'Network error. Check your connection and try again.',
    });
  });

  test('400 → invalid-input copy', () => {
    expect(
      interpretLobbyError(
        new ApiError(400, 'api_error', 'Validation failed'),
      ),
    ).toEqual({
      kind: 'invalid-input',
      message: 'Enter a valid 6-character session code.',
    });
  });

  test('401 → unauthorized', () => {
    expect(
      interpretLobbyError(new ApiError(401, 'api_error', 'Unauthorized')),
    ).toEqual({ kind: 'unauthorized' });
  });

  test('403 → forbidden copy', () => {
    expect(
      interpretLobbyError(new ApiError(403, 'api_error', 'Forbidden')),
    ).toEqual({
      kind: 'forbidden',
      message: 'Your participant access is no longer available. Contact your operator.',
    });
  });

  test('404 → session not found copy', () => {
    expect(
      interpretLobbyError(new ApiError(404, 'api_error', 'Not found')),
    ).toEqual({
      kind: 'not-found',
      message: 'Session code not found. Check the code and try again.',
    });
  });

  test('other errors → generic copy', () => {
    expect(interpretLobbyError(new Error('boom'))).toEqual({
      kind: 'error',
      message: 'Failed to load teams. Please try again.',
    });
  });
});
