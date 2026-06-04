import { View } from 'react-native';
import { colors, radii, spacing } from '@/constants/theme';
import { Text } from '@/components/ui/text';
import type { TimerDisplay, TimerTone } from '@/lib/realtime/timer-types';

const CHIP_LABELS: Record<TimerTone, string> = {
  running: 'Running',
  paused: 'Paused',
  expired: 'Expired',
  unavailable: 'Unavailable',
};

const FILL_COLORS: Record<TimerTone, string> = {
  running: colors.emberAccent,
  paused: colors.signalWarning,
  expired: colors.signalCritical,
  unavailable: colors.borderSoft,
};

export function SessionTimerBar({ display }: { display: TimerDisplay }) {
  const { label, pct, tone } = display;
  const clampedPct = Math.min(100, Math.max(0, pct));
  const fillColor = FILL_COLORS[tone];
  const chipLabel = CHIP_LABELS[tone];
  const isTerminal = tone === 'expired' || tone === 'unavailable';

  return (
    <View style={{ gap: spacing.xs }}>
      <View
        accessibilityRole="progressbar"
        accessibilityValue={{ min: 0, max: 100, now: clampedPct }}
        accessibilityLabel={`Session timer: ${label}`}
        style={{
          backgroundColor: colors.raisedSurface,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          borderWidth: 1,
          borderColor: colors.borderSoft,
          height: 10,
          overflow: 'hidden',
        }}
      >
        <View
          style={{
            width: `${clampedPct}%`,
            height: '100%',
            backgroundColor: fillColor,
            opacity: isTerminal ? 0.5 : 1,
          }}
        />
      </View>

      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <Text
          variant="mono"
          style={{
            color: isTerminal ? colors.textMuted : colors.textInk,
            fontVariant: ['tabular-nums'],
          }}
        >
          {label}
        </Text>
        <View
          style={{
            backgroundColor: colors.raisedSurface,
            borderRadius: radii.pill,
            borderWidth: 1,
            borderColor: colors.borderSoft,
            paddingHorizontal: spacing.sm,
            paddingVertical: spacing.one,
          }}
        >
          <Text
            variant="label"
            style={{ color: isTerminal ? colors.textMuted : fillColor }}
          >
            {chipLabel}
          </Text>
        </View>
      </View>
    </View>
  );
}
