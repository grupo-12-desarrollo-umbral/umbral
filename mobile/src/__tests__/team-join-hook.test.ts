import { ApiError } from '@/lib/api/client';
import { resolveTeamJoinError } from '@/lib/membership/use-team-join';

describe('resolveTeamJoinError', () => {
  test('401 -> unauthorized outcome', () => {
    expect(resolveTeamJoinError(new ApiError(401, 'api_error', 'Unauthorized')))
      .toEqual({ kind: 'unauthorized' });
  });

  test('status 0 -> network copy', () => {
    expect(
      resolveTeamJoinError(
        new ApiError(0, 'network_error', 'Network request failed'),
      ),
    ).toEqual({
      kind: 'failed',
      message: 'Network error. Check your connection and try again.',
    });
  });

  test('404 -> unavailable team copy', () => {
    expect(resolveTeamJoinError(new ApiError(404, 'api_error', 'Not found')))
      .toEqual({
        kind: 'failed',
        message: 'This team is no longer available for the selected session.',
      });
  });

  test('403 and 409 -> locked-team copy', () => {
    expect(resolveTeamJoinError(new ApiError(403, 'api_error', 'Forbidden')))
      .toEqual({
        kind: 'failed',
        message:
          "You don't belong to this team. You can only enter the team you were assigned to.",
      });

    expect(resolveTeamJoinError(new ApiError(409, 'api_error', 'Conflict')))
      .toEqual({
        kind: 'failed',
        message:
          "You don't belong to this team. You can only enter the team you were assigned to.",
      });
  });

  test('other errors -> generic copy', () => {
    expect(resolveTeamJoinError(new Error('boom'))).toEqual({
      kind: 'failed',
      message: 'Failed to join team. Please try again.',
    });
  });
});
