import { ActivityIndicator, Pressable, View } from 'react-native';
import { colors, radii, spacing, typography } from '@/constants/theme';
import { Text } from '@/components/ui/text';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TriviaAnswerRejectionDisplay } from '@/lib/realtime/use-submit-answer';
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
        paddingTop: 52,
        paddingBottom: spacing.md,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md }}>
        {/* Left column: state badge, question number, timer */}
        <View style={{ flex: 1, gap: spacing.sm }}>
          {/* State badge */}
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
            <View
              style={{
                width: 8,
                height: 8,
                borderRadius: radii.pill,
                backgroundColor: dotColor,
              }}
            />
            <Text
              variant="label"
              style={{
                color: sessionState === 'Active' ? colors.signalSuccess : dotColor,
                textTransform: 'uppercase',
              }}
            >
              {sessionState}
            </Text>
          </View>

          {showTimer ? (
            <>
              <Text variant="label" muted>
                {`QUESTION ${questionSequenceOrder}`}
              </Text>
              <CountdownBar display={timerDisplay} />
            </>
          ) : null}
        </View>

        {/* Right column: score */}
        <View style={{ alignItems: 'flex-end', gap: 2 }}>
          <Text variant="label" muted style={{ textTransform: 'uppercase' }}>
            SCORE
          </Text>
          <Text
            variant="headline"
            style={{
              color: colors.emberAccentStrong,
              fontVariant: ['tabular-nums'],
              fontSize: 28,
              lineHeight: 32,
            }}
          >
            {score}
          </Text>
        </View>
      </View>
    </View>
  );
}

export function ActiveQuestionStage({
  question,
  sessionState,
  score,
  timerDisplay,
  selectedOptionSequenceOrder,
  isSubmitting,
  isLocked,
  isClosed,
  rejection,
  onSelectOption,
  onSubmit,
  onDismissRejection,
}: {
  question: ActiveQuestion;
  sessionState: string;
  score: number;
  timerDisplay: TimerDisplay;
  selectedOptionSequenceOrder?: number | null;
  isSubmitting?: boolean;
  isLocked?: boolean;
  // Display-only close lock (HU-M3): the question closed and controls settle until the next resolves.
  // Independent of the submit hook's `isLocked` and takes precedence for display.
  isClosed?: boolean;
  rejection?: TriviaAnswerRejectionDisplay | null;
  onSelectOption?: (sequenceOrder: number) => void;
  onSubmit?: () => void;
  onDismissRejection?: () => void;
}) {
  const hasSubmitProps = onSelectOption !== undefined;
  const selected = selectedOptionSequenceOrder ?? null;
  const locked = isLocked ?? false;
  const closed = isClosed ?? false;
  const submitting = isSubmitting ?? false;
  // Rows are interactive only when submit is wired and neither lock is in effect.
  const interactive = hasSubmitProps && !locked && !closed;

  return (
    <View style={{ alignSelf: 'stretch', backgroundColor: colors.ivoryFog }}>
      <StageHeader
        sessionState={sessionState}
        score={score}
        questionSequenceOrder={question.sequenceOrder}
        timerDisplay={timerDisplay}
      />

      <View style={{ padding: spacing.lg, borderBottomWidth: 1, borderBottomColor: colors.borderSoft }}>
        <Text selectable accessibilityRole="header" style={typography.display}>
          {question.prompt}
        </Text>
      </View>

      <View>
        {question.options.map((option, index) => {
          const isSelected = selected === index + 1;
          const rowBg = isSelected
            ? colors.emberAccentSoft
            : index % 2 === 0
              ? colors.ivoryFog
              : colors.warmMist;
          const pillBorderColor = isSelected ? colors.emberAccentStrong : colors.emberAccent;
          const pillFillBg = isSelected ? colors.emberAccent : 'transparent';
          const pillTextColor = isSelected ? colors.ivoryFog : colors.emberAccent;

          const hint = closed
            ? 'This question is closed.'
            : locked
              ? 'Answer already submitted.'
              : hasSubmitProps
                ? 'Select to submit as your team\'s answer.'
                : 'Answering is not yet available.';

          const Row = interactive ? Pressable : View;

          return (
            <Row
              key={`${index}-${option}`}
              {...(interactive
                ? {
                    accessibilityRole: 'radio' as const,
                    accessibilityState: { selected: isSelected, disabled: false },
                    accessibilityHint: hint,
                    onPress: () => onSelectOption!(index + 1),
                  }
                : {
                    accessibilityRole: 'text' as const,
                    accessibilityState: { disabled: locked || closed },
                    accessibilityHint: hint,
                  })}
              style={{
                backgroundColor: rowBg,
                borderBottomWidth: 1,
                borderBottomColor: colors.borderSoft,
                flexDirection: 'row' as const,
                alignItems: 'center' as const,
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
                  borderColor: pillBorderColor,
                  backgroundColor: pillFillBg,
                  alignItems: 'center' as const,
                  justifyContent: 'center' as const,
                }}
              >
                <Text variant="label" style={{ color: pillTextColor }}>
                  {String.fromCharCode(65 + index)}
                </Text>
              </View>
              <Text
                style={{ flex: 1, ...typography.headline, fontWeight: '400' as const }}
              >
                {option}
              </Text>
            </Row>
          );
        })}
      </View>

      {hasSubmitProps || closed ? (
        <View style={{ padding: spacing.lg, gap: spacing.md }}>
          {closed ? (
            // Close affordance — neutral/critical tone, distinct from the green "Answer submitted" chip.
            <View
              accessibilityRole="text"
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                gap: spacing.xs,
                paddingVertical: spacing.sm,
              }}
            >
              <View
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: radii.pill,
                  backgroundColor: colors.signalCritical,
                }}
              />
              <Text variant="label" style={{ color: colors.textMuted }}>
                Question closed — waiting for the next
              </Text>
            </View>
          ) : locked ? (
            <View
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                gap: spacing.xs,
                paddingVertical: spacing.sm,
              }}
            >
              <View
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: radii.pill,
                  backgroundColor: colors.signalSuccess,
                }}
              />
              <Text variant="label" style={{ color: colors.signalSuccess }}>
                Answer submitted
              </Text>
            </View>
          ) : (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Submit answer"
              disabled={selected === null || submitting || locked}
              onPress={onSubmit}
              style={({ pressed }) => ({
                backgroundColor:
                  selected === null || submitting || locked
                    ? colors.borderSoft
                    : pressed
                      ? colors.emberAccent
                      : colors.emberAccentStrong,
                borderRadius: radii.control,
                borderCurve: 'continuous',
                paddingVertical: 12,
                paddingHorizontal: 16,
                alignItems: 'center' as const,
                justifyContent: 'center' as const,
                flexDirection: 'row' as const,
                gap: spacing.xs,
                minHeight: 44,
                opacity: selected === null ? 0.45 : 1,
              })}
            >
              {submitting ? (
                <ActivityIndicator size="small" color={colors.ivoryFog} />
              ) : (
                <Text
                  variant="label"
                  style={{ color: colors.ivoryFog }}
                >
                  Submit answer
                </Text>
              )}
            </Pressable>
          )}

          {rejection && !closed ? (
            <View
              accessibilityRole="alert"
              style={{
                backgroundColor: colors.signalWarning + '1A',
                borderWidth: 1,
                borderColor: colors.signalWarning,
                borderRadius: radii.control,
                borderCurve: 'continuous',
                padding: spacing.md,
                gap: spacing.xs,
              }}
            >
              <View
                style={{
                  flexDirection: 'row',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <Text variant="title" style={{ color: colors.signalWarning }}>
                  {rejection.title}
                </Text>
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel="Dismiss"
                  onPress={onDismissRejection}
                  hitSlop={spacing.xs}
                  style={{
                    width: 24,
                    height: 24,
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <Text variant="label" style={{ color: colors.textMuted }}>
                    ✕
                  </Text>
                </Pressable>
              </View>
              <Text variant="body" muted>
                {rejection.message}
              </Text>
            </View>
          ) : null}
        </View>
      ) : null}
    </View>
  );
}

export { StageHeader as ActiveQuestionStageHeader };
