import React from 'react';
import { act, create } from 'react-test-renderer';
import { ApiError } from '@/lib/api/client';
import {
  resolveTeamJoinError,
  useTeamJoin,
  type TeamJoinOutcome,
} from '@/lib/membership/use-team-join';
import type { JoinSessionTeamResponse } from '@/lib/api/teams';

jest.mock('@/lib/api/teams', () => ({
  joinSessionTeam: jest.fn(),
}));

const mockJoin = jest.requireMock('@/lib/api/teams').joinSessionTeam as jest.Mock;

type UseTeamJoinResult = ReturnType<typeof useTeamJoin>;

// Container object (not a bare reassignable binding) so TestComponent captures the hook result
// without reassigning a variable declared outside the component — mirrors use-target-scan.test.
const hookResult: { current: UseTeamJoinResult | null } = { current: null };

function TestComponent() {
  // Test harness: capture the hook's return so assertions can read it outside render.
  // eslint-disable-next-line react-hooks/immutability
  hookResult.current = useTeamJoin();
  return null;
}

function renderHook() {
  act(() => {
    create(React.createElement(TestComponent));
  });
  return { get: () => hookResult.current! };
}

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

// A manually settled promise so a request can be held in-flight across deterministic assertions.
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

// Flush the microtask that carries the deferred API invocation and any settled continuations.
async function flushMicrotasks() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe('useTeamJoin', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    hookResult.current = null;
  });

  test('the guard is armed before the API runs, and same-tick taps collapse to one POST', async () => {
    const d = deferred<JoinSessionTeamResponse>();
    mockJoin.mockImplementationOnce(() => d.promise);

    const hook = renderHook();

    let first!: Promise<TeamJoinOutcome>;
    act(() => {
      first = hook.get().join('SES-1', 'team-1');
    });

    // Request construction is deferred to a microtask, so the POST has not begun yet — but the guard is
    // already armed, which the collapsing second tap proves.
    expect(mockJoin).not.toHaveBeenCalled();

    let second!: Promise<TeamJoinOutcome>;
    act(() => {
      second = hook.get().join('SES-1', 'team-1');
    });
    expect(second).toBe(first);

    await flushMicrotasks();
    expect(mockJoin).toHaveBeenCalledTimes(1);

    const joined: TeamJoinOutcome = {
      kind: 'joined',
      response: { teamMembershipId: 'membership-1' },
    };
    await act(async () => {
      d.resolve({ teamMembershipId: 'membership-1' });
      await first;
    });

    expect(await first).toEqual(joined);
    expect(await second).toEqual(joined);
    expect(hook.get().outcome).toEqual(joined);
    expect(hook.get().status).toBe('resolved');
  });

  test('a failed request re-enables joining', async () => {
    mockJoin.mockRejectedValueOnce(
      new ApiError(0, 'network_error', 'Network request failed'),
    );

    const hook = renderHook();
    await act(async () => {
      await hook.get().join('SES-1', 'team-1');
    });

    expect(hook.get().outcome).toEqual({
      kind: 'failed',
      message: 'Network error. Check your connection and try again.',
    });

    // The guard cleared on the failed terminal outcome, so a retry issues a fresh POST rather than being
    // swallowed as a duplicate.
    mockJoin.mockResolvedValueOnce({ teamMembershipId: 'membership-2' });
    await act(async () => {
      await hook.get().join('SES-1', 'team-1');
    });

    expect(mockJoin).toHaveBeenCalledTimes(2);
    expect(hook.get().outcome).toEqual({
      kind: 'joined',
      response: { teamMembershipId: 'membership-2' },
    });
  });

  test('a synchronously thrown API call fails and still permits a retry', async () => {
    mockJoin.mockImplementationOnce(() => {
      throw new ApiError(0, 'network_error', 'Network request failed');
    });

    const hook = renderHook();
    await act(async () => {
      await hook.get().join('SES-1', 'team-1');
    });

    // A synchronous request-construction exception maps to a failure like any rejection…
    expect(hook.get().outcome).toEqual({
      kind: 'failed',
      message: 'Network error. Check your connection and try again.',
    });

    // …and leaves the guard cleared, so the next tap issues a fresh POST.
    mockJoin.mockResolvedValueOnce({ teamMembershipId: 'membership-retry' });
    await act(async () => {
      await hook.get().join('SES-1', 'team-1');
    });

    expect(mockJoin).toHaveBeenCalledTimes(2);
    expect(hook.get().outcome).toEqual({
      kind: 'joined',
      response: { teamMembershipId: 'membership-retry' },
    });
  });

  test('reset invalidates request A: B starts, and A cannot clear B or replace its outcome', async () => {
    const a = deferred<JoinSessionTeamResponse>();
    const b = deferred<JoinSessionTeamResponse>();
    mockJoin.mockImplementationOnce(() => a.promise).mockImplementationOnce(() => b.promise);

    const hook = renderHook();

    let requestA!: Promise<TeamJoinOutcome>;
    act(() => {
      requestA = hook.get().join('SES-1', 'team-a');
    });
    await flushMicrotasks();

    // Reset invalidates A, then B is admitted (A's guard no longer blocks it).
    act(() => {
      hook.get().reset();
    });
    expect(hook.get().status).toBe('idle');
    expect(hook.get().outcome).toBeNull();

    let requestB!: Promise<TeamJoinOutcome>;
    act(() => {
      requestB = hook.get().join('SES-1', 'team-b');
    });
    await flushMicrotasks();
    expect(mockJoin).toHaveBeenCalledTimes(2);

    // A completes after reset + after B started: it must neither clear B's guard nor publish anything.
    await act(async () => {
      a.resolve({ teamMembershipId: 'membership-a' });
      await requestA;
    });
    expect(hook.get().outcome).toBeNull();
    expect(hook.get().status).toBe('joining');

    // A third tap during B must still collapse onto B (its guard survived A's completion).
    let duplicateB!: Promise<TeamJoinOutcome>;
    act(() => {
      duplicateB = hook.get().join('SES-1', 'team-b');
    });
    expect(duplicateB).toBe(requestB);
    expect(mockJoin).toHaveBeenCalledTimes(2);

    // B is the rendered terminal outcome regardless of A/B completion order.
    const joinedB: TeamJoinOutcome = {
      kind: 'joined',
      response: { teamMembershipId: 'membership-b' },
    };
    await act(async () => {
      b.resolve({ teamMembershipId: 'membership-b' });
      await requestB;
    });
    expect(hook.get().outcome).toEqual(joinedB);
    expect(hook.get().status).toBe('resolved');
    expect(await requestA).toEqual({
      kind: 'joined',
      response: { teamMembershipId: 'membership-a' },
    });
  });
});
