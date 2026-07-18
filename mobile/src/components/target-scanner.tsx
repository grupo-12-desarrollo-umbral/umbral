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
import { useEffect, useRef } from 'react';
import {
  ActivityIndicator,
  Animated,
  Easing,
  Modal,
  Pressable,
  useAnimatedValue,
  View,
} from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { useTargetScan } from '@/lib/realtime/use-target-scan';
import { colors, radii, spacing } from '@/constants/theme';

// How long the success burst plays before the scanner dismisses itself. Long enough to read as a
// celebratory beat, short enough that a player scanning several targets isn't kept waiting.
const SUCCESS_DISMISS_MS = 1550;

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
  // The ref makes "once" real: this effect reruns on every `permission` change, and on platforms where a
  // denial still returns `canAskAgain: true` (Android without "don't ask again"), re-requesting would
  // immediately re-prompt in a loop instead of landing on the manual retry screen.
  const hasRequestedRef = useRef(false);
  useEffect(() => {
    if (hasRequestedRef.current) return;
    if (permission && !permission.granted && permission.canAskAgain) {
      hasRequestedRef.current = true;
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
        Preparando la cámara…
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
        Se necesita acceso a la cámara
      </Text>
      <Text variant="body" style={{ color: colors.textInkNight, textAlign: 'center', opacity: 0.85 }}>
        {canAskAgain
          ? 'Escanear el QR de un target necesita tu cámara. Permite el acceso para continuar.'
          : 'El acceso a la cámara está desactivado para esta app. Actívalo en los ajustes de tu dispositivo para escanear targets.'}
      </Text>
      <View style={{ alignSelf: 'stretch', gap: spacing.sm }}>
        {canAskAgain ? (
          <Button label="Permitir cámara" variant="primary" onPress={onRequest} />
        ) : null}
        <Button label="Cerrar" variant="secondary" onPress={onClose} />
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
          ESCANEAR TARGET
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Cerrar escáner"
          onPress={onClose}
          hitSlop={12}
          style={styles.closeButton}
        >
          <Text variant="label" style={{ color: '#FFF' }}>
            CERRAR
          </Text>
        </Pressable>
      </View>

      <View style={styles.reticleWrap} pointerEvents="none">
        <View
          accessibilityRole="image"
          accessibilityLabel="Apunta la cámara a un código QR de target"
          style={[
            styles.reticle,
            outcome.kind === 'accepted' ? { borderColor: colors.emberAccent } : null,
            outcome.kind === 'rejected' ? { borderColor: colors.signalCritical } : null,
          ]}
        />
      </View>

      <View style={styles.statusSlot} pointerEvents="box-none">
        <ScanStatus outcome={outcome} onClose={onClose} onScanAgain={onScanAgain} />
      </View>

      {/* An accepted scan takes over the whole overlay with a celebratory burst that then dismisses the
          scanner on its own — no "Done" tap needed. Layered last so it sits above the reticle + status. */}
      {outcome.kind === 'accepted' ? <ScanSuccessBurst onDone={onClose} /> : null}
    </View>
  );
}

// Success moment for an accepted scan, staged as a warm celebration sheet that rises from the bottom edge
// into the lower portion of the screen — not a full-screen takeover — so the camera and reticle stay
// visible above it. On-brand ember/parchment (the Lantern Control Room): a paper surface with a top ember
// hairline, an ember badge that springs in behind a warm glow and a single contained pulse ring, then the
// "¡Felicitaciones!" copy. It auto-dismisses. Mounts only while the outcome is `accepted`, so its
// animation/haptic fire exactly once per successful scan.
function ScanSuccessBurst({ onDone }: { onDone: () => void }) {
  // 0 → 1 drives the sheet's rise + fade; interpolated into a translateY so it slides up from below.
  const sheetProgress = useAnimatedValue(0);
  const badgeScale = useAnimatedValue(0);
  // A single ring pulses out from behind the badge — contained within the sheet, not a full-screen flare.
  const ringScale = useAnimatedValue(0);
  const ringOpacity = useAnimatedValue(0.6);

  useEffect(() => {
    // Success haptic — iOS only, matching the play surface's guard (Android lacks the notification pattern).
    if (process.env.EXPO_OS === 'ios') {
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
    }

    const animation = Animated.parallel([
      // Sheet slides up and fades in together.
      Animated.timing(sheetProgress, {
        toValue: 1,
        duration: 340,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: false,
      }),
      // Badge overshoots then settles — the "pop" that makes success feel earned.
      Animated.spring(badgeScale, {
        toValue: 1,
        friction: 5,
        tension: 140,
        delay: 120,
        useNativeDriver: false,
      }),
      Animated.timing(ringScale, {
        toValue: 1,
        duration: 620,
        delay: 120,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: false,
      }),
      // Every timing carries an explicit non-bezier easing: the default easing samples Easing.bezier,
      // which is unavailable under the jest-expo Animated shim.
      Animated.timing(ringOpacity, {
        toValue: 0,
        duration: 620,
        delay: 120,
        easing: Easing.out(Easing.quad),
        useNativeDriver: false,
      }),
    ]);
    animation.start();

    const timer = setTimeout(onDone, SUCCESS_DISMISS_MS);
    // Stop the animation (and its chained delay timer) and cancel the dismiss on unmount, so nothing
    // fires into a torn-down tree if the scanner closes early.
    return () => {
      clearTimeout(timer);
      animation.stop();
    };
  }, [sheetProgress, badgeScale, ringScale, ringOpacity, onDone]);

  return (
    <Animated.View
      accessibilityRole="alert"
      style={[
        styles.successSheet,
        {
          opacity: sheetProgress,
          transform: [
            { translateY: sheetProgress.interpolate({ inputRange: [0, 1], outputRange: [320, 0] }) },
          ],
        },
      ]}
      pointerEvents="auto"
    >
      <View style={styles.successBadgeWrap} pointerEvents="none">
        <Animated.View
          style={{
            position: 'absolute',
            width: 100,
            height: 100,
            borderRadius: 50,
            borderWidth: 2.5,
            borderColor: colors.emberAccent,
            opacity: ringOpacity,
            transform: [
              { scale: ringScale.interpolate({ inputRange: [0, 1], outputRange: [0.5, 1.6] }) },
            ],
          }}
        />
        <Animated.View
          style={{
            width: 72,
            height: 72,
            borderRadius: 36,
            backgroundColor: colors.emberAccentStrong,
            alignItems: 'center',
            justifyContent: 'center',
            // Warm lantern halo instead of a hard signal glow — the signature-moment finish.
            boxShadow: `0 0 32px ${colors.emberAccent}66`,
            transform: [{ scale: badgeScale }],
          }}
        >
          <Text style={{ color: colors.ivoryFog, fontSize: 38, lineHeight: 42, fontWeight: '700' }}>
            ✓
          </Text>
        </Animated.View>
      </View>

      <View style={{ alignItems: 'center', gap: spacing.xs }}>
        <Text variant="headline" style={{ color: colors.textInk, textAlign: 'center' }}>
          ¡Felicitaciones!
        </Text>
        <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
          Encontraste el target.
        </Text>
      </View>
    </Animated.View>
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
          Alinea un código QR de target para escanearlo.
        </Text>
      </View>
    );
  }

  if (outcome.kind === 'submitting') {
    return (
      <View style={[styles.statusCard]} accessibilityRole="text">
        <ActivityIndicator size="small" color={colors.emberAccent} />
        <Text variant="body" style={{ color: colors.textInk }}>
          Registrando tu escaneo…
        </Text>
      </View>
    );
  }

  if (outcome.kind === 'accepted') {
    // Handled by the full-screen ScanSuccessBurst (which also auto-dismisses); no bottom card here.
    return null;
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
          <Button label="Reintentar" variant="primary" onPress={onScanAgain} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Cerrar" variant="secondary" onPress={onClose} />
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
  // Celebration sheet anchored to the bottom edge — rises into the lower portion of the screen, leaving the
  // camera/reticle visible above. Rounded top corners + a warm upward halo read it as a raised surface.
  successSheet: {
    ...({ position: 'absolute' } as const),
    left: 0,
    right: 0,
    bottom: 0,
    alignItems: 'center' as const,
    gap: spacing.lg,
    paddingHorizontal: spacing.xl,
    paddingTop: spacing.xl,
    paddingBottom: 48,
    backgroundColor: colors.paperSurface,
    borderTopLeftRadius: radii.panel,
    borderTopRightRadius: radii.panel,
    borderCurve: 'continuous' as const,
    borderTopWidth: 2,
    borderColor: colors.emberAccent,
    boxShadow: '0 -8px 30px rgba(53, 37, 27, 0.18)',
  },
  successBadgeWrap: {
    width: 100,
    height: 100,
    alignItems: 'center' as const,
    justifyContent: 'center' as const,
  },
};
