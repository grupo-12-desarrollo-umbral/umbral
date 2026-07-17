/**
 * THROWAWAY UI PROTOTYPE — HU-M4 result reveal after question close.
 *
 * Winning variant: B — Option-centric (no banner; correct option becomes a hero
 * row with green check, wrong options muted; team result chip is compact and
 * inline; explanation is subtle).
 *
 * Delete this route once the winning variant is folded into active-question-stage.tsx.
 */
import { useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import { colors, radii, spacing, typography } from '@/constants/theme';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';

// ------------------------------------------------------------------
// Stub data — same question as active-question-prototype
// ------------------------------------------------------------------

const STUB_QUESTION: ActiveQuestion = {
  questionIndex: 0,
  sequenceOrder: 3,
  prompt: 'Which street borders the north colonnade of the Plaza Mayor?',
  options: ['Calle del Sol', 'Avenida Mayor', 'Paseo del Prado', 'Rambla Vella'],
  timeLimitSeconds: 60,
  triviaSubstageSnapshotId: 'stub',
};

const CORRECT_INDEX = 1; // Avenida Mayor

const STUB_EXPLANATION =
  'The Plaza Mayor was built by Philip III in 1619. The north colonnade is bordered by Calle de Ciudad Rodrigo, but the closest named street on the standard map is Avenida Mayor.';

// ------------------------------------------------------------------
// Reveal DTO stub (mirrors TriviaTeamQuestionResultDto from hu-m4 plan)
// ------------------------------------------------------------------

type RevealState =
  | { kind: 'correct'; selectedOptionSequenceOrder: number; points: number }
  | { kind: 'incorrect'; selectedOptionSequenceOrder: number; points: number }
  | { kind: 'no-answer'; selectedOptionSequenceOrder: null; points: number };

// ------------------------------------------------------------------
// Shared helpers
// ------------------------------------------------------------------

function RevealControls({
  reveal,
  onSetReveal,
}: {
  reveal: RevealState;
  onSetReveal: (r: RevealState) => void;
}) {
  return (
    <View style={{ flexDirection: 'row', gap: spacing.xs }}>
      <ControlPill
        label="Correct (+20)"
        active={reveal.kind === 'correct'}
        onPress={() => onSetReveal({ kind: 'correct', selectedOptionSequenceOrder: CORRECT_INDEX, points: 20 })}
      />
      <ControlPill
        label="Wrong (-10)"
        active={reveal.kind === 'incorrect'}
        onPress={() => onSetReveal({ kind: 'incorrect', selectedOptionSequenceOrder: 2, points: -10 })}
      />
      <ControlPill
        label="No answer"
        active={reveal.kind === 'no-answer'}
        onPress={() => onSetReveal({ kind: 'no-answer', selectedOptionSequenceOrder: null, points: 0 })}
      />
    </View>
  );
}

function ControlPill({
  label,
  onPress,
  active,
}: {
  label: string;
  onPress: () => void;
  active?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      style={{
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.xs,
        borderRadius: radii.control,
        borderCurve: 'continuous',
        borderWidth: 1,
        backgroundColor: active ? colors.emberAccentSoft : colors.raisedSurface,
        borderColor: active ? colors.emberAccent : colors.borderSoft,
      }}
    >
      <Text
        variant="label"
        style={{ color: active ? colors.emberAccentStrong : colors.textMuted }}
      >
        {label}
      </Text>
    </Pressable>
  );
}

function OptionRow({
  index,
  option,
  isCorrectOption,
  isSelected,
  chip,
}: {
  index: number;
  option: string;
  isCorrectOption: boolean;
  isSelected: boolean;
  chip?: string;
}) {
  const rowBg = isCorrectOption
    ? colors.signalSuccess + '22'
    : isSelected
      ? colors.signalCritical + '12'
      : index % 2 === 0
        ? colors.ivoryFog
        : colors.warmMist;
  const pillBorderColor = isCorrectOption
    ? colors.signalSuccess
    : isSelected
      ? colors.signalCritical
      : colors.borderSoft;
  const pillFillBg = isCorrectOption
    ? colors.signalSuccess
    : isSelected
      ? colors.signalCritical
      : 'transparent';
  const pillTextColor = isCorrectOption || isSelected ? colors.ivoryFog : colors.textMuted;
  const textColor = isCorrectOption ? colors.signalSuccess : isSelected ? colors.signalCritical : colors.textMuted;

  return (
    <View
      style={{
        backgroundColor: rowBg,
        borderBottomWidth: 1,
        borderBottomColor: isCorrectOption ? colors.signalSuccess : colors.borderSoft,
        paddingHorizontal: spacing.lg,
        paddingVertical: spacing.md,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
        <View
          style={{
            width: 34,
            height: 34,
            borderRadius: radii.pill,
            borderWidth: 1,
            borderColor: pillBorderColor,
            backgroundColor: pillFillBg,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text variant="label" style={{ color: pillTextColor }}>
            {String.fromCharCode(65 + index)}
          </Text>
        </View>
        <Text
          style={{
            flex: 1,
            ...typography.headline,
            fontWeight: '400',
            color: textColor,
          }}
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
      </View>
    </View>
  );
}

// ------------------------------------------------------------------
// Variant B — Option-centric Inline (WINNER)
// ------------------------------------------------------------------

function VariantBOptionCentric({ reveal }: { reveal: RevealState }) {
  const isCorrect = reveal.kind === 'correct';
  const isNoAnswer = reveal.kind === 'no-answer';
  const selected = reveal.selectedOptionSequenceOrder;
  const outcomeColor = isCorrect
    ? colors.signalSuccess
    : isNoAnswer
      ? colors.textMuted
      : colors.signalCritical;

  return (
    <View style={{ flex: 1 }}>
      <ScrollView contentContainerStyle={{ paddingBottom: 120 }}>
        {/* Minimal header */}
        <View
          style={{
            backgroundColor: colors.panelSurface,
            borderBottomWidth: 1,
            borderBottomColor: colors.borderSoft,
            paddingTop: 52,
            paddingHorizontal: spacing.lg,
            paddingBottom: spacing.md,
          }}
        >
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
            <View
              style={{
                width: 8,
                height: 8,
                borderRadius: radii.pill,
                backgroundColor: colors.signalCritical,
              }}
            />
            <Text variant="label" style={{ color: colors.textMuted }}>
              QUESTION CLOSED
            </Text>
          </View>
        </View>

        {/* Question prompt */}
        <View style={{ padding: spacing.lg, borderBottomWidth: 1, borderBottomColor: colors.borderSoft }}>
          <Text selectable accessibilityRole="header" style={typography.display}>
            {STUB_QUESTION.prompt}
          </Text>
        </View>

        {/* Options */}
        <View>
          {STUB_QUESTION.options.map((option, index) => {
            const seq = index + 1;
            const isCorrectOption = seq === CORRECT_INDEX;
            const isSelected = seq === selected;
            return (
              <OptionRow
                key={`${index}-${option}`}
                index={index}
                option={option}
                isCorrectOption={isCorrectOption}
                isSelected={isSelected}
                chip={isCorrectOption ? 'CORRECT' : isSelected ? 'YOUR ANSWER' : undefined}
              />
            );
          })}
        </View>

        {/* Compact team outcome */}
        <View
          style={{
            paddingHorizontal: spacing.lg,
            paddingVertical: spacing.md,
            flexDirection: 'row',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
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
          >
            {reveal.points >= 0 ? `+${reveal.points}` : `${reveal.points}`}
          </Text>
        </View>

        {/* Explanation */}
        <View style={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.lg }}>
          <Card style={{ backgroundColor: colors.paperSurface, borderColor: colors.borderSoft }}>
            <View style={{ gap: spacing.xs }}>
              <Text variant="label" muted>WHY</Text>
              <Text variant="body" muted>
                {STUB_EXPLANATION}
              </Text>
            </View>
          </Card>
        </View>
      </ScrollView>
    </View>
  );
}

// ------------------------------------------------------------------
// Screen
// ------------------------------------------------------------------

export default function ResultRevealPrototypeScreen() {
  const [reveal, setReveal] = useState<RevealState>({
    kind: 'correct',
    selectedOptionSequenceOrder: CORRECT_INDEX,
    points: 20,
  });

  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog }}>
      {/* Prototype control strip */}
      <View
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          zIndex: 60,
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
          gap: spacing.xs,
        }}
      >
        <Text variant="label" muted>PROTOTYPE · RESULT REVEAL</Text>
        <RevealControls reveal={reveal} onSetReveal={setReveal} />
      </View>

      {/* Spacer for fixed control strip */}
      <View style={{ height: 100 }} />

      {/* Variant B only */}
      <VariantBOptionCentric reveal={reveal} />
    </View>
  );
}
