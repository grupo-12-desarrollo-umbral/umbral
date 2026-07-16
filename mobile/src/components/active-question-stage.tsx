import { ActivityIndicator, Pressable, View } from 'react-native';
import { colors, radii, spacing, typography } from '@/constants/theme';
import { Card } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TriviaAnswerRejectionDisplay } from '@/lib/realtime/use-submit-answer';
import type { TimerDisplay, TimerTone } from '@/lib/realtime/timer-types';
import type { TriviaTeamQuestionResultDto } from '@/lib/realtime/trivia-types';

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

function RevealOutcome({ teamResult }: { teamResult: TriviaTeamQuestionResultDto | null | undefined }) {
  if (!teamResult) {
    // Loading state — show a placeholder so layout doesn't jump.
    return (
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingHorizontal: spacing.lg,
          paddingVertical: spacing.md,
        }}
      >
        <ActivityIndicator size="small" color={colors.textMuted} />
        <Text variant="body" muted>
          Loading result…
        </Text>
      </View>
    );
  }

  const isCorrect = teamResult.isCorrect === true;
  const isNoAnswer = teamResult.selectedOptionSequenceOrder === null;
  const outcomeColor = isCorrect
    ? colors.signalSuccess
    : isNoAnswer
      ? colors.textMuted
      : colors.signalCritical;

  const outcomeTestId = isCorrect
    ? 'reveal-team-outcome-correct'
    : isNoAnswer
      ? 'reveal-team-outcome-no-answer'
      : 'reveal-team-outcome-incorrect';

  return (
    <View
      style={{
        paddingHorizontal: spacing.lg,
        paddingVertical: spacing.md,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
      }}
      testID={outcomeTestId}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <View
          style={{
            width: 28,
            height: 28,
            borderRadius: 14,
            backgroundColor: outcomeColor,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text variant="label" style={{ color: colors.ivoryFog, fontSize: 14 }}>
            {isCorrect ? '✓' : isNoAnswer ? '—' : '✕'}
          </Text>
        </View>
        <Text variant="title" style={{ color: outcomeColor }}>
          {isCorrect ? 'Correct' : isNoAnswer ? 'No answer' : 'Incorrect'}
        </Text>
      </View>
      <Text
        variant="headline"
        style={{ color: outcomeColor, fontVariant: ['tabular-nums'] }}
        testID="reveal-points"
      >
        {teamResult.scoreValue != null && teamResult.scoreValue >= 0
          ? `+${teamResult.scoreValue}`
          : `${teamResult.scoreValue}`}
      </Text>
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
  // Reveal data (HU-M4). When present, the closed branch renders the result reveal UI.
  correctOptionSequenceOrder,
  explanation,
  teamResult,
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
  correctOptionSequenceOrder?: number;
  explanation?: string | null;
  teamResult?: TriviaTeamQuestionResultDto | null;
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

      <View testID={correctOptionSequenceOrder !== undefined ? 'question-reveal' : undefined}>
        {question.options.map((option, index) => {
          const seq = index + 1;
          const inReveal = correctOptionSequenceOrder !== undefined;
          const isCorrectOption = inReveal && seq === correctOptionSequenceOrder;
          // Mark the team's answer red in the reveal. Prefer this participant's own submitted pick
          // (`selected`) so the red shows immediately and even when the `my-result` read is slow or
          // returns no team answer; fall back to the authoritative team answer (e.g. after a
          // reconnect that lost the local pick). Never marks the correct option red — that stays green.
          const revealSelectedSeq = inReveal
            ? (selected ?? teamResult?.selectedOptionSequenceOrder ?? null)
            : null;
          const isUserAnswer =
            inReveal && !isCorrectOption && revealSelectedSeq != null && seq === revealSelectedSeq;
          const isSelected = !inReveal && selected === seq;

          let rowBg: string;
          let pillBorderColor: string;
          let pillFillBg: string;
          let pillTextColor: string;
          let textColor: string;
          let borderBottomColor: string;

          if (isCorrectOption) {
            rowBg = colors.signalSuccess + '22';
            pillBorderColor = colors.signalSuccess;
            pillFillBg = colors.signalSuccess;
            pillTextColor = colors.ivoryFog;
            textColor = colors.signalSuccess;
            borderBottomColor = colors.signalSuccess;
          } else if (isUserAnswer) {
            rowBg = colors.signalCritical + '12';
            pillBorderColor = colors.signalCritical;
            pillFillBg = colors.signalCritical;
            pillTextColor = colors.ivoryFog;
            textColor = colors.signalCritical;
            borderBottomColor = colors.borderSoft;
          } else if (isSelected) {
            rowBg = colors.emberAccentSoft;
            pillBorderColor = colors.emberAccentStrong;
            pillFillBg = colors.emberAccent;
            pillTextColor = colors.ivoryFog;
            textColor = colors.textInk;
            borderBottomColor = colors.borderSoft;
          } else {
            rowBg = index % 2 === 0 ? colors.ivoryFog : colors.warmMist;
            pillBorderColor = colors.emberAccent;
            pillFillBg = 'transparent';
            pillTextColor = colors.emberAccent;
            textColor = colors.textInk;
            borderBottomColor = colors.borderSoft;
          }

          const chip = isCorrectOption
            ? 'CORRECT'
            : isUserAnswer
              ? 'YOUR ANSWER'
              : undefined;

          const hint = closed
            ? isCorrectOption
              ? 'This is the correct answer.'
              : isUserAnswer
                ? 'This was your team\'s answer.'
                : 'This question is closed.'
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
                borderBottomColor: borderBottomColor,
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
                style={{ flex: 1, ...typography.headline, fontWeight: '400' as const, color: textColor }}
                testID={isCorrectOption ? 'reveal-correct-option' : undefined}
              >
                {option}
              </Text>
              {chip ? (
                <View
                  style={{
                    paddingHorizontal: spacing.sm,
                    paddingVertical: 2,
                    borderRadius: radii.control,
                    borderCurve: 'continuous',
                    backgroundColor: (isCorrectOption ? colors.signalSuccess : colors.signalCritical) + '1A',
                    borderWidth: 1,
                    borderColor: isCorrectOption ? colors.signalSuccess : colors.signalCritical,
                  }}
                >
                  <Text
                    variant="label"
                    style={{ color: isCorrectOption ? colors.signalSuccess : colors.signalCritical }}
                  >
                    {chip}
                  </Text>
                </View>
              ) : null}
            </Row>
          );
        })}
      </View>

      {hasSubmitProps || closed ? (
        <View style={{ padding: spacing.lg, gap: spacing.md }}>
          {closed ? (
            correctOptionSequenceOrder !== undefined ? (
              // HU-M4 result reveal — option-centric inline layout (Variant B winner).
              <View style={{ gap: spacing.md }}>
                {/* Compact team outcome */}
                <RevealOutcome teamResult={teamResult} />

                {/* Explanation */}
                {explanation ? (
                  <Card
                    style={{
                      backgroundColor: colors.paperSurface,
                      borderColor: colors.borderSoft,
                    }}
                    testID="reveal-explanation"
                  >
                    <View style={{ gap: spacing.xs }}>
                      <Text variant="label" muted>
                        WHY
                      </Text>
                      <Text variant="body" muted>
                        {explanation}
                      </Text>
                    </View>
                  </Card>
                ) : null}
              </View>
            ) : (
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
            )
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
