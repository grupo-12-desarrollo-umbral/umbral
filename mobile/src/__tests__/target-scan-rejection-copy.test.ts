import {
  targetScanRejectionTitle,
  targetScanRejectionMessage,
} from '@/lib/realtime/target-scan-rejection-copy';
import type { TargetScanRejectionReasonCode } from '@/lib/realtime/target-scan-types';

const ALL_CODES: TargetScanRejectionReasonCode[] = [
  'retained-rejection',
  'session-not-accepting',
  'not-a-participant',
  'unauthorized',
  'session-not-found',
  'invalid-scan',
  'network',
  'unknown',
];

describe('target-scan rejection copy', () => {
  test('every reason code has a non-empty title and message', () => {
    for (const code of ALL_CODES) {
      expect(targetScanRejectionTitle(code)).toBeTruthy();
      expect(targetScanRejectionMessage(code)).toBeTruthy();
    }
  });

  test('a retained rejection surfaces the backend detail as the message', () => {
    const detail = 'The scanned value does not resolve to a target.';
    expect(targetScanRejectionMessage('retained-rejection', detail)).toBe(detail);
  });

  test('a retained rejection with blank detail falls back to static copy', () => {
    const fallback = targetScanRejectionMessage('retained-rejection');
    expect(targetScanRejectionMessage('retained-rejection', '   ')).toBe(fallback);
    expect(fallback).toBeTruthy();
  });

  test('detail is ignored for non-retained codes', () => {
    const withDetail = targetScanRejectionMessage('network', 'some server text');
    const withoutDetail = targetScanRejectionMessage('network');
    expect(withDetail).toBe(withoutDetail);
  });
});
