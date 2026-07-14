import React from 'react';
import { act, create } from 'react-test-renderer';
import { TargetScanner } from '@/components/target-scanner';

jest.mock('expo-haptics', () => ({
  impactAsync: jest.fn(),
  notificationAsync: jest.fn(),
  ImpactFeedbackStyle: { Light: 'light' },
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

// Per-file expo-camera mock (overrides the global moduleNameMapper stand-in) so each test drives the
// permission state and captures the camera's onBarcodeScanned to simulate a scan.
const mockRequestPermission = jest.fn(() => Promise.resolve({ granted: true }));
let mockPermission: { granted: boolean; canAskAgain: boolean } | null = {
  granted: true,
  canAskAgain: true,
};
jest.mock('expo-camera', () => ({
  CameraView: (props: Record<string, unknown>) => {
    const rn = require('react-native');
    return require('react').createElement(rn.View, props);
  },
  useCameraPermissions: () => [mockPermission, mockRequestPermission],
}));

jest.mock('@/lib/api/sessions', () => ({
  registerTargetScan: jest.fn(),
  RegisterTargetScanRejection: jest.requireActual('@/lib/api/sessions').RegisterTargetScanRejection,
}));

const mockScan = jest.requireMock('@/lib/api/sessions').registerTargetScan as jest.Mock;
const { RegisterTargetScanRejection } = jest.requireActual('@/lib/api/sessions');

type TreeNode = { props?: Record<string, unknown>; children?: (TreeNode | string | number)[] | null };
function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

const PROPS = { liveSessionId: 'sess-1', teamId: 'team-1', token: 'tok' };

function render(extra?: Partial<React.ComponentProps<typeof TargetScanner>>) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(TargetScanner, {
        ...PROPS,
        onClose: jest.fn(),
        onResolved: jest.fn(),
        ...extra,
      }),
    );
  });
  return renderer!;
}

// A single element renders as several matching nodes (composite + host), so index the first.
function findCamera(renderer: ReturnType<typeof create>) {
  return renderer.root.findAllByProps({ testID: 'target-scanner-camera' });
}

function fireScan(renderer: ReturnType<typeof create>, data: string) {
  const camera = findCamera(renderer)[0];
  act(() => {
    (camera.props.onBarcodeScanned as (r: { data: string }) => void)({ data });
  });
}

describe('TargetScanner', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockPermission = { granted: true, canAskAgain: true };
  });

  test('renders the camera and idle hint when permission is granted', () => {
    const renderer = render();
    expect(findCamera(renderer).length).toBeGreaterThan(0);
    expect(allText(renderer.toJSON()).join(' ')).toContain('Line up a target QR code');
  });

  test('a granted-but-not-yet-asked permission triggers a request on mount', () => {
    mockPermission = { granted: false, canAskAgain: true };
    render();
    expect(mockRequestPermission).toHaveBeenCalled();
  });

  test('a hard denial shows a settings hint and does not re-request', () => {
    mockPermission = { granted: false, canAskAgain: false };
    const renderer = render();

    expect(mockRequestPermission).not.toHaveBeenCalled();
    const text = allText(renderer.toJSON()).join(' ');
    expect(text).toContain('Camera access is off');
    // No camera preview when permission is refused.
    expect(findCamera(renderer)).toHaveLength(0);
  });

  test('an accepted scan shows success and calls onResolved', async () => {
    mockScan.mockResolvedValueOnce({ targetSnapshotId: 'target-1', isResolved: true });
    const onResolved = jest.fn();
    const renderer = render({ onResolved });

    await act(async () => {
      fireScan(renderer, 'QR-ALPHA');
    });

    expect(mockScan).toHaveBeenCalledWith('sess-1', {
      teamId: 'team-1',
      scannedValue: 'QR-ALPHA',
      token: 'tok',
    });
    expect(allText(renderer.toJSON()).join(' ')).toContain('TARGET RESOLVED');
    expect(onResolved).toHaveBeenCalledTimes(1);
  });

  test('a rejected scan surfaces the backend reason', async () => {
    mockScan.mockRejectedValueOnce(
      new RegisterTargetScanRejection(
        'retained-rejection',
        422,
        'The scanned value does not resolve to a target.',
      ),
    );
    const renderer = render();

    await act(async () => {
      fireScan(renderer, 'QR-BOGUS');
    });

    const text = allText(renderer.toJSON()).join(' ');
    expect(text).toContain('The scanned value does not resolve to a target.');
  });

  test('the camera stops scanning once an outcome is showing', async () => {
    mockScan.mockResolvedValueOnce({ targetSnapshotId: 'target-1', isResolved: true });
    const renderer = render();

    await act(async () => {
      fireScan(renderer, 'QR-ALPHA');
    });

    // After a terminal outcome the camera's onBarcodeScanned is unwired (undefined), so no further
    // submissions can fire until the scanner is reset.
    const camera = findCamera(renderer)[0];
    expect(camera.props.onBarcodeScanned).toBeUndefined();
    expect(mockScan).toHaveBeenCalledTimes(1);
  });

  test('close control invokes onClose', () => {
    const onClose = jest.fn();
    const renderer = render({ onClose });
    const closeBtn = renderer.root.findAllByProps({ accessibilityLabel: 'Close scanner' })[0];
    act(() => (closeBtn.props.onPress as () => void)());
    expect(onClose).toHaveBeenCalled();
  });
});
