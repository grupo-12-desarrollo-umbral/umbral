import { useCallback, useRef, useState } from 'react';
import * as Haptics from 'expo-haptics';
import { registerTargetScan, RegisterTargetScanRejection } from '@/lib/api/sessions';
import {
  targetScanRejectionTitle,
  targetScanRejectionMessage,
} from './target-scan-rejection-copy';
import type { TargetScanRejectionReasonCode } from './target-scan-types';

// The scan lifecycle. `idle` accepts a fresh capture; `submitting` blocks re-entry while the POST is in
// flight; `accepted`/`rejected` are terminal until `reset()` re-arms the scanner. Keeping a single
// discriminated outcome (rather than separate booleans) makes the camera easy to freeze: it scans only
// while `idle`.
export type TargetScanOutcome =
  | { kind: 'idle' }
  | { kind: 'submitting' }
  | { kind: 'accepted'; targetSnapshotId: string | null }
  | {
      kind: 'rejected';
      reasonCode: TargetScanRejectionReasonCode;
      title: string;
      message: string;
    };

export type UseTargetScanResult = {
  outcome: TargetScanOutcome;
  // Submit a captured QR payload. A no-op unless the scanner is idle, so the camera's repeated
  // `onBarcodeScanned` callbacks collapse to a single submission per capture.
  submitScan: (scannedValue: string) => void;
  // Re-arm the scanner after a terminal outcome.
  reset: () => void;
};

function fireHaptic(type: 'success' | 'error') {
  if (process.env.EXPO_OS === 'ios') {
    Haptics.notificationAsync(
      type === 'success'
        ? Haptics.NotificationFeedbackType.Success
        : Haptics.NotificationFeedbackType.Error,
    );
  }
}

/**
 * Owns the capture → submit → feedback loop for the treasure-hunt QR scanner (#223). Given the
 * participant's live-session/team scope, it POSTs a scanned value to the backend target-scan intake and
 * maps the result to a terminal outcome: an accepted scan fires `onResolved` (so the play surface can
 * pull the advanced target progress) and a rejected one carries the backend's reason. Resolution,
 * validation and the reject reasons live entirely in the backend — this only renders the verified
 * result.
 */
export function useTargetScan({
  liveSessionId,
  teamId,
  token,
  onResolved,
}: {
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  // Called once per accepted scan so the caller can refresh the board (progress numerator advances).
  onResolved?: () => void;
}): UseTargetScanResult {
  const [outcome, setOutcome] = useState<TargetScanOutcome>({ kind: 'idle' });

  // Ref guard: state updates are async, and the camera can fire `onBarcodeScanned` several times per
  // frame — a bare `outcome` check would let a double-capture sneak two POSTs past the `idle` gate.
  const busyRef = useRef(false);

  const reset = useCallback(() => {
    busyRef.current = false;
    setOutcome({ kind: 'idle' });
  }, []);

  const submitScan = useCallback(
    async (scannedValue: string) => {
      const value = scannedValue.trim();
      if (busyRef.current || value.length === 0) return;

      busyRef.current = true;
      setOutcome({ kind: 'submitting' });

      try {
        const result = await registerTargetScan(liveSessionId, {
          teamId,
          scannedValue: value,
          token,
        });
        setOutcome({ kind: 'accepted', targetSnapshotId: result.targetSnapshotId });
        fireHaptic('success');
        onResolved?.();
      } catch (error) {
        const reasonCode =
          error instanceof RegisterTargetScanRejection ? error.reasonCode : 'unknown';
        const detail =
          error instanceof RegisterTargetScanRejection ? error.detail : undefined;
        setOutcome({
          kind: 'rejected',
          reasonCode,
          title: targetScanRejectionTitle(reasonCode),
          message: targetScanRejectionMessage(reasonCode, detail),
        });
        fireHaptic('error');
      }
      // busyRef stays true until reset(): the outcome is terminal and the camera is frozen, so we must
      // not re-arm until the participant dismisses the result.
    },
    [liveSessionId, teamId, token, onResolved],
  );

  return { outcome, submitScan, reset };
}
