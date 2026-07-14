/**
 * Treasure-hunt QR target scanner (#223). A full-screen camera overlay launched from the play surface:
 * it requests camera permission (handling denial without crashing), shows a live viewfinder that scans
 * QR codes, submits each capture to the backend target-scan intake scoped to the participant's team, and
 * surfaces the outcome inline — an accepted scan confirms success (and advances the board's target
 * progress via `onResolved`), a rejected one shows the backend's reason.
 *
 * Resolution logic and the reject reasons live entirely in the backend (`useTargetScan` just renders the
 * verified result). No geofencing / GPS proximity — coordinates stay display-only (per #156).
 */
import { useEffect } from 'react';
import { ActivityIndicator, Modal, Pressable, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { useTargetScan } from '@/lib/realtime/use-target-scan';
import { colors, radii, spacing } from '@/constants/theme';

export function TargetScanner({
  liveSessionId,
  teamId,
  token,
  onClose,
  onResolved,
}: {
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  onClose: () => void;
  // Bumped on every accepted scan so the host can pull the advanced target progress.
  onResolved?: () => void;
}) {
  const [permission, requestPermission] = useCameraPermissions();
  const { outcome, submitScan, reset } = useTargetScan({
    liveSessionId,
    teamId,
    token,
    onResolved,
  });

  // Ask once on mount when the OS still allows a prompt. A hard denial (`canAskAgain === false`) is left
  // to the settings-hint branch below — re-requesting there is a silent no-op that only confuses.
  useEffect(() => {
    if (permission && !permission.granted && permission.canAskAgain) {
      void requestPermission();
    }
  }, [permission, requestPermission]);

  return (
    <Modal
      visible
      animationType="slide"
      onRequestClose={onClose}
      statusBarTranslucent
      presentationStyle="fullScreen"
    >
      <View style={{ flex: 1, backgroundColor: '#000' }}>
        {permission == null ? (
          <PermissionPending />
        ) : !permission.granted ? (
          <PermissionDenied
            canAskAgain={permission.canAskAgain}
            onRequest={requestPermission}
            onClose={onClose}
          />
        ) : (
          <>
            <CameraView
              testID="target-scanner-camera"
              style={{ flex: 1 }}
              facing="back"
              barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
              // Freeze capture unless idle: a terminal/submitting outcome keeps the last scan's result on
              // screen instead of firing a fresh submission every camera frame.
              onBarcodeScanned={
                outcome.kind === 'idle' ? ({ data }) => submitScan(data) : undefined
              }
            />
            <ScannerOverlay outcome={outcome} onClose={onClose} onScanAgain={reset} />
          </>
        )}
      </View>
    </Modal>
  );
}

function PermissionPending() {
  return (
    <View style={styles.centered}>
      <ActivityIndicator size="large" color={colors.emberAccent} />
      <Text variant="body" style={{ color: colors.textInkNight }}>
        Preparing the camera…
      </Text>
    </View>
  );
}

// Camera permission was refused. `canAskAgain` splits the copy: the OS may still show a prompt (retry
// in-app) or the participant must flip it in system settings. Either way there is a clear message and a
// way out — never a crash (AC).
function PermissionDenied({
  canAskAgain,
  onRequest,
  onClose,
}: {
  canAskAgain: boolean;
  onRequest: () => void;
  onClose: () => void;
}) {
  return (
    <View
      accessibilityRole="alert"
      style={[styles.centered, { paddingHorizontal: spacing.xl, gap: spacing.md }]}
    >
      <Text variant="headline" style={{ color: colors.textInkNight, textAlign: 'center' }}>
        Camera access needed
      </Text>
      <Text variant="body" style={{ color: colors.textInkNight, textAlign: 'center', opacity: 0.85 }}>
        {canAskAgain
          ? 'Scanning a target QR needs your camera. Allow access to continue.'
          : 'Camera access is off for this app. Turn it on in your device settings to scan targets.'}
      </Text>
      <View style={{ alignSelf: 'stretch', gap: spacing.sm }}>
        {canAskAgain ? (
          <Button label="Allow camera" variant="primary" onPress={onRequest} />
        ) : null}
        <Button label="Close" variant="secondary" onPress={onClose} />
      </View>
    </View>
  );
}

// Chrome layered over the live camera: a close control, a viewfinder reticle, and the status card that
// reflects the scan lifecycle. Uses `pointerEvents="box-none"` so the camera preview keeps receiving
// touches everywhere the chrome isn't.
function ScannerOverlay({
  outcome,
  onClose,
  onScanAgain,
}: {
  outcome: ReturnType<typeof useTargetScan>['outcome'];
  onClose: () => void;
  onScanAgain: () => void;
}) {
  return (
    <View style={styles.overlay} pointerEvents="box-none">
      <View style={styles.overlayHeader} pointerEvents="box-none">
        <Text variant="label" style={{ color: '#FFF' }}>
          SCAN TARGET
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Close scanner"
          onPress={onClose}
          hitSlop={12}
          style={styles.closeButton}
        >
          <Text variant="label" style={{ color: '#FFF' }}>
            CLOSE
          </Text>
        </Pressable>
      </View>

      <View style={styles.reticleWrap} pointerEvents="none">
        <View
          accessibilityRole="image"
          accessibilityLabel="Point the camera at a target QR code"
          style={[
            styles.reticle,
            outcome.kind === 'accepted' ? { borderColor: colors.signalSuccess } : null,
            outcome.kind === 'rejected' ? { borderColor: colors.signalCritical } : null,
          ]}
        />
      </View>

      <View style={styles.statusSlot} pointerEvents="box-none">
        <ScanStatus outcome={outcome} onClose={onClose} onScanAgain={onScanAgain} />
      </View>
    </View>
  );
}

function ScanStatus({
  outcome,
  onClose,
  onScanAgain,
}: {
  outcome: ReturnType<typeof useTargetScan>['outcome'];
  onClose: () => void;
  onScanAgain: () => void;
}) {
  if (outcome.kind === 'idle') {
    return (
      <View style={styles.hintCard} pointerEvents="none">
        <Text variant="body" style={{ color: '#FFF', textAlign: 'center' }}>
          Line up a target QR code to scan it.
        </Text>
      </View>
    );
  }

  if (outcome.kind === 'submitting') {
    return (
      <View style={[styles.statusCard]} accessibilityRole="text">
        <ActivityIndicator size="small" color={colors.emberAccent} />
        <Text variant="body" style={{ color: colors.textInk }}>
          Registering your scan…
        </Text>
      </View>
    );
  }

  if (outcome.kind === 'accepted') {
    return (
      <View
        style={[styles.statusCard, { borderColor: colors.signalSuccess }]}
        accessibilityRole="alert"
      >
        <View style={{ gap: spacing.xs }}>
          <Text variant="label" style={{ color: colors.signalSuccess }}>
            TARGET RESOLVED
          </Text>
          <Text variant="body" style={{ color: colors.textInk }}>
            Nice find — your team is one target closer.
          </Text>
        </View>
        <View style={{ flexDirection: 'row', gap: spacing.sm }}>
          <View style={{ flex: 1 }}>
            <Button label="Scan another" variant="primary" onPress={onScanAgain} />
          </View>
          <View style={{ flex: 1 }}>
            <Button label="Done" variant="secondary" onPress={onClose} />
          </View>
        </View>
      </View>
    );
  }

  // rejected
  return (
    <View
      style={[styles.statusCard, { borderColor: colors.signalCritical }]}
      accessibilityRole="alert"
    >
      <View style={{ gap: spacing.xs }}>
        <Text variant="label" style={{ color: colors.signalCritical }}>
          {outcome.title.toUpperCase()}
        </Text>
        <Text variant="body" style={{ color: colors.textInk }}>
          {outcome.message}
        </Text>
      </View>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <View style={{ flex: 1 }}>
          <Button label="Try again" variant="primary" onPress={onScanAgain} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Close" variant="secondary" onPress={onClose} />
        </View>
      </View>
    </View>
  );
}

const styles = {
  centered: {
    flex: 1,
    alignItems: 'center' as const,
    justifyContent: 'center' as const,
    gap: spacing.md,
    backgroundColor: colors.charcoalRoom,
  },
  overlay: {
    ...({ position: 'absolute' } as const),
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
  },
  overlayHeader: {
    flexDirection: 'row' as const,
    alignItems: 'center' as const,
    justifyContent: 'space-between' as const,
    paddingTop: 56,
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.md,
  },
  closeButton: {
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.6)',
    borderRadius: radii.pill,
    borderCurve: 'continuous' as const,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
  },
  reticleWrap: {
    flex: 1,
    alignItems: 'center' as const,
    justifyContent: 'center' as const,
  },
  reticle: {
    width: 240,
    height: 240,
    borderWidth: 3,
    borderColor: 'rgba(255,255,255,0.9)',
    borderRadius: radii.card,
    borderCurve: 'continuous' as const,
  },
  statusSlot: {
    paddingHorizontal: spacing.lg,
    paddingBottom: 48,
    gap: spacing.sm,
  },
  hintCard: {
    alignSelf: 'center' as const,
    backgroundColor: 'rgba(0,0,0,0.55)',
    borderRadius: radii.control,
    borderCurve: 'continuous' as const,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  statusCard: {
    backgroundColor: colors.paperSurface,
    borderRadius: radii.card,
    borderCurve: 'continuous' as const,
    borderWidth: 1.5,
    borderColor: colors.borderSoft,
    padding: spacing.md,
    gap: spacing.md,
  },
};
