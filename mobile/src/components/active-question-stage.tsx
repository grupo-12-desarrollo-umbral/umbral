import { ActivityIndicator, Pressable, View } from 'react-native';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';
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

// Participant-facing Spanish labels for the backend session-state enum. Falls back to the raw value
// so an unmapped/newer state still renders rather than showing blank.
const STATE_LABELS: Record<string, string> = {
  Scheduled: 'Programada',
  Preparing: 'Preparando',
  Active: 'Activa',
  Paused: 'En pausa',
  Finished: 'Finalizada',
  Cancelled: 'Cancelada',
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
        accessibilityLabel={`Temporizador de pregunta: ${display.label}`}
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

function MissionDeadlineLine({ display }: { display: TimerDisplay }) {
  const timeColor =
    display.tone === 'paused'
      ? colors.signalWarning
      : display.tone === 'expired'
        ? colors.signalCritical
        : colors.textMuted;

  return (
    <View
      accessibilityRole="text"
      accessibilityLabel={`Temporizador de misión: ${display.label}`}
      style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}
    >
      <Text variant="label" muted>
        MISIÓN
      </Text>
      <Text variant="mono" style={{ color: timeColor, fontVariant: ['tabular-nums'] }}>
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
  missionDisplay,
  topInset = spacing.lg,
}: {
  sessionState: string;
  score: number;
  questionSequenceOrder?: number;
  timerDisplay?: TimerDisplay;
  // The whole-mission deadline, shown beneath the per-question countdown so the two clocks read as
  // distinct. Null/absent before the deadline is seeded or on an older backend.
  missionDisplay?: TimerDisplay | null;
  // Top padding above the state badge. Defaults to a tight inset because on the trivia surface this
  // header is embedded below the mission card, not at the top of the screen — a status-bar-sized inset
  // there just reads as a floating gap. Pass a larger value when the header owns the top of the screen.
  topInset?: number;
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
        paddingTop: topInset,
        paddingBottom: spacing.md,
      }}
    >
      {/* Row 1: state + question on one line, compact score inline on the right. Collapsing these
          onto a single row keeps the player's eye on the question rather than a wall of metadata. */}
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs, flex: 1 }}>
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
            {STATE_LABELS[sessionState] ?? sessionState}
          </Text>
          {showTimer ? (
            <Text variant="label" muted style={{ textTransform: 'uppercase' }}>
              {`· Pregunta ${questionSequenceOrder}`}
            </Text>
          ) : null}
        </View>

        {/* Score — compact and inline, no longer a giant stacked block competing with the prompt. */}
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
          <Text variant="label" muted style={{ textTransform: 'uppercase' }}>
            PUNTUACIÓN
          </Text>
          <Text
            variant="headline"
            style={{
              color: colors.emberAccentStrong,
              fontVariant: ['tabular-nums'],
              fontSize: 22,
              lineHeight: 24,
            }}
          >
            {score}
          </Text>
        </View>
      </View>

      {/* Row 2: the per-question countdown stays prominent — it's the clock the player acts on. */}
      {showTimer ? (
        <View style={{ marginTop: spacing.sm }}>
          <CountdownBar display={timerDisplay} />
        </View>
      ) : null}

      {/* Row 3: the whole-mission deadline, demoted to one quiet line so it stops reading as a second
          equally-urgent clock next to the question countdown. */}
      {missionDisplay ? (
        <View style={{ marginTop: spacing.xs }}>
          <MissionDeadlineLine display={missionDisplay} />
        </View>
      ) : null}
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
          Cargando resultado…
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
          {isCorrect ? 'Correcta' : isNoAnswer ? 'Sin respuesta' : 'Incorrecta'}
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
  missionDisplay,
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
  // The whole-mission deadline, rendered under the question countdown. Absent on older callers.
  missionDisplay?: TimerDisplay | null;
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
        missionDisplay={missionDisplay}
      />

      <View style={{ padding: spacing.lg, borderBottomWidth: 1, borderBottomColor: colors.borderSoft }}>
        <Text selectable accessibilityRole="header" style={typography.display}>
          {question.prompt}
        </Text>
      </View>

      {/* Answers are cards, not table rows — spaced, rounded and bordered to match the treasure-hunt
          clue cards, so trivia and treasure hunt read as one product and each option is an obvious
          tap target rather than a strip in a list. */}
      <View
        testID={correctOptionSequenceOrder !== undefined ? 'question-reveal' : undefined}
        style={{ paddingHorizontal: spacing.lg, paddingTop: spacing.md, gap: spacing.sm }}
      >
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
          let cardBorderColor: string;
          // Emphasised states (selection + reveal outcomes) carry a slightly heavier border so the
          // active card reads at a glance; a plain option keeps the hairline card border.
          let cardBorderWidth: number;

          if (isCorrectOption) {
            rowBg = colors.signalSuccess + '22';
            pillBorderColor = colors.signalSuccess;
            pillFillBg = colors.signalSuccess;
            pillTextColor = colors.ivoryFog;
            textColor = colors.signalSuccess;
            cardBorderColor = colors.signalSuccess;
            cardBorderWidth = 1.5;
          } else if (isUserAnswer) {
            rowBg = colors.signalCritical + '12';
            pillBorderColor = colors.signalCritical;
            pillFillBg = colors.signalCritical;
            pillTextColor = colors.ivoryFog;
            textColor = colors.signalCritical;
            cardBorderColor = colors.signalCritical;
            cardBorderWidth = 1.5;
          } else if (isSelected) {
            rowBg = colors.emberAccentSoft;
            pillBorderColor = colors.emberAccentStrong;
            pillFillBg = colors.emberAccent;
            pillTextColor = colors.ivoryFog;
            textColor = colors.textInk;
            cardBorderColor = colors.emberAccentStrong;
            cardBorderWidth = 1.5;
          } else {
            rowBg = colors.raisedSurface;
            pillBorderColor = colors.emberAccent;
            pillFillBg = 'transparent';
            pillTextColor = colors.emberAccent;
            textColor = colors.textInk;
            cardBorderColor = colors.borderSoft;
            cardBorderWidth = 1;
          }

          const chip = isCorrectOption
            ? 'CORRECTA'
            : isUserAnswer
              ? 'TU RESPUESTA'
              : undefined;

          const hint = closed
            ? isCorrectOption
              ? 'Esta es la respuesta correcta.'
              : isUserAnswer
                ? 'Esta fue la respuesta de tu equipo.'
                : 'Esta pregunta está cerrada.'
            : locked
              ? 'Respuesta ya enviada.'
              : hasSubmitProps
                ? 'Selecciona para enviar como la respuesta de tu equipo.'
                : 'Responder aún no está disponible.';

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
                borderWidth: cardBorderWidth,
                borderColor: cardBorderColor,
                borderRadius: radii.card,
                borderCurve: 'continuous' as const,
                boxShadow: shadows.card,
                flexDirection: 'row' as const,
                alignItems: 'center' as const,
                gap: spacing.md,
                paddingHorizontal: spacing.md,
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
                        ¿POR QUÉ?
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
                  Pregunta cerrada — esperando la siguiente
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
                Respuesta enviada
              </Text>
            </View>
          ) : (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Enviar respuesta"
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
                  Enviar respuesta
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
                  accessibilityLabel="Descartar"
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
