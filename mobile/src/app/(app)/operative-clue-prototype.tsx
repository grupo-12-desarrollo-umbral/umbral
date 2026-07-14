/**
 * THROWAWAY UI PROTOTYPE — HU-28 operative-clue reveal on the TRIVIA participant surface.
 *
 * Three radically different variants of how an operator-authored operative clue
 * (a `VisibleClueDto` with null targets + a real `operativeClueId`) surfaces on the
 * trivia view — switchable via `?variant=` from the floating bottom bar:
 *
 *   A — Signal chip + toast   (the current B2 doc decision: collapsed in-flow pill +
 *                              transient viewport-top toast)
 *   B — Docked drawer         (persistent bottom handle → slide-up bottom sheet; no toast)
 *   C — Inline artifact feed   (clues animate in as full parchment cards, newest-first)
 *
 * Sub-shape B (a throwaway route): the live trivia host `LiveTeamSpace` needs a running
 * gateway + active trivia substage to reach, so — like `active-question-prototype.tsx` —
 * this stubs the data and mounts a COPY of the real `ActiveQuestionStage` as the backdrop.
 * The "＋ Simulate operator clue" control fakes the live push. Read-only; no mutations.
 *
 * Delete this route, the switcher, and `components/prototype/` once a variant wins.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  AccessibilityInfo,
  Animated,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  useAnimatedValue,
  View,
} from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Card } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import { ActiveQuestionStage } from '@/components/prototype/active-question-stage.copy';
import { PrototypeSwitcher, type PrototypeVariant } from '@/components/prototype/prototype-switcher';
import { colors, radii, shadows, spacing } from '@/constants/theme';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TimerDisplay } from '@/lib/realtime/timer-types';
import type { VisibleClueDto } from '@/lib/realtime/team-board-types';

const VARIANTS: readonly PrototypeVariant[] = [
  { key: 'A', name: 'Signal chip' },
  { key: 'B', name: 'Docked drawer' },
  { key: 'C', name: 'Inline feed' },
];

const STUB_QUESTION: ActiveQuestion = {
  questionIndex: 0,
  sequenceOrder: 3,
  prompt: 'Which street borders the north colonnade of the Plaza Mayor?',
  options: ['Calle del Sol', 'Avenida Mayor', 'Paseo del Prado', 'Rambla Vella'],
  timeLimitSeconds: 60,
  triviaSubstageSnapshotId: 'stub',
};
const STUB_TIMER: TimerDisplay = { label: '0:47', pct: 78, tone: 'running' };

// Operator-authored operative clues: null targets, real operativeClueId (mirrors the P0 projection).
const STUB_CLUE_TEXTS: readonly string[] = [
  'The gatekeeper answers only to those who know the third lantern is never lit.',
  'Count the arches on the east wall — the true door is the one with no number.',
  'Ignore the loud market. The quiet vendor by the fountain holds the token.',
  'When the bell tower shows a quarter past, the north gate is unguarded.',
];

// Operative clues key on their operativeClueId (target fields are null). Mirrors treasure-hunt-board.
const clueKey = (c: VisibleClueDto): string => c.operativeClueId!;

/** The durable artifact — parchment Card + "OPERATIVE CLUE" label + mono body (matches ClueCard). */
function OperativeClueArtifact({ clue }: { clue: VisibleClueDto }) {
  return (
    <Card parchment>
      <View accessibilityRole="text" style={{ gap: spacing.xs }}>
        <Text variant="label" muted>{clue.targetName ?? 'OPERATIVE CLUE'}</Text>
        <Text variant="mono">{clue.clueText}</Text>
      </View>
    </Card>
  );
}

function EmptyHint() {
  return (
    <View style={{ padding: spacing.lg }}>
      <Card>
        <Text variant="body" muted>
          No operative clues yet — tap ＋ Simulate operator clue to push one.
        </Text>
      </Card>
    </View>
  );
}

export default function OperativeCluePrototypeScreen() {
  const params = useLocalSearchParams<{ variant?: string }>();
  const variant = (Array.isArray(params.variant) ? params.variant[0] : params.variant) ?? 'A';

  const [clues, setClues] = useState<VisibleClueDto[]>([]);
  const [arrivalNonce, setArrivalNonce] = useState(0);
  const [expanded, setExpanded] = useState(false); // Variant A inline list
  const [sheetOpen, setSheetOpen] = useState(false); // Variant B bottom sheet
  const [seenCount, setSeenCount] = useState(0);
  const pushCountRef = useRef(0);

  // While the list/sheet is open every clue is visible, so unseen is 0 by definition;
  // closing (below) marks them seen. Deriving this avoids setting state from an effect.
  const isOpen = expanded || sheetOpen;
  const unseen = isOpen ? 0 : clues.length - seenCount;

  const simulate = useCallback(() => {
    const n = pushCountRef.current;
    pushCountRef.current += 1;
    const clue: VisibleClueDto = {
      targetSnapshotId: null,
      targetName: null,
      operativeClueId: `op-${n}`,
      clueText: STUB_CLUE_TEXTS[n % STUB_CLUE_TEXTS.length],
    };
    setClues((prev) => [...prev, clue]);
    setArrivalNonce((x) => x + 1);
  }, []);

  const reset = useCallback(() => {
    pushCountRef.current = 0;
    setClues([]);
    setArrivalNonce(0);
    setExpanded(false);
    setSheetOpen(false);
    setSeenCount(0);
  }, []);

  const openList = useCallback(() => setExpanded(true), []);
  const closeList = useCallback(() => {
    setExpanded(false);
    setSeenCount(clues.length);
  }, [clues.length]);

  const openSheet = useCallback(() => setSheetOpen(true), []);
  const closeSheet = useCallback(() => {
    setSheetOpen(false);
    setSeenCount(clues.length);
  }, [clues.length]);

  const newest = clues.length > 0 ? clues[clues.length - 1] : null;

  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog }}>
      {/* Prototype control strip (chrome — not part of the design being judged) */}
      <View
        style={{
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
          gap: spacing.xs,
          zIndex: 60,
        }}
      >
        <Text variant="label" muted>PROTOTYPE · OPERATIVE CLUE · TRIVIA</Text>
        <View style={{ flexDirection: 'row', gap: spacing.xs }}>
          <ControlPill label="＋ Simulate operator clue" onPress={simulate} emphasis />
          <ControlPill label="Reset" onPress={reset} />
        </View>
      </View>

      <ScrollView contentContainerStyle={{ paddingBottom: 200 }}>
        <ActiveQuestionStage
          question={STUB_QUESTION}
          sessionState="Active"
          score={240}
          timerDisplay={STUB_TIMER}
          selectedOptionSequenceOrder={2}
          onSelectOption={() => {}}
          onSubmit={() => {}}
        />

        {/* IN-FLOW surfaces */}
        {clues.length === 0 ? <EmptyHint /> : null}

        {variant === 'A' && clues.length > 0 ? (
          <ChipInline clues={clues} unseen={unseen} expanded={expanded} onToggle={() => (expanded ? closeList() : openList())} />
        ) : null}

        {variant === 'C' && clues.length > 0 ? <InlineFeed clues={clues} /> : null}
      </ScrollView>

      {/* OVERLAY surfaces */}
      {variant === 'A' && newest ? (
        <ClueToast key={arrivalNonce} clue={newest} onOpen={openList} />
      ) : null}

      {variant === 'B' ? (
        <DockedHandle count={clues.length} unseen={unseen} arrivalNonce={arrivalNonce} onOpen={openSheet} />
      ) : null}
      {variant === 'B' && sheetOpen ? (
        <ClueSheet clues={clues} onClose={closeSheet} />
      ) : null}

      <PrototypeSwitcher variants={VARIANTS} />
    </View>
  );
}

// --- Prototype chrome ---

function ControlPill({ label, onPress, emphasis }: { label: string; onPress: () => void; emphasis?: boolean }) {
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
        backgroundColor: emphasis ? colors.emberAccentSoft : colors.raisedSurface,
        borderColor: emphasis ? colors.emberAccent : colors.borderSoft,
      }}
    >
      <Text variant="label" style={{ color: emphasis ? colors.emberAccentStrong : colors.textMuted }}>
        {label}
      </Text>
    </Pressable>
  );
}

// --- Variant A: collapsed signal chip + inline expansion ---

function ChipInline({
  clues,
  unseen,
  expanded,
  onToggle,
}: {
  clues: VisibleClueDto[];
  unseen: number;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <View style={{ paddingHorizontal: spacing.lg, paddingTop: spacing.md, gap: spacing.sm }}>
      <Pressable
        testID="operative-clue-list"
        accessibilityRole="button"
        accessibilityLabel={`Operative clues, ${clues.length}${unseen > 0 ? `, ${unseen} new` : ''}`}
        onPress={onToggle}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          backgroundColor: colors.raisedSurface,
          borderWidth: 1,
          borderColor: colors.borderSoft,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          boxShadow: shadows.insetSheen,
          paddingHorizontal: spacing.md,
          paddingVertical: spacing.sm,
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
          {unseen > 0 ? (
            <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.emberAccentStrong }} />
          ) : null}
          <Text variant="label" muted>{`OPERATIVE CLUES · ${clues.length}`}</Text>
        </View>
        <Text variant="label" muted>{expanded ? '▲' : '▼'}</Text>
      </Pressable>

      {expanded ? (
        <View style={{ gap: spacing.sm }}>
          {clues.map((c) => (
            <OperativeClueArtifact key={clueKey(c)} clue={c} />
          ))}
        </View>
      ) : null}
    </View>
  );
}

function ClueToast({ clue, onOpen }: { clue: VisibleClueDto; onOpen: () => void }) {
  const translateY = useAnimatedValue(-24);
  const opacity = useAnimatedValue(0);
  const [gone, setGone] = useState(false);

  const dismiss = useCallback(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: -24, duration: 180, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 0, duration: 180, useNativeDriver: true }),
    ]).start(() => setGone(true));
  }, [translateY, opacity]);

  useEffect(() => {
    if (Platform.OS !== 'web') {
      AccessibilityInfo.announceForAccessibility(`New operative clue. ${clue.clueText}`);
    }
    Animated.parallel([
      Animated.timing(translateY, { toValue: 0, duration: 220, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 1, duration: 220, useNativeDriver: true }),
    ]).start();
    const t = setTimeout(dismiss, 4000);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (gone) return null;

  return (
    <Animated.View
      accessibilityLiveRegion="polite"
      style={{
        position: 'absolute',
        top: 56,
        left: spacing.lg,
        right: spacing.lg,
        zIndex: 90,
        opacity,
        transform: [{ translateY }],
      }}
    >
      <Pressable
        testID="operative-clue-toast"
        accessibilityRole="button"
        accessibilityLabel="Open operative clues"
        onPress={() => {
          onOpen();
          dismiss();
        }}
        style={{
          backgroundColor: colors.panelSurface,
          borderColor: colors.borderSoft,
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
        <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.emberAccentStrong }} />
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="label" muted>NEW OPERATIVE CLUE</Text>
          <Text variant="body" numberOfLines={1}>{clue.clueText}</Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Dismiss"
          onPress={dismiss}
          hitSlop={spacing.xs}
          style={{ width: 24, height: 24, alignItems: 'center', justifyContent: 'center' }}
        >
          <Text variant="label" muted>✕</Text>
        </Pressable>
      </Pressable>
    </Animated.View>
  );
}

// --- Variant B: docked handle + slide-up bottom sheet ---

function DockedHandle({
  count,
  unseen,
  arrivalNonce,
  onOpen,
}: {
  count: number;
  unseen: number;
  arrivalNonce: number;
  onOpen: () => void;
}) {
  const scale = useAnimatedValue(1);

  // Pulse on arrival — the handle IS the arrival cue in this variant (no toast).
  useEffect(() => {
    if (arrivalNonce === 0) return;
    Animated.sequence([
      Animated.timing(scale, { toValue: 1.04, duration: 140, useNativeDriver: true }),
      Animated.timing(scale, { toValue: 1, duration: 160, useNativeDriver: true }),
    ]).start();
  }, [arrivalNonce, scale]);

  return (
    <View
      pointerEvents="box-none"
      style={{ position: 'absolute', left: 0, right: 0, bottom: 88, paddingHorizontal: spacing.lg, zIndex: 80 }}
    >
      <Animated.View style={{ transform: [{ scale }] }}>
        <Pressable
          testID="operative-clue-handle"
          accessibilityRole="button"
          accessibilityLabel={`Open operative clues, ${count}${unseen > 0 ? `, ${unseen} new` : ''}`}
          onPress={onOpen}
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: colors.raisedSurface,
            borderWidth: 1,
            borderColor: colors.borderSoft,
            borderRadius: radii.control,
            borderCurve: 'continuous',
            boxShadow: shadows.card,
            paddingHorizontal: spacing.md,
            paddingVertical: spacing.sm,
          }}
        >
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
            {unseen > 0 ? (
              <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.emberAccentStrong }} />
            ) : null}
            <Text variant="label" muted>{`OPERATIVE CLUES · ${count}`}</Text>
          </View>
          <Text variant="label" muted>▲</Text>
        </Pressable>
      </Animated.View>
    </View>
  );
}

function ClueSheet({ clues, onClose }: { clues: VisibleClueDto[]; onClose: () => void }) {
  const translateY = useAnimatedValue(640);
  const scrim = useAnimatedValue(0);
  const [gone, setGone] = useState(false);

  const close = useCallback(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: 640, duration: 220, useNativeDriver: true }),
      Animated.timing(scrim, { toValue: 0, duration: 180, useNativeDriver: true }),
    ]).start(() => {
      setGone(true);
      onClose();
    });
  }, [translateY, scrim, onClose]);

  useEffect(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: 0, duration: 240, useNativeDriver: true }),
      Animated.timing(scrim, { toValue: 1, duration: 240, useNativeDriver: true }),
    ]).start();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (gone) return null;

  return (
    <View style={[StyleSheet.absoluteFill, { zIndex: 95 }]}>
      <Animated.View style={{ flex: 1, backgroundColor: 'rgba(24, 17, 12, 0.45)', opacity: scrim }}>
        <Pressable accessibilityRole="button" accessibilityLabel="Close operative clues" onPress={close} style={{ flex: 1 }} />
      </Animated.View>
      <Animated.View
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          maxHeight: '62%',
          backgroundColor: colors.ivoryFog,
          borderTopLeftRadius: radii.panel,
          borderTopRightRadius: radii.panel,
          borderCurve: 'continuous',
          borderTopWidth: 1,
          borderColor: colors.borderSoft,
          transform: [{ translateY }],
        }}
      >
        <View
          style={{
            flexDirection: 'row',
            justifyContent: 'space-between',
            alignItems: 'center',
            paddingHorizontal: spacing.lg,
            paddingTop: spacing.md,
            paddingBottom: spacing.sm,
            borderBottomWidth: 1,
            borderColor: colors.borderSoft,
          }}
        >
          <Text variant="title">Operative clues</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Close"
            onPress={close}
            style={{ paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, backgroundColor: colors.charcoalRoom, borderRadius: radii.control, borderCurve: 'continuous' }}
          >
            <Text variant="label" style={{ color: colors.emberAccentSoft }}>CLOSE</Text>
          </Pressable>
        </View>
        <ScrollView testID="operative-clue-list" contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm, paddingBottom: spacing.xl }}>
          {clues.map((c) => (
            <OperativeClueArtifact key={clueKey(c)} clue={c} />
          ))}
        </ScrollView>
      </Animated.View>
    </View>
  );
}

// --- Variant C: inline artifact feed (newest-first, each animates in) ---

function InlineFeed({ clues }: { clues: VisibleClueDto[] }) {
  // Newest first — the freshest clue reads at the top of the feed, directly under the stage.
  const ordered = useMemo(() => clues.slice().reverse(), [clues]);
  return (
    <View testID="operative-clue-list" style={{ paddingHorizontal: spacing.lg, paddingTop: spacing.md, gap: spacing.sm }}>
      <Text variant="label" muted>OPERATIVE CLUES</Text>
      {ordered.map((c) => (
        <FeedCard key={clueKey(c)} clue={c} />
      ))}
    </View>
  );
}

/** Each feed card slides down + fades in as it mounts — a new clue "arrives" into the feed. */
function FeedCard({ clue }: { clue: VisibleClueDto }) {
  const translateY = useAnimatedValue(-12);
  const opacity = useAnimatedValue(0);

  useEffect(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: 0, duration: 260, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 1, duration: 260, useNativeDriver: true }),
    ]).start();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Animated.View style={{ opacity, transform: [{ translateY }] }}>
      <OperativeClueArtifact clue={clue} />
    </Animated.View>
  );
}
