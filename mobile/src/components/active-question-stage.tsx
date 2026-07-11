import { View } from 'react-native';
import { colors, radii, spacing, typography } from '@/constants/theme';
import { Text } from '@/components/ui/text';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TimerDisplay, TimerTone } from '@/lib/realtime/timer-types';

const STATE_DOT_COLORS: Record<string, string> = {
  Scheduled: colors.textMuted,
  Preparing: colors.signalWarning,
  Active: colors.signalSuccess,
  Paused: colors.signalWarning,
  Finished: colors.textMuted,
  Cancelled: colors.signalCritical,
};

const TIMER_FILL_COLORS: Record<TimerTone, string> = {
  running: colors.emberAccent,
  paused: colors.signalWarning,
  expired: colors.signalCritical,
  unavailable: colors.borderSoft,
};

export function CountdownBar({ display }: { display: TimerDisplay }) {
  const clampedPct = Math.min(100, Math.max(0, display.pct));
  const isTerminal = display.tone === 'expired' || display.tone === 'unavailable';

  return (
    <View style={{ gap: spacing.xs }}>
      <View
        accessibilityRole="progressbar"
        accessibilityValue={{ min: 0, max: 100, now: clampedPct }}
        accessibilityLabel={`Question timer: ${display.label}`}
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
            backgroundColor: TIMER_FILL_COLORS[display.tone],
            opacity: isTerminal ? 0.5 : 1,
          }}
        />
      </View>
      <Text
        variant="mono"
        style={{
          color: isTerminal ? colors.textMuted : colors.textInk,
          fontVariant: ['tabular-nums'],
          textAlign: 'right',
        }}
      >
        {display.label}
      </Text>
    </View>
  );
}

function StageHeader({
  sessionState,
  score,
  questionSequenceOrder,
  timerDisplay,
}: {
  sessionState: string;
  score: number;
  questionSequenceOrder?: number;
  timerDisplay?: TimerDisplay;
}) {
  const dotColor = STATE_DOT_COLORS[sessionState] ?? colors.textMuted;
  const showTimer = questionSequenceOrder !== undefined && timerDisplay !== undefined;

  return (
    <View
      style={{
        backgroundColor: colors.panelSurface,
        borderBottomWidth: 1,
        borderBottomColor: colors.borderSoft,
        paddingHorizontal: spacing.lg,
        paddingVertical: spacing.md,
        gap: spacing.sm,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
          <View
            style={{
              width: 8,
              height: 8,
              borderRadius: radii.pill,
              backgroundColor: dotColor,
            }}
          />
          <Text variant="label">{sessionState}</Text>
        </View>

        <View style={{ alignItems: 'flex-end' }}>
          <Text variant="label" muted>
            SCORE
          </Text>
          <Text
            variant="headline"
            style={{ color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
          >
            {score}
          </Text>
        </View>
      </View>

      {showTimer ? (
        <View style={{ gap: spacing.xs }}>
          <Text variant="label" muted>
            {`QUESTION ${questionSequenceOrder}`}
          </Text>
          <CountdownBar display={timerDisplay} />
        </View>
      ) : null}
    </View>
  );
}

export function ActiveQuestionStage({
  question,
  sessionState,
  score,
  timerDisplay,
}: {
  question: ActiveQuestion;
  sessionState: string;
  score: number;
  timerDisplay: TimerDisplay;
}) {
  return (
    <View style={{ alignSelf: 'stretch', backgroundColor: colors.ivoryFog }}>
      <StageHeader
        sessionState={sessionState}
        score={score}
        questionSequenceOrder={question.sequenceOrder}
        timerDisplay={timerDisplay}
      />

      <View style={{ padding: spacing.lg, backgroundColor: colors.paperSurface }}>
        <Text selectable accessibilityRole="header" style={typography.display}>
          {question.prompt}
        </Text>
      </View>

      <View>
        {question.options.map((option, index) => (
          <View
            key={`${index}-${option}`}
            style={{
              backgroundColor: index % 2 === 0 ? colors.ivoryFog : colors.warmMist,
              borderBottomWidth: 1,
              borderBottomColor: colors.borderSoft,
              flexDirection: 'row',
              alignItems: 'center',
              gap: spacing.md,
              paddingHorizontal: spacing.lg,
              paddingVertical: spacing.md,
            }}
          >
            <View
              style={{
                width: 34,
                height: 34,
                borderRadius: radii.pill,
                borderWidth: 1,
                borderColor: colors.emberAccent,
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <Text variant="label" accent>
                {String.fromCharCode(65 + index)}
              </Text>
            </View>
            <Text
              accessibilityRole="text"
              accessibilityHint="Answering is not yet available."
              style={{ flex: 1, ...typography.headline, fontWeight: '400' }}
            >
              {option}
            </Text>
          </View>
        ))}
      </View>
    </View>
  );
}

export { StageHeader as ActiveQuestionStageHeader };
