/**
 * Trivia pre-game countdown numeral (5→1). The backend's TriviaRoundStartedNotificationHandler emits a
 * short-window SessionTimerUpdated before a trivia round's first question; `useSessionTimer` routes it
 * to `pregameSecondsLeft`. This is the participant-side twin of the operator dashboard's countdown, so
 * the "get ready" beat is visible on mobile too instead of silently clobbering the session clock.
 */
import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { colors, spacing, typography } from '@/constants/theme';

export function SubstageCountdown({ secondsLeft }: { secondsLeft: number }) {
  return (
    <View
      testID="substage-countdown"
      accessibilityRole="text"
      accessibilityLabel={`Starting in ${secondsLeft}`}
      accessibilityLiveRegion="assertive"
      style={{ alignItems: 'center', gap: spacing.xs, paddingVertical: spacing.lg }}
    >
      <Text variant="label" muted>GET READY</Text>
      <Text
        style={{ ...typography.headline, fontSize: 48, lineHeight: 52, color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
      >
        {secondsLeft}
      </Text>
    </View>
  );
}
