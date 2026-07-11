/**
 * Active-question participant surface — chosen design ("Full-bleed Stage").
 *
 * Dev-only preview of the trivia question display with stub data.
 * Mode switcher tabs to preview all states: active, waiting, empty, finished,
 * cancelled, selected option, right answer, wrong answer, go to next.
 */
import { useEffect, useRef, useState } from 'react';
import { Animated, Pressable, ScrollView, View } from 'react-native';
import { Text } from '@/components/ui/text';
import { Card } from '@/components/ui/card';
import {
  ActiveQuestionStage,
  ActiveQuestionStageHeader,
} from '@/components/active-question-stage';
import { QuestionEmptyState } from '@/components/question-empty-state';
import { colors, spacing } from '@/constants/theme';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TimerDisplay } from '@/lib/realtime/timer-types';

type Team = { name: string; participants: string[] };

const STUB_QUESTION: ActiveQuestion = {
  questionIndex: 0,
  sequenceOrder: 3,
  prompt: 'Which street borders the north colonnade of the Plaza Mayor?',
  options: ['Calle del Sol', 'Avenida Mayor', 'Paseo del Prado', 'Rambla Vella'],
  timeLimitSeconds: 60,
  triviaSubstageSnapshotId: 'stub',
};

const CORRECT_INDEX = 1; // Avenida Mayor
const STUB_TIMER: TimerDisplay = { label: '0:47', pct: 78, tone: 'running' };

const YOUR_TEAM: Team = { name: 'Lantern Bearers', participants: ['You', 'Mara', 'Diego', 'Priya'] };
const OTHER_TEAMS: Team[] = [
  { name: 'Compass Rose', participants: ['Ivan', 'Lucía', 'Sam'] },
  { name: 'Ember Foxes', participants: ['Noor', 'Theo', 'Aiko', 'Ben'] },
];

type PreviewMode =
  | 'active'
  | 'waiting'
  | 'none'
  | 'finished'
  | 'cancelled'
  | 'selected'
  | 'right'
  | 'wrong'
  | 'next';

const MODE_LABELS: Record<PreviewMode, string> = {
  active: 'Active question',
  waiting: 'Waiting for next',
  none: 'No question yet',
  finished: 'Finished',
  cancelled: 'Cancelled',
  selected: 'Selected option',
  right: 'Right answer',
  wrong: 'Wrong answer',
  next: 'Go to next',
};

const MODE_ROWS: PreviewMode[][] = [
  ['active', 'waiting', 'none'],
  ['finished', 'cancelled', 'selected'],
  ['right', 'wrong', 'next'],
];

function TeamsSheet({ onClose }: { onClose: () => void }) {
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(20)).current;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(fadeAnim, { toValue: 1, duration: 250, useNativeDriver: true }),
      Animated.timing(slideAnim, { toValue: 0, duration: 250, useNativeDriver: true }),
    ]).start();
  }, []);

  return (
    <Animated.View
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: colors.ivoryFog,
        zIndex: 50,
        opacity: fadeAnim,
        transform: [{ translateY: slideAnim }],
      }}
    >
      <View
        style={{
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.md,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
          flexDirection: 'row',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Text variant="title">All Teams</Text>
        <Pressable
          onPress={onClose}
          accessibilityRole="button"
          accessibilityLabel="Close"
          style={{
            paddingHorizontal: spacing.md,
            paddingVertical: spacing.xs,
            backgroundColor: colors.charcoalRoom,
            borderRadius: 8,
          }}
        >
          <Text variant="label" style={{ color: colors.ivoryFog }}>
            CLOSE
          </Text>
        </Pressable>
      </View>

      <ScrollView contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm, paddingBottom: 100 }}>
        <Card style={{ borderColor: colors.emberAccent, borderWidth: 1.5 }}>
          <View style={{ gap: spacing.xs }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
              <Text variant="title" accent>{YOUR_TEAM.name}</Text>
              <Text variant="label" muted>YOUR TEAM</Text>
            </View>
            {YOUR_TEAM.participants.map((p) => (
              <Text key={p} variant="body">{p}</Text>
            ))}
          </View>
        </Card>
        {OTHER_TEAMS.map((t) => (
          <Card key={t.name}>
            <View style={{ gap: spacing.xs }}>
              <Text variant="title">{t.name}</Text>
              <Text variant="body" muted>{t.participants.join('  ·  ')}</Text>
            </View>
          </Card>
        ))}
      </ScrollView>
    </Animated.View>
  );
}

/** Result banner — renders inline, pushing content down naturally */
function ResultBanner({ kind }: { kind: 'right' | 'wrong' }) {
  const isCorrect = kind === 'right';
  return (
    <View
      style={{
        backgroundColor: isCorrect ? colors.signalSuccess + '1A' : colors.signalCritical + '1A',
        borderTopWidth: 1,
        borderBottomWidth: 1,
        borderColor: isCorrect ? colors.signalSuccess : colors.signalCritical,
        padding: spacing.md,
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.sm,
      }}
    >
      <View
        style={{
          width: 28,
          height: 28,
          borderRadius: 14,
          backgroundColor: isCorrect ? colors.signalSuccess : colors.signalCritical,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Text variant="label" style={{ color: colors.ivoryFog, fontSize: 16 }}>
          {isCorrect ? '✓' : '✕'}
        </Text>
      </View>
      <View style={{ flex: 1 }}>
        <Text variant="title" style={{ color: isCorrect ? colors.signalSuccess : colors.signalCritical }}>
          {isCorrect ? 'Correct!' : 'Incorrect'}
        </Text>
        <Text variant="body" muted>
          {isCorrect
            ? 'Your team answered correctly.'
            : `The correct answer was ${STUB_QUESTION.options[CORRECT_INDEX]}.`}
        </Text>
      </View>
    </View>
  );
}

export default function ActiveQuestionPrototypeScreen() {
  const [mode, setMode] = useState<PreviewMode>('active');
  const [showTeams, setShowTeams] = useState(false);

  const baseScore = 240;
  const bonus = mode === 'right' ? 20 : mode === 'wrong' ? -10 : 0;
  const targetScore = baseScore + bonus;

  const bonusFade = useRef(new Animated.Value(mode === 'right' ? 1 : 0)).current;
  const bonusSlide = useRef(new Animated.Value(mode === 'right' ? 0 : 8)).current;
  const penaltyFade = useRef(new Animated.Value(mode === 'wrong' ? 1 : 0)).current;
  const penaltySlide = useRef(new Animated.Value(mode === 'wrong' ? 0 : 8)).current;

  useEffect(() => {
    if (mode === 'right') {
      Animated.parallel([
        Animated.timing(bonusFade, { toValue: 1, duration: 300, useNativeDriver: true }),
        Animated.timing(bonusSlide, { toValue: 0, duration: 300, useNativeDriver: true }),
      ]).start();
    } else {
      Animated.timing(bonusFade, { toValue: 0, duration: 150, useNativeDriver: true }).start();
    }
  }, [mode]);

  useEffect(() => {
    if (mode === 'wrong') {
      Animated.parallel([
        Animated.timing(penaltyFade, { toValue: 1, duration: 300, useNativeDriver: true }),
        Animated.timing(penaltySlide, { toValue: 0, duration: 300, useNativeDriver: true }),
      ]).start();
    } else {
      Animated.timing(penaltyFade, { toValue: 0, duration: 150, useNativeDriver: true }).start();
    }
  }, [mode]);

  const sessionState =
    mode === 'active' || mode === 'selected' || mode === 'right' || mode === 'wrong' ? 'Active'
    : mode === 'waiting' || mode === 'next' ? 'Active'
    : mode === 'none' ? 'Preparing'
    : mode === 'finished' ? 'Finished'
    : 'Cancelled';

  const isActive = mode === 'active' || mode === 'selected' || mode === 'right' || mode === 'wrong';
  const isWaiting = mode === 'waiting' || mode === 'next';
  const isEmpty = mode === 'none';
  const isClosed = mode === 'finished' || mode === 'cancelled';

  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog }}>
      {/* Mode switcher */}
      <View
        style={{
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
          gap: spacing.xs,
        }}
      >
        <Text variant="label" muted>PROTOTYPE · ACTIVE QUESTION</Text>
        {MODE_ROWS.map((row, ri) => (
          <View key={ri} style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
            {row.map((k) => (
              <Pressable
                key={k}
                onPress={() => setMode(k)}
                style={{
                  paddingHorizontal: spacing.sm,
                  paddingVertical: spacing.xs,
                  borderRadius: 8,
                  backgroundColor: mode === k ? colors.emberAccentSoft : colors.raisedSurface,
                  borderWidth: 1,
                  borderColor: mode === k ? colors.emberAccent : colors.borderSoft,
                }}
              >
                <Text
                  variant="label"
                  style={{
                    color: mode === k ? colors.emberAccentStrong : colors.textMuted,
                    fontSize: 11,
                  }}
                >
                  {MODE_LABELS[k]}
                </Text>
              </Pressable>
            ))}
          </View>
        ))}
      </View>

      {/* Content */}
      {isActive ? (
        <ScrollView style={{ flex: 1 }} contentContainerStyle={{ paddingBottom: 80 }}>
          <ActiveQuestionStage
            question={STUB_QUESTION}
            sessionState={sessionState}
            score={targetScore}
            timerDisplay={STUB_TIMER}
            selectedOptionSequenceOrder={
              mode === 'selected' || mode === 'right' ? CORRECT_INDEX
              : mode === 'wrong' ? 2 // Paseo del Prado (wrong)
              : undefined
            }
            isLocked={mode === 'right' || mode === 'wrong'}
          />
          {(mode === 'right' || mode === 'wrong') ? <ResultBanner kind={mode} /> : null}
        </ScrollView>
      ) : isWaiting ? (
          <View style={{ flex: 1 }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={targetScore} />
            <QuestionEmptyState kind="waiting" sessionState={sessionState} />
          </View>
        ) : isEmpty ? (
          <View style={{ flex: 1 }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={targetScore} />
            <QuestionEmptyState kind="none" sessionState={sessionState} />
          </View>
        ) : (
          <View style={{ flex: 1 }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={targetScore} />
            <QuestionEmptyState kind="closed" sessionState={sessionState} />
          </View>
        )}

      {/* Score bonus indicator — right answer only */}
      {mode === 'right' ? (
        <Animated.View
          style={{
            position: 'absolute',
            top: 80,
            right: spacing.lg,
            zIndex: 20,
            backgroundColor: colors.signalSuccess + '1A',
            borderWidth: 1,
            borderColor: colors.signalSuccess,
            borderRadius: 8,
            paddingHorizontal: spacing.sm,
            paddingVertical: 2,
            opacity: bonusFade,
            transform: [{ translateY: bonusSlide }],
          }}
        >
          <Text variant="label" style={{ color: colors.signalSuccess }}>
            +20
          </Text>
        </Animated.View>
      ) : null}

      {/* Score penalty indicator — wrong answer only */}
      {mode === 'wrong' ? (
        <Animated.View
          style={{
            position: 'absolute',
            top: 80,
            right: spacing.lg,
            zIndex: 20,
            backgroundColor: colors.signalCritical + '1A',
            borderWidth: 1,
            borderColor: colors.signalCritical,
            borderRadius: 8,
            paddingHorizontal: spacing.sm,
            paddingVertical: 2,
            opacity: penaltyFade,
            transform: [{ translateY: penaltySlide }],
          }}
        >
          <Text variant="label" style={{ color: colors.signalCritical }}>
            -10
          </Text>
        </Animated.View>
      ) : null}

      {/* Persistent team footer — tappable */}
      <Pressable
        onPress={() => setShowTeams(true)}
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: colors.charcoalRoom,
          paddingHorizontal: spacing.lg,
          paddingTop: spacing.xs,
          paddingBottom: 28,
        }}
      >
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <Text variant="label" style={{ color: colors.emberAccentSoft }}>
            YOUR TEAM · {YOUR_TEAM.name}
          </Text>
          <Text variant="label" style={{ color: colors.emberAccentSoft }}>
            ALL TEAMS ›
          </Text>
        </View>
        <Text variant="body" style={{ color: colors.ivoryFog }} numberOfLines={1}>
          {YOUR_TEAM.participants.join('  ·  ')}
        </Text>
      </Pressable>

      {/* Teams sheet overlay */}
      {showTeams ? <TeamsSheet onClose={() => setShowTeams(false)} /> : null}
    </View>
  );
}
