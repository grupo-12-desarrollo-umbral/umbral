import React from 'react';
import { act, create } from 'react-test-renderer';
import { useTargetScan, type UseTargetScanResult } from '@/lib/realtime/use-target-scan';
import { RegisterTargetScanRejection } from '@/lib/api/sessions';

jest.mock('expo-haptics', () => ({
  impactAsync: jest.fn(),
  notificationAsync: jest.fn(),
  ImpactFeedbackStyle: { Light: 'light' },
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

jest.mock('@/lib/api/sessions', () => ({
  registerTargetScan: jest.fn(),
  RegisterTargetScanRejection: jest.requireActual('@/lib/api/sessions').RegisterTargetScanRejection,
}));

const mockScan = jest.requireMock('@/lib/api/sessions').registerTargetScan as jest.Mock;

const DEFAULT_PROPS = { liveSessionId: 'sess-1', teamId: 'team-1', token: 'tok' };

type ScanProps = {
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  onResolved?: () => void;
};

// Container object (not a bare reassignable binding) so TestComponent captures the
// hook result without reassigning a variable declared outside the component.
const hookResult: { current: UseTargetScanResult | null } = { current: null };
let onResolved: jest.Mock;

function TestComponent(props: ScanProps) {
  // Test harness: capture the hook's return so assertions can read it outside render.
  // eslint-disable-next-line react-hooks/immutability
  hookResult.current = useTargetScan(props);
  return null;
}

function renderHook(extra?: { onResolved?: () => void }) {
  act(() => {
    create(React.createElement(TestComponent, { ...DEFAULT_PROPS, ...extra }));
  });
  return { get: () => hookResult.current! };
}

describe('useTargetScan', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    hookResult.current = null;
    onResolved = jest.fn();
  });

  test('starts idle', () => {
    const hook = renderHook();
    expect(hook.get().outcome).toEqual({ kind: 'idle' });
  });

  test('accepted scan sets outcome and fires onResolved', async () => {
    mockScan.mockResolvedValueOnce({ targetSnapshotId: 'target-9', isResolved: true });
    const hook = renderHook({ onResolved });

    await act(async () => {
      await hook.get().submitScan('QR-ALPHA');
    });

    expect(mockScan).toHaveBeenCalledWith('sess-1', {
      teamId: 'team-1',
      scannedValue: 'QR-ALPHA',
      token: 'tok',
    });
    expect(hook.get().outcome).toEqual({ kind: 'accepted', targetSnapshotId: 'target-9' });
    expect(onResolved).toHaveBeenCalledTimes(1);
  });

  test('rejected scan maps the reason and does not fire onResolved', async () => {
    mockScan.mockRejectedValueOnce(
      new RegisterTargetScanRejection('retained-rejection', 422, 'The target has already been resolved by this team.'),
    );
    const hook = renderHook({ onResolved });

    await act(async () => {
      await hook.get().submitScan('QR-ALPHA');
    });

    expect(hook.get().outcome).toEqual({
      kind: 'rejected',
      reasonCode: 'retained-rejection',
      title: 'Scan not accepted',
      message: 'The target has already been resolved by this team.',
    });
    expect(onResolved).not.toHaveBeenCalled();
  });

  test('a blank scanned value is ignored', async () => {
    const hook = renderHook();
    await act(async () => {
      await hook.get().submitScan('   ');
    });
    expect(mockScan).not.toHaveBeenCalled();
    expect(hook.get().outcome).toEqual({ kind: 'idle' });
  });

  test('a second capture while a terminal outcome stands is a no-op until reset', async () => {
    mockScan.mockResolvedValueOnce({ targetSnapshotId: 'target-1', isResolved: true });
    const hook = renderHook({ onResolved });

    await act(async () => {
      await hook.get().submitScan('QR-ONE');
    });
    expect(hook.get().outcome.kind).toBe('accepted');

    await act(async () => {
      await hook.get().submitScan('QR-TWO');
    });
    expect(mockScan).toHaveBeenCalledTimes(1);

    // reset re-arms the scanner
    act(() => hook.get().reset());
    expect(hook.get().outcome).toEqual({ kind: 'idle' });

    mockScan.mockResolvedValueOnce({ targetSnapshotId: 'target-2', isResolved: true });
    await act(async () => {
      await hook.get().submitScan('QR-TWO');
    });
    expect(mockScan).toHaveBeenCalledTimes(2);
  });

  test('a non-rejection error still surfaces as an unknown rejection', async () => {
    mockScan.mockRejectedValueOnce(new Error('boom'));
    const hook = renderHook();

    await act(async () => {
      await hook.get().submitScan('QR-ALPHA');
    });

    expect(hook.get().outcome).toMatchObject({ kind: 'rejected', reasonCode: 'unknown' });
  });
});
