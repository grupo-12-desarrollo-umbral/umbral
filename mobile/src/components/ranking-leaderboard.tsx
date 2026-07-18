import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { colors, radii, spacing } from '@/constants/theme';
import type { RankingRowDto } from '@/lib/realtime/ranking-types';
import type { TimerDisplay, TimerTone } from '@/lib/realtime/timer-types';

export type RankingLeaderboardProps = {
  rows: readonly RankingRowDto[];
  ownTeamId: string;
  // Optional whole-mission countdown (`useSessionTimer().missionDisplay`). When
  // provided, a "Mission ends in" bar renders above the standings. Live on the
  // in-session standings; frozen (paused tone) mid-reveal, expired/00:00 once the
  // mission finishes. Omit it where another mission clock already shows (the
  // treasure-hunt board's sticky header already carries the mission deadline).
  missionDisplay?: TimerDisplay | null;
};

const POSITION_COLORS = [
  colors.emberAccentStrong, // 1st
  '#B8A99A',                // 2nd  (warm silver)
  '#C49A6C',                // 3rd  (bronze)
  colors.textMuted,         // rest
];

function positionColor(position: number) {
  return POSITION_COLORS[Math.min(position - 1, POSITION_COLORS.length - 1)];
}

const TIMER_FILL_COLORS: Record<TimerTone, string> = {
  running: colors.emberAccent,
  paused: colors.signalWarning,
  expired: colors.signalCritical,
  unavailable: colors.borderSoft,
};

/** Whole-mission "time left" bar, styled after the question CountdownBar. */
function MissionCountdown({ display }: { display: TimerDisplay }) {
  const pct = Math.min(100, Math.max(0, display.pct));
  const terminal = display.tone === 'expired' || display.tone === 'unavailable';
  return (
    <View style={{ gap: spacing.xs, paddingBottom: spacing.xs }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <Text variant="label" muted style={{ textTransform: 'uppercase' }}>
          La misión termina en
        </Text>
        <Text
          variant="mono"
          style={{ color: terminal ? colors.textMuted : colors.textInk, fontVariant: ['tabular-nums'] }}
        >
          {display.label}
        </Text>
      </View>
      <View
        accessibilityRole="progressbar"
        accessibilityValue={{ min: 0, max: 100, now: pct }}
        accessibilityLabel={`Temporizador de misión: ${display.label}`}
        style={{
          height: 8,
          borderRadius: radii.pill,
          backgroundColor: colors.raisedSurface,
          borderWidth: 1,
          borderColor: colors.borderSoft,
          overflow: 'hidden',
        }}
      >
        <View
          style={{
            width: `${pct}%`,
            height: '100%',
            backgroundColor: TIMER_FILL_COLORS[display.tone],
            opacity: terminal ? 0.5 : 1,
          }}
        />
      </View>
    </View>
  );
}

function LedgerRow({
  row,
  isOwnTeam,
  maxScore,
}: {
  row: RankingRowDto;
  isOwnTeam: boolean;
  maxScore: number;
}) {
  const topThree = row.position <= 3;
  // Bar never collapses to nothing (readability floor); top-3 carry their medal tint.
  const pct = Math.max(8, Math.round((row.totalScore / maxScore) * 100));
  const barColor = topThree ? positionColor(row.position) : colors.emberAccent;

  return (
    <View
      style={{
        gap: spacing.xs,
        backgroundColor: isOwnTeam ? colors.emberAccentSoft : colors.paperSurface,
        borderWidth: isOwnTeam ? 1.5 : 1,
        borderColor: isOwnTeam ? colors.emberAccent : colors.borderSoft,
        borderRadius: radii.card,
        borderCurve: 'continuous',
        paddingVertical: spacing.sm,
        paddingHorizontal: spacing.md,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <View
          style={{
            width: 28,
            height: 28,
            borderRadius: radii.pill,
            backgroundColor: topThree ? positionColor(row.position) : colors.raisedSurface,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text
            variant="label"
            style={{ color: topThree ? colors.ivoryFog : colors.textMuted, fontVariant: ['tabular-nums'] }}
          >
            {row.position}
          </Text>
        </View>
        <Text
          variant="title"
          numberOfLines={1}
          style={{ flex: 1, color: isOwnTeam ? colors.emberAccentStrong : colors.textInk }}
        >
          {row.teamDisplayName} {isOwnTeam ? '(Tú)' : ''}
        </Text>
        <Text
          variant="headline"
          style={{ width: 52, textAlign: 'right', color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
        >
          {row.totalScore}
        </Text>
      </View>

      {/* Proportional score bar */}
      <View
        style={{
          height: 6,
          borderRadius: radii.pill,
          backgroundColor: colors.raisedSurface,
          overflow: 'hidden',
        }}
      >
        <View style={{ width: `${pct}%`, height: '100%', backgroundColor: barColor }} />
      </View>
    </View>
  );
}

/**
 * HU-25B ranking view — the standings ledger. A dense ranked list where every team
 * is one row (rank badge, name, resolution time, score) over a proportional ember
 * score bar; the top three carry their medal tint. Scales cleanly to a full field
 * of teams. The participant's own team is highlighted with an ember border + text.
 * When `missionDisplay` is supplied, a "Mission ends in" countdown leads the list.
 *
 * Empty state: trophy emoji + "Standings will appear once the round begins."
 */
export function RankingLeaderboard({ rows, ownTeamId, missionDisplay }: RankingLeaderboardProps) {
  if (rows.length === 0) {
    return (
      <View
        style={{
          paddingVertical: spacing.xl,
          alignItems: 'center',
          gap: spacing.sm,
        }}
      >
        <Text variant="display" style={{ fontSize: 40 }}>
          🏆
        </Text>
        <Text variant="title" muted>
          La clasificación aparecerá cuando comience la ronda.
        </Text>
      </View>
    );
  }

  const maxScore = Math.max(...rows.map((r) => r.totalScore), 1);

  return (
    <View style={{ gap: spacing.sm }}>
      {missionDisplay ? <MissionCountdown display={missionDisplay} /> : null}
      {rows.map((row) => (
        <LedgerRow
          key={row.teamId}
          row={row}
          isOwnTeam={row.teamId === ownTeamId}
          maxScore={maxScore}
        />
      ))}
    </View>
  );
}
