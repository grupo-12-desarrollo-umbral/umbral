/**
 * Treasure-hunt participant live board (HU-23 / HU-25B).
 *
 * Lifts the "Focus Tabs" layout from the (now-deleted) dev prototype — a compact
 * sticky header (substage title + score + timer) over a segmented Map / Clues /
 * Teams body, with a persistent "your team" strip — but is fed entirely by props
 * from the live HU-23 snapshot/push. It is TEAM-ONLY: score, target progress and
 * clues are for the participant's own team. The Map tab renders the live target
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
import type { RankingRowDto } from '@/lib/realtime/ranking-types';
import { rankingErrorCopy } from '@/lib/realtime/ranking-error-copy';
import type { TimerSnapshotError } from '@/lib/api/sessions';
import { RankingLeaderboard } from './ranking-leaderboard';

// Fallback other-team cards used when real ranking rows have not yet loaded.
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
        <Text variant="label" muted>{clue.targetName ?? 'PISTA'}</Text>
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
  // The active substage's snapshot id, threaded to the clue surface so its arrival toast re-arms on a
  // substage boundary (see OperativeClueSurface). Also re-arms this board's own reveal-dot baseline so
  // a new substage's visible-on-start clues flash as new. Optional → degrades to mount-only baseline.
  substageId?: string | null;
  // The board owns the whole viewport (its body is the only scroller), so session chrome that would
  // otherwise sit around it has to come in: `headerSlot` rides the sticky header — it is where the
  // substage name belongs (#171, see below) and where a transient connection banner stays readable on
  // every tab. `onLeave` adds the leave affordance to the persistent team strip.
  headerSlot?: ReactNode;
  onLeave?: () => void;
  // Launches the QR target scanner (#223). Present only for an active treasure-hunt substage — which is
  // exactly when this board renders — so the affordance is inherently scoped to treasure hunts, never
  // trivia. Absent → no scan button (e.g. a board push that predates the scanner).
  onScan?: () => void;
  // HU-25B: real session ranking rows. When provided the TEAMS tab renders the
  // live RankingLeaderboard; when absent it falls back to the participant's own
  // team card + placeholder other-team cards.
  rankingRows?: readonly RankingRowDto[];
  // Per-session team id used to highlight the participant's own team in the
  // ranking. Must match the `teamId` field carried by `RankingRowDto` rows.
  ownTeamId?: string;
  // HU-25B: set when the ranking fetch failed with no usable snapshot. When present
  // (and there are no rows to show) the TEAMS tab renders an error card + Retry
  // instead of the placeholder sample cards, so a failed load isn't mistaken for
  // "no standings yet". Ignored once rows are available (a stale snapshot still renders).
  rankingError?: TimerSnapshotError | null;
  // Retry trigger for a failed ranking fetch — wired to `useRanking`'s `refetch`.
  onRetryRanking?: () => void;
};

// Display labels for the segmented tabs. Keyed by the tab discriminant so the state logic keeps using
// the English enum values while the UI reads in Spanish.
const TAB_LABELS: Record<'map' | 'clues' | 'teams', string> = {
  map: 'MAPA',
  clues: 'PISTAS',
  teams: 'EQUIPOS',
};

export function TreasureHuntBoard({
  teamDisplayName,
  currentScore,
  timerDisplay,
  resolvedTargets,
  totalActiveTargets,
  visibleClues,
  activeTargets = [],
  substageId,
  headerSlot,
  onLeave,
  onScan,
  rankingRows,
  ownTeamId,
  rankingError,
  onRetryRanking,
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
  // Mirror of OperativeClueSurface's substage re-arm: on a substage boundary the incoming substage's
  // visible-on-start clues should read as new, not inherit the prior baseline. Seeded to `substageId`
  // so the first mount stays quiet (reconnect rule).
  const lastSubstageId = useRef<string | null | undefined>(substageId);

  // Reveal-badge state, recomputed from the seen-set baseline whenever the tab or the
  // clue list changes. Kept in state (not derived at render) because the source is a ref,
  // and reading a ref's value during render is disallowed.
  const [hasNewClues, setHasNewClues] = useState(false);

  // Acknowledge (mark current clues seen) whenever the participant is on the CLUES tab;
  // otherwise flag any visible clue not yet in the seen-set as new.
  useEffect(() => {
    // Reconciles the reveal badge against the seen-set ref whenever tab/clues change. The
    // baseline lives in a ref (mount snapshot, mutated on ack), so this derivation can only
    // run in an effect, not at render.
    /* eslint-disable react-hooks/set-state-in-effect */
    if (substageId !== lastSubstageId.current) {
      lastSubstageId.current = substageId;
      seenClueIds.current = new Set();
    }
    if (tab === 'clues') {
      for (const c of visibleClues) seenClueIds.current.add(clueKey(c));
      setHasNewClues(false);
    } else {
      setHasNewClues(visibleClues.some((c) => !seenClueIds.current.has(clueKey(c))));
    }
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [tab, visibleClues, substageId]);

  return (
    <View
      accessibilityLabel={`Tablero de búsqueda del tesoro para ${teamDisplayName}`}
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
            <Text variant="label" muted>BÚSQUEDA DEL TESORO</Text>
          </View>
          <View style={{ alignItems: 'flex-end' }}>
            <Text variant="label" muted>PUNTUACIÓN</Text>
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
                accessibilityLabel={showDot ? 'Nueva pista disponible' : undefined}
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
                  {TAB_LABELS[k]}
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
                  accessibilityLabel={`Targets resueltos ${resolvedTargets} de ${totalActiveTargets}`}
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
                <Text variant="body" muted>Aún no hay pistas.</Text>
              </Card>
            )}
          </Screen>
        ) : rankingRows && ownTeamId ? (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            <RankingLeaderboard rows={rankingRows} ownTeamId={ownTeamId} />
          </Screen>
        ) : rankingError ? (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            <Card style={{ borderColor: colors.signalCritical, borderWidth: 1 }}>
              <View style={{ gap: spacing.sm, alignItems: 'center', paddingVertical: spacing.md }}>
                <Text
                  variant="body"
                  style={{ color: colors.signalCritical, textAlign: 'center' }}
                >
                  {rankingErrorCopy(rankingError)}
                </Text>
                {onRetryRanking ? (
                  <Pressable
                    accessibilityRole="button"
                    accessibilityLabel="Reintentar cargar la clasificación"
                    onPress={onRetryRanking}
                    style={{
                      backgroundColor: colors.raisedSurface,
                      borderRadius: radii.control,
                      borderCurve: 'continuous',
                      borderWidth: 1,
                      borderColor: colors.borderSoft,
                      paddingHorizontal: spacing.md,
                      paddingVertical: spacing.xs,
                    }}
                  >
                    <Text variant="label" style={{ color: colors.textInk }}>REINTENTAR</Text>
                  </Pressable>
                ) : null}
              </View>
            </Card>
          </Screen>
        ) : (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            <Card style={{ borderColor: colors.emberAccent, borderWidth: 1.5 }}>
              <View style={{ gap: spacing.xs }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Text variant="title" accent>{teamDisplayName}</Text>
                  <Text variant="label" muted>TU EQUIPO</Text>
                </View>
                <Text
                  style={{ ...typography.headline, fontSize: 22, color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
                >
                  {currentScore}
                </Text>
              </View>
            </Card>
            {PLACEHOLDER_OTHER_TEAMS.map((t) => (
              <Card key={t.name} accessibilityHint="Clasificación de ejemplo — aún no en vivo">
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
                      <Text variant="label" muted>EJEMPLO</Text>
                    </View>
                  </View>
                  <Text variant="body" muted>Clasificación de ejemplo — aún no en vivo</Text>
                </View>
              </Card>
            ))}
          </Screen>
        )}
      </View>

      {/* #223: floating QR-scan launcher, pinned above the team strip so it rides every tab. Only ever
          shown for an active treasure-hunt substage (this board is the treasure-hunt branch), which is
          the AC's "scanning only while a treasure-hunt substage is active". */}
      {onScan ? (
        <Pressable
          testID="treasure-hunt-scan-button"
          accessibilityRole="button"
          accessibilityLabel="Escanea un código QR de target"
          onPress={onScan}
          style={{
            position: 'absolute',
            right: spacing.lg,
            bottom: 92,
            flexDirection: 'row',
            alignItems: 'center',
            gap: spacing.xs,
            backgroundColor: colors.emberAccentStrong,
            borderRadius: radii.pill,
            borderCurve: 'continuous',
            paddingHorizontal: spacing.lg,
            paddingVertical: spacing.sm,
            boxShadow: shadows.card,
          }}
        >
          <Text variant="label" style={{ color: colors.ivoryFog }}>
            ESCANEAR TARGET
          </Text>
        </Pressable>
      ) : null}

      {/* HU-28: the same viewport-pinned arrival toast the trivia surface raises. The Clues tab is
          already the durable home here, so this is toast-only (no chip) — it stays quiet while that
          tab is open and opens it when tapped. */}
      <OperativeClueSurface
        visibleClues={visibleClues}
        home={{ listVisible: tab === 'clues', onOpen: () => setTab('clues') }}
        substageId={substageId}
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
            TU EQUIPO · {teamDisplayName}
          </Text>
          <Text
            variant="body"
            style={{ color: colors.ivoryFog, fontVariant: ['tabular-nums'] }}
            numberOfLines={1}
          >
            PUNTUACIÓN {currentScore}
          </Text>
        </View>
        {onLeave ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Salir del espacio de equipo"
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
            <Text variant="label" style={{ color: colors.emberAccentSoft }}>SALIR</Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}
