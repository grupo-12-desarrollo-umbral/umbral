import { useCallback, useEffect } from 'react';
import {
  AccessibilityInfo,
  Animated,
  Platform,
  Pressable,
  useAnimatedValue,
  View,
} from 'react-native';
import { Text } from '@/components/ui/text';
import { colors, radii, shadows, spacing } from '@/constants/theme';
import type { PenaltyToastData } from '@/lib/realtime/use-penalty-toast';

/**
 * Transient toast announcing an operator-applied penalty. Driven by the ScoringHub `PenaltyApplied` push
 * (see `usePenaltyToast`), so it fires for every penalty — including one whose resulting team total clamps
 * to zero — and shows the true deduction magnitude rather than a value inferred from the ranking snapshot.
 *
 * Mount-to-dismiss is one lifecycle: the enter animation and the 4s auto-dismiss timer run in a mount-only
 * effect. The caller keys the element by `penalty.id` so a fresh penalty remounts it and replays both.
 */
export function PenaltyToast({
  penalty,
  onDismiss,
}: {
  penalty: PenaltyToastData;
  onDismiss: () => void;
}) {
  const translateY = useAnimatedValue(-24);
  const opacity = useAnimatedValue(0);

  const dismiss = useCallback(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: -24, duration: 180, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 0, duration: 180, useNativeDriver: true }),
    ]).start(({ finished }) => {
      if (finished) onDismiss();
    });
  }, [translateY, opacity, onDismiss]);

  useEffect(() => {
    if (Platform.OS !== 'web') {
      AccessibilityInfo.announceForAccessibility(
        `Penalización aplicada. La puntuación bajó ${penalty.magnitude} puntos.`,
      );
    }
    Animated.parallel([
      Animated.timing(translateY, { toValue: 0, duration: 220, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 1, duration: 220, useNativeDriver: true }),
    ]).start();
    const t = setTimeout(dismiss, 4000);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Animated.View
      accessibilityLiveRegion="polite"
      pointerEvents="box-none"
      style={{
        position: 'absolute',
        top: 56,
        left: spacing.lg,
        right: spacing.lg,
        opacity,
        transform: [{ translateY }],
        zIndex: 10,
      }}
    >
      <Pressable
        testID="penalty-toast"
        accessibilityRole="alert"
        accessibilityLabel={`Penalización aplicada. La puntuación bajó ${penalty.magnitude} puntos.`}
        onPress={dismiss}
        style={{
          backgroundColor: colors.panelSurface,
          borderColor: colors.signalCritical,
          borderWidth: 1,
          borderRadius: radii.card,
          borderCurve: 'continuous',
          boxShadow: shadows.card,
          padding: spacing.md,
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.sm,
        }}
      >
        <View
          style={{
            width: 8,
            height: 8,
            borderRadius: 4,
            backgroundColor: colors.signalCritical,
          }}
        />
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="label" style={{ color: colors.signalCritical }}>
            PENALIZACIÓN APLICADA
          </Text>
          <Text variant="body">
            La puntuación bajó {penalty.magnitude} pts
          </Text>
          {penalty.reason ? (
            <Text variant="label" muted numberOfLines={2}>
              {penalty.reason}
            </Text>
          ) : null}
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Descartar"
          onPress={dismiss}
          hitSlop={spacing.xs}
          style={{ width: 24, height: 24, alignItems: 'center', justifyContent: 'center' }}
        >
          <Text variant="label" muted>
            ✕
          </Text>
        </Pressable>
      </Pressable>
    </Animated.View>
  );
}
