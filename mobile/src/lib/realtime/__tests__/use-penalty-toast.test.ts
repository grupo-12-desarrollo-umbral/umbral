import { act, create } from 'react-test-renderer';
import React from 'react';
import { usePenaltyToast, type PenaltyToastData } from '../use-penalty-toast';
import type { PenaltyAppliedDto } from '../penalty-types';
import type { ScoringHubClient } from '../scoring-hub';

const LIVE_SESSION_ID = 'sess-1';
const OWN_TEAM_ID = 'team-1';

function penalty(overrides: Partial<PenaltyAppliedDto> = {}): PenaltyAppliedDto {
  return {
    liveSessionId: LIVE_SESSION_ID,
    teamId: OWN_TEAM_ID,
    penaltyId: 'pen-1',
    scoreEntryId: 'entry-1',
    deductionMagnitude: 100,
    reason: 'Unsportsmanlike conduct',
    appliedAt: '2026-07-15T10:00:00Z',
    ...overrides,
  };
}

// A fake ScoringHubClient that captures the `onPenaltyApplied` callback so a test can push events into it.
function fakeClient() {
  let handler: ((p: PenaltyAppliedDto) => void) | null = null;
  const off = jest.fn(() => {
    handler = null;
  });
  const client = {
    onPenaltyApplied: jest.fn((cb: (p: PenaltyAppliedDto) => void) => {
      handler = cb;
      return off;
    }),
  } as unknown as ScoringHubClient;
  return {
    client,
    off,
    push: (p: PenaltyAppliedDto) => act(() => handler?.(p)),
  };
}

// Drive the hook through a host component so effects run, capturing its latest return value.
function renderHook(scoringClient?: ScoringHubClient | null) {
  const results: { penalty: PenaltyToastData | null; clear: () => void }[] = [];
  function Host() {
    results.push(usePenaltyToast(LIVE_SESSION_ID, OWN_TEAM_ID, scoringClient));
    return null;
  }
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(Host));
  });
  return {
    latest: () => results[results.length - 1],
    unmount: () => act(() => renderer?.unmount()),
  };
}

describe('usePenaltyToast', () => {
  test('surfaces a penalty pushed for the own team', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    expect(hook.latest().penalty).toBeNull();

    hub.push(penalty({ deductionMagnitude: 100, reason: 'Late arrival' }));

    expect(hook.latest().penalty).toMatchObject({ magnitude: 100, reason: 'Late arrival' });
    hook.unmount();
  });

  test('ignores penalties for other teams', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    hub.push(penalty({ teamId: 'team-2' }));

    expect(hook.latest().penalty).toBeNull();
    hook.unmount();
  });

  test('ignores penalties for a different session', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    hub.push(penalty({ liveSessionId: 'sess-2' }));

    expect(hook.latest().penalty).toBeNull();
    hook.unmount();
  });

  test('assigns a fresh id per penalty so the toast remounts', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    hub.push(penalty());
    const first = hook.latest().penalty;
    hub.push(penalty());
    const second = hook.latest().penalty;

    expect(first?.id).toBeDefined();
    expect(second?.id).not.toBe(first?.id);
    hook.unmount();
  });

  test('clear() dismisses the current penalty', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    hub.push(penalty());
    expect(hook.latest().penalty).not.toBeNull();

    act(() => hook.latest().clear());
    expect(hook.latest().penalty).toBeNull();
    hook.unmount();
  });

  test('unsubscribes on unmount', () => {
    const hub = fakeClient();
    const hook = renderHook(hub.client);

    hook.unmount();
    expect(hub.off).toHaveBeenCalled();
  });

  test('is inert without a scoring client', () => {
    const hook = renderHook(null);
    expect(hook.latest().penalty).toBeNull();
    hook.unmount();
  });
});
