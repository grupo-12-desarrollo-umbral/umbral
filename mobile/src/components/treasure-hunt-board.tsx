/**
 * Treasure-hunt participant live board (HU-23).
 *
 * Lifts the "Focus Tabs" layout from the (now-deleted) dev prototype — a compact
 * sticky header (substage title + score + timer) over a segmented Map / Clues /
 * Teams body, with a persistent "your team" strip — but is fed entirely by props
 * from the live HU-23 snapshot/push. It is TEAM-ONLY: score, target progress and
 * clues are for the participant's own team. Other-team cards are static,
 * clearly-marked placeholders (real standings/ranking is HU-39); the map stays a
 * labelled stub (real map is #156, target coordinates are #154).
 */
import { useEffect, useRef, useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { SessionTimerBar } from '@/components/session-timer-bar';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';
import type { TimerDisplay } from '@/lib/realtime/timer-types';
import type { VisibleClueDto } from '@/lib/realtime/team-board-types';

// Other-team cards are static placeholders until HU-39 (real standings/ranking).
// Each is tagged in the UI so it is unmistakable to sighted users and to
// assistive tech (PLACEHOLDER badge + "sample standings" hint).
const PLACEHOLDER_OTHER_TEAMS: readonly { name: string }[] = [
  { name: 'Compass Rose' },
  { name: 'Ember Foxes' },
];

// --- Presentational pieces (ported from the prototype) ---

function GridLines() {
  const at = ['20%', '40%', '60%', '80%'] as const;
  return (
    <View style={[StyleSheet.absoluteFill, { pointerEvents: 'none' }]}>
      {at.map((p) => (
        <View
          key={`h${p}`}
          style={{ position: 'absolute', left: 0, right: 0, top: p, height: 1, backgroundColor: colors.borderSoft, opacity: 0.5 }}
        />
      ))}
      {at.map((p) => (
        <View
          key={`v${p}`}
          style={{ position: 'absolute', top: 0, bottom: 0, left: p, width: 1, backgroundColor: colors.borderSoft, opacity: 0.5 }}
        />
      ))}
    </View>
  );
}

// Coordinate chip dropped: the HU-23 contract carries no target coordinates.
function MapStub({ style }: { style?: object }) {
  return (
    <View
      accessibilityRole="image"
      accessibilityLabel="Map preview stub"
      style={[
        {
          backgroundColor: colors.warmMist,
          borderRadius: radii.card,
          borderCurve: 'continuous',
          borderWidth: 1,
          borderColor: colors.borderSoft,
          overflow: 'hidden',
          alignItems: 'center',
          justifyContent: 'center',
        },
        style,
      ]}
    >
      <GridLines />
      <View style={{ alignItems: 'center', gap: spacing.xs }}>
        <View
          style={{
            width: 24,
            height: 24,
            borderRadius: 12,
            backgroundColor: colors.emberAccentStrong,
            borderWidth: 3,
            borderColor: colors.ivoryFog,
            boxShadow: shadows.card,
          }}
        />
        <View
          style={{
            backgroundColor: colors.paperSurface,
            borderRadius: radii.pill,
            borderWidth: 1,
            borderColor: colors.borderSoft,
            paddingHorizontal: spacing.sm,
            paddingVertical: spacing.one,
          }}
        >
          <Text variant="label" muted>MAP PREVIEW · STUB</Text>
        </View>
      </View>
    </View>
  );
}

// No scope chip: the live HU-23 VisibleClueDto has no team/global scope field.
// Heading is the target name; body is the clue text.
function ClueCard({ clue }: { clue: VisibleClueDto }) {
  return (
    <Card parchment>
      <View accessibilityRole="text" style={{ gap: spacing.xs }}>
        <Text variant="label" muted>{clue.targetName}</Text>
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
};

export function TreasureHuntBoard({
  teamDisplayName,
  currentScore,
  timerDisplay,
  resolvedTargets,
  totalActiveTargets,
  visibleClues,
}: TreasureHuntBoardProps) {
  const [tab, setTab] = useState<'map' | 'clues' | 'teams'>('map');

  // Reveal indicator: track which clue ids have been seen. Mount baseline = the seed snapshot's
  // clues (already-seen) so a reconnect never false-flashes; only clues arriving in a LATER render
  // are "new". Prop-diff bookkeeping only — never owns board state.
  const seenClueIds = useRef<Set<string>>(new Set(visibleClues.map((c) => c.targetSnapshotId)));

  // Acknowledge (mark current clues seen) whenever the participant is on the CLUES tab.
  useEffect(() => {
    if (tab === 'clues') {
      for (const c of visibleClues) seenClueIds.current.add(c.targetSnapshotId);
    }
  }, [tab, visibleClues]);

  const hasNewClues =
    tab !== 'clues' && visibleClues.some((c) => !seenClueIds.current.has(c.targetSnapshotId));

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
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <View style={{ flex: 1, paddingRight: spacing.sm }}>
            {/* Active substage name is owned by the shared SubstageProgress component (#171). */}
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
            <MapStub style={{ flex: 1 }} />
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
            {visibleClues.length > 0 ? (
              visibleClues.map((c) => <ClueCard key={c.targetSnapshotId} clue={c} />)
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
        }}
      >
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
    </View>
  );
}
