import { useCallback, useEffect, useRef, useState } from 'react';
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

export type ScoreDrop = {
  previous: number;
  current: number;
  delta: number;
};

/**
 * Detects when a score drops between board updates. Returns the drop details
 * so the caller can render a toast, then clears it once consumed.
 *
 * Rules:
 * - No drop on initial mount (previous is undefined).
 * - No drop when the score rises (grants, target resolution).
 * - No drop when the score is unchanged (reconnects with same score).
 */
export function useScoreDrop(
  currentScore: number | undefined,
): { drop: ScoreDrop | null; clear: () => void } {
  const prevRef = useRef<number | undefined>(undefined);
  const [drop, setDrop] = useState<ScoreDrop | null>(null);

  useEffect(() => {
    if (currentScore === undefined) {
      prevRef.current = undefined;
      return;
    }

    const previous = prevRef.current;
    prevRef.current = currentScore;

    if (previous === undefined) return;
    if (currentScore >= previous) return;

    setDrop({ previous, current: currentScore, delta: previous - currentScore });
  }, [currentScore]);

  const clear = useCallback(() => setDrop(null), []);

  return { drop, clear };
}

function ScoreDropToast({ drop, onDismiss }: { drop: ScoreDrop; onDismiss: () => void }) {
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
        `Penalty applied. Score dropped by ${drop.delta} points.`,
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
        testID="score-drop-toast"
        accessibilityRole="alert"
        accessibilityLabel={`Penalty applied. Score dropped by ${drop.delta} points.`}
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
            PENALTY APPLIED
          </Text>
          <Text variant="body">
            Score dropped by {drop.delta} pts
          </Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Dismiss"
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

export { ScoreDropToast };
