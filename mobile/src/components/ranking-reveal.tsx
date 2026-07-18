import { useEffect } from 'react';
import { View } from 'react-native';
import { Button } from '@/components/ui/button';
import { RankingLeaderboard } from '@/components/ranking-leaderboard';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { rankingErrorCopy } from '@/lib/realtime/ranking-error-copy';
import type { TimerSnapshotError } from '@/lib/api/sessions';
import type { RankingRowDto } from '@/lib/realtime/ranking-types';
import type { TimerDisplay } from '@/lib/realtime/timer-types';
import { colors, spacing } from '@/constants/theme';

export type RankingRevealProps = {
  rows: readonly RankingRowDto[] | undefined;
  ownTeamId: string;
  // The mission is over: the ranking is final and the participant can leave.
  isFinished: boolean;
  // Whole-mission countdown. Shown on the between-substage reveal ("how the teams
  // stand"); suppressed once finished, where the clock has run out anyway.
  missionDisplay?: TimerDisplay | null;
  error: TimerSnapshotError | null;
  onRetry: () => void;
  onRefetch: () => void;
  onLeave: () => void;
};

/**
 * Full-screen substage ranking (D-3), shown for a timed reveal window between substages and again —
 * without a time limit — once the mission finishes. Both states are one component on purpose: a
 * terminal reveal flows straight into `Finished` without unmounting, so the leaderboard never flashes.
 *
 * Outranks the treasure-hunt board and the trivia surface, since the reveal fires in either play mode.
 */
export function RankingReveal({
  rows,
  ownTeamId,
  isFinished,
  missionDisplay,
  error,
  onRetry,
  onRefetch,
  onLeave,
}: RankingRevealProps) {
  // Pull a fresh ranking as the reveal opens. Live `RankingChanged` pushes keep it current from here
  // (`useRanking` stays subscribed), but the last push may predate the scan that just cleared the
  // substage — the reveal is exactly when being a beat stale is most visible.
  useEffect(() => {
    onRefetch();
  }, [onRefetch]);

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingTop: spacing.lg, gap: spacing.xs }}>
        <Text variant="label" muted>
          {isFinished ? 'MISIÓN COMPLETADA' : 'CLASIFICACIÓN DE LA RONDA'}
        </Text>
        <Text variant="headline">
          {isFinished ? 'Clasificación final' : 'Cómo van los equipos'}
        </Text>
      </View>

      {error && !rows ? (
        <View style={{ gap: spacing.sm, alignItems: 'center', paddingVertical: spacing.xl }}>
          <Text variant="body" style={{ color: colors.signalCritical, textAlign: 'center' }}>
            {rankingErrorCopy(error)}
          </Text>
          <Button label="REINTENTAR" variant="secondary" onPress={onRetry} />
        </View>
      ) : isFinished && rows?.length === 0 ? (
        // A mission that ends with nobody on the board returns an empty ranking — routine when
        // MaximumTime expires before any team scores. RankingLeaderboard's own empty state promises
        // standings "once the round begins", which contradicts the MISSION COMPLETE heading above it,
        // so the terminal case gets its own copy instead.
        <View style={{ paddingVertical: spacing.xl, alignItems: 'center', gap: spacing.sm }}>
          <Text variant="display" style={{ fontSize: 40 }}>
            🏁
          </Text>
          <Text variant="title" muted style={{ textAlign: 'center' }}>
            Ningún equipo puntuó antes de que terminara la misión.
          </Text>
        </View>
      ) : (
        // RankingLeaderboard owns the empty state, so a not-yet-loaded snapshot reads as
        // "standings coming" rather than an error.
        <RankingLeaderboard
          rows={rows ?? []}
          ownTeamId={ownTeamId}
          missionDisplay={isFinished ? null : missionDisplay}
        />
      )}

      {isFinished ? (
        <Button label="Salir del espacio de equipo" variant="secondary" onPress={onLeave} />
      ) : null}
    </Screen>
  );
}
