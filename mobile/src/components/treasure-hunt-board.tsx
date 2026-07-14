/**
 * Treasure-hunt participant live board (HU-23).
 *
 * Lifts the "Focus Tabs" layout from the (now-deleted) dev prototype — a compact
 * sticky header (substage title + score + timer) over a segmented Map / Clues /
 * Teams body, with a persistent "your team" strip — but is fed entirely by props
 * from the live HU-23 snapshot/push. It is TEAM-ONLY: score, target progress and
 * clues are for the participant's own team. Other-team cards are static,
 * clearly-marked placeholders (real standings/ranking is HU-39); the Map tab renders the live target
 * coordinates (#154) on a real Leaflet map (#156) via TargetMap.
 *
 * Layout: this is a full-viewport surface — sticky header, `flex: 1` body, absolutely-pinned team
 * strip — so it must be given the whole screen, never nested inside another vertical ScrollView.
 * The tab bodies are then the only scroller, which is what makes a long clue list scroll at all.
 */
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { OperativeClueSurface } from '@/components/operative-clue-surface';
import { SessionTimerBar } from '@/components/session-timer-bar';
import { TargetMap } from '@/components/target-map';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';
import type { TimerDisplay } from '@/lib/realtime/timer-types';
import { clueKey, type ActiveTargetDto, type VisibleClueDto } from '@/lib/realtime/team-board-types';

// Other-team cards are static placeholders until HU-39 (real standings/ranking).
// Each is tagged in the UI so it is unmistakable to sighted users and to
// assistive tech (PLACEHOLDER badge + "sample standings" hint).
const PLACEHOLDER_OTHER_TEAMS: readonly { name: string }[] = [
  { name: 'Compass Rose' },
  { name: 'Ember Foxes' },
];

// --- Presentational pieces (ported from the prototype) ---

// No scope chip: the live HU-23 VisibleClueDto has no team/global scope field. Heading is the target
// name; body is the clue text. Target-less clues (operative / substage-initial) carry no target name,
// so they read a plain "CLUE" label — one unified Clues surface, no per-kind wording.
function ClueCard({ clue }: { clue: VisibleClueDto }) {
  return (
    <Card parchment>
      <View accessibilityRole="text" style={{ gap: spacing.xs }}>
        <Text variant="label" muted>{clue.targetName ?? 'CLUE'}</Text>
        <Text variant="mono">{clue.clueText}</Text>
      </View>
    </Card>
  );
}

// --- Board ---

export type TreasureHuntBoardProps = {
  teamDisplayName: string;
  currentScore: number;
  timerDisplay: TimerDisplay;
  resolvedTargets: number;
  totalActiveTargets: number;
  visibleClues: readonly VisibleClueDto[];
  // Active treasure-hunt targets with coordinates; drives the Map tab (#156). Absent/empty → map
  // empty state. Optional so a board push that predates #156 still renders (degrades, never crashes).
  activeTargets?: readonly ActiveTargetDto[];
  // The board owns the whole viewport (its body is the only scroller), so session chrome that would
  // otherwise sit around it has to come in: `headerSlot` rides the sticky header — it is where the
  // substage name belongs (#171, see below) and where a transient connection banner stays readable on
  // every tab. `onLeave` adds the leave affordance to the persistent team strip.
  headerSlot?: ReactNode;
  onLeave?: () => void;
};

export function TreasureHuntBoard({
  teamDisplayName,
  currentScore,
  timerDisplay,
  resolvedTargets,
  totalActiveTargets,
  visibleClues,
  activeTargets = [],
  headerSlot,
  onLeave,
}: TreasureHuntBoardProps) {
  const [tab, setTab] = useState<'map' | 'clues' | 'teams'>('map');

  // HU-28: the board projection appends operative clues after the substage's target clues
  // (`LiveSession.CollectVisibleClues`), which buries operator-pushed guidance below the mission's
  // own always-visible clues — on a substage with a few targets it lands off the bottom of the tab,
  // leaving the list looking unchanged. Op clues are the newest and most actionable guidance, so pin
  // them above target clues. Within each group the list reads newest → oldest: operatives arrive
  // already ordered by CreatedAt DESC from the backend; target clues are reversed so the last in
  // SequenceOrder (closest to "last released") appears at the top of the target group.
  const orderedClues = useMemo(
    () => [
      ...visibleClues.filter((c) => c.targetSnapshotId == null),
      ...visibleClues.filter((c) => c.targetSnapshotId != null).reverse(),
    ],
    [visibleClues],
  );

  // Reveal indicator: track which clue ids have been seen. Mount baseline = the seed snapshot's
  // clues (already-seen) so a reconnect never false-flashes; only clues arriving in a LATER render
  // are "new". Prop-diff bookkeeping only — never owns board state.
  const seenClueIds = useRef<Set<string>>(new Set(visibleClues.map(clueKey)));

  // Acknowledge (mark current clues seen) whenever the participant is on the CLUES tab.
  useEffect(() => {
    if (tab === 'clues') {
      for (const c of visibleClues) seenClueIds.current.add(clueKey(c));
    }
  }, [tab, visibleClues]);

  const hasNewClues =
    tab !== 'clues' && visibleClues.some((c) => !seenClueIds.current.has(clueKey(c)));

  return (
    <View
      accessibilityLabel={`Treasure hunt board for ${teamDisplayName}`}
      style={{ flex: 1, backgroundColor: colors.ivoryFog }}
    >
      <View
        style={{
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
          gap: spacing.sm,
        }}
      >
        {headerSlot}

        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <View style={{ flex: 1, paddingRight: spacing.sm }}>
            {/* Active substage name is owned by the shared SubstageProgress component (#171), which
                the host hands in through `headerSlot` above. */}
            <Text variant="label" muted>TREASURE HUNT</Text>
          </View>
          <View style={{ alignItems: 'flex-end' }}>
            <Text variant="label" muted>SCORE</Text>
            <Text style={{ ...typography.headline, fontSize: 22, color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}>
              {currentScore}
            </Text>
          </View>
        </View>

        <SessionTimerBar display={timerDisplay} />

        <View
          style={{
            flexDirection: 'row',
            backgroundColor: colors.raisedSurface,
            borderRadius: radii.control,
            borderCurve: 'continuous',
            borderWidth: 1,
            borderColor: colors.borderSoft,
            padding: 3,
          }}
        >
          {(['map', 'clues', 'teams'] as const).map((k) => {
            const showDot = k === 'clues' && hasNewClues;
            return (
              <Pressable
                key={k}
                accessibilityRole="button"
                accessibilityLabel={showDot ? 'New clue available' : undefined}
                onPress={() => setTab(k)}
                style={{
                  flex: 1,
                  flexDirection: 'row',
                  justifyContent: 'center',
                  alignItems: 'center',
                  gap: 6,
                  paddingVertical: spacing.xs,
                  borderRadius: radii.control - 3,
                  borderCurve: 'continuous',
                  backgroundColor: tab === k ? colors.paperSurface : 'transparent',
                  boxShadow: tab === k ? shadows.insetSheen : undefined,
                }}
              >
                <Text variant="label" style={{ color: tab === k ? colors.textInk : colors.textMuted }}>
                  {k.toUpperCase()}
                </Text>
                {showDot ? (
                  // Ember dot: tiny new-clue accent (DESIGN Ember Rule); ClueCard artifact untouched.
                  <View
                    testID="treasure-hunt-clue-indicator"
                    style={{ width: 8, height: 8, borderRadius: 4, borderCurve: 'continuous', backgroundColor: colors.emberAccentStrong }}
                  />
                ) : null}
              </Pressable>
            );
          })}
        </View>
      </View>

      <View style={{ flex: 1 }}>
        {tab === 'map' ? (
          <View style={{ flex: 1, padding: spacing.lg, paddingBottom: 100, gap: spacing.sm }}>
            <TargetMap targets={activeTargets} style={{ flex: 1 }} />
            <Card>
              <View style={{ gap: 2 }}>
                <Text variant="label" muted>TARGET</Text>
                <Text
                  variant="title"
                  accessibilityRole="text"
                  accessibilityLabel={`Targets resolved ${resolvedTargets} of ${totalActiveTargets}`}
                  style={{ fontVariant: ['tabular-nums'] }}
                >
                  {resolvedTargets} / {totalActiveTargets} targets
                </Text>
              </View>
            </Card>
          </View>
        ) : tab === 'clues' ? (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            {orderedClues.length > 0 ? (
              orderedClues.map((c) => <ClueCard key={clueKey(c)} clue={c} />)
            ) : (
              <Card>
                <Text variant="body" muted>No clues yet.</Text>
              </Card>
            )}
          </Screen>
        ) : (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            <Card style={{ borderColor: colors.emberAccent, borderWidth: 1.5 }}>
              <View style={{ gap: spacing.xs }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Text variant="title" accent>{teamDisplayName}</Text>
                  <Text variant="label" muted>YOUR TEAM</Text>
                </View>
                <Text
                  style={{ ...typography.headline, fontSize: 22, color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
                >
                  {currentScore}
                </Text>
              </View>
            </Card>
            {PLACEHOLDER_OTHER_TEAMS.map((t) => (
              <Card key={t.name} accessibilityHint="Sample standings — not live yet">
                <View style={{ gap: spacing.xs }}>
                  <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                    <Text variant="title">{t.name}</Text>
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
                      <Text variant="label" muted>PLACEHOLDER</Text>
                    </View>
                  </View>
                  <Text variant="body" muted>Sample standings — not live yet</Text>
                </View>
              </Card>
            ))}
          </Screen>
        )}
      </View>

      {/* HU-28: the same viewport-pinned arrival toast the trivia surface raises. The Clues tab is
          already the durable home here, so this is toast-only (no chip) — it stays quiet while that
          tab is open and opens it when tapped. */}
      <OperativeClueSurface
        visibleClues={visibleClues}
        home={{ listVisible: tab === 'clues', onOpen: () => setTab('clues') }}
      />

      {/* persistent "your team" strip so team identity survives tab switches */}
      <View
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: colors.charcoalRoom,
          paddingHorizontal: spacing.lg,
          paddingTop: spacing.xs,
          paddingBottom: 28,
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.sm,
        }}
      >
        <View style={{ flex: 1 }}>
          <Text variant="label" style={{ color: colors.emberAccentSoft }}>
            YOUR TEAM · {teamDisplayName}
          </Text>
          <Text
            variant="body"
            style={{ color: colors.ivoryFog, fontVariant: ['tabular-nums'] }}
            numberOfLines={1}
          >
            SCORE {currentScore}
          </Text>
        </View>
        {onLeave ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Leave team space"
            onPress={onLeave}
            style={{
              borderWidth: 1,
              borderColor: colors.emberAccentSoft,
              borderRadius: radii.pill,
              borderCurve: 'continuous',
              paddingHorizontal: spacing.md,
              paddingVertical: spacing.xs,
            }}
          >
            <Text variant="label" style={{ color: colors.emberAccentSoft }}>LEAVE</Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}
