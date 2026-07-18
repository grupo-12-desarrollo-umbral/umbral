/**
 * Trivia pre-game countdown (5→1), shown as a full-screen "GET READY" overlay. The backend's
 * TriviaRoundStartedNotificationHandler emits a short-window SessionTimerUpdated before a trivia round's
 * first question; `useSessionTimer` routes it to `pregameSecondsLeft`. This is the participant-side twin
 * of the operator dashboard's countdown, so the "get ready" beat is a full moment on mobile too instead
 * of silently clobbering the session clock.
 *
 * The numbers arrive as discrete server ticks (one SignalR event per number). To keep the full-screen
 * beat from visibly skipping when a tick is delayed or dropped, a local 1s interval interpolates the
 * count downward while each server tick resyncs the value (the server stays authoritative). The overlay
 * unmounts when the parent clears `pregameSecondsLeft` (real first-question tick, reconnect snapshot, or
 * the view flipping to active), so the interpolation floors at 1 and simply waits to be unmounted.
 */
import { useEffect, useState } from 'react';
import { Animated, Modal, useAnimatedValue, View } from 'react-native';
import { Text } from '@/components/ui/text';
import { colors, spacing } from '@/constants/theme';

export function SubstageCountdown({ secondsLeft }: { secondsLeft: number }) {
  // `display` is the interpolated value; it seeds from the server tick and each new server tick resyncs.
  const [display, setDisplay] = useState(secondsLeft);
  const [syncedTo, setSyncedTo] = useState(secondsLeft);
  const scale = useAnimatedValue(1);
  const opacity = useAnimatedValue(1);

  // Resync to each server tick during render — the backend is authoritative for the count. This is the
  // React "adjust state when a prop changes" pattern, so a dropped tick still lets interpolation continue.
  if (secondsLeft !== syncedTo) {
    setSyncedTo(secondsLeft);
    setDisplay(secondsLeft);
  }

  // Client interpolation: step down once per second so the count never stalls or skips between ticks.
  // Floor at 1; the parent unmounts this overlay when the real question window arrives.
  useEffect(() => {
    const id = setInterval(() => {
      setDisplay((current) => (current > 1 ? current - 1 : current));
    }, 1000);
    return () => clearInterval(id);
  }, []);

  // Pop each number as it changes: quick scale-in from a slightly larger, faded frame.
  useEffect(() => {
    scale.setValue(1.35);
    opacity.setValue(0);
    Animated.parallel([
      Animated.spring(scale, { toValue: 1, friction: 5, tension: 140, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 1, duration: 180, useNativeDriver: true }),
    ]).start();
  }, [display, scale, opacity]);

  return (
    <Modal visible transparent statusBarTranslucent animationType="fade" onRequestClose={() => {}}>
      <View
        testID="substage-countdown"
        accessibilityRole="text"
        accessibilityLabel={`Comienza en ${display}`}
        accessibilityLiveRegion="assertive"
        style={{
          flex: 1,
          alignItems: 'center',
          justifyContent: 'center',
          gap: spacing.md,
          backgroundColor: colors.emberAccentStrong,
        }}
      >
        <Text
          variant="label"
          style={{ color: colors.ivoryFog, opacity: 0.85, letterSpacing: 4, fontSize: 15 }}
        >
          PREPÁRATE
        </Text>
        <Animated.Text
          style={{
            color: colors.ivoryFog,
            fontSize: 140,
            lineHeight: 152,
            fontWeight: '700',
            fontVariant: ['tabular-nums'],
            opacity,
            transform: [{ scale }],
          }}
        >
          {display}
        </Animated.Text>
      </View>
    </Modal>
  );
}
