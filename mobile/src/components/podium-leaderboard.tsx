import { View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import { colors, radii, shadows, spacing } from '@/constants/theme';
import type { RankingRowDto } from '@/lib/realtime/ranking-types';

export type PodiumLeaderboardProps = {
  rows: readonly RankingRowDto[];
  ownTeamId: string;
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

function formatResolutionTime(value: string | null): string {
  if (!value) {
    return '';
  }

  const match = value.match(/^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?$/);
  if (!match) {
    return value;
  }

  const [, daysPart, hoursPart, minutesPart, secondsPart] = match;
  const totalHours = (Number(daysPart ?? '0') * 24) + Number(hoursPart);
  const minutes = Number(minutesPart);
  const seconds = Number(secondsPart);

  if (totalHours > 0) {
    return `${totalHours}h ${minutes}m`;
  }

  if (minutes > 0) {
    return `${minutes}m`;
  }

  return `${seconds}s`;
}

function ListRow({
  row,
  isOwnTeam,
}: {
  row: RankingRowDto;
  isOwnTeam: boolean;
}) {
  return (
    <Card
      style={{
        borderColor: isOwnTeam ? colors.emberAccent : colors.borderSoft,
        borderWidth: isOwnTeam ? 1.5 : 1,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <View
          style={{
            width: 32,
            height: 32,
            borderRadius: 16,
            backgroundColor: colors.raisedSurface,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text
            variant="label"
            style={{ color: positionColor(row.position), fontVariant: ['tabular-nums'] }}
          >
            {row.position}
          </Text>
        </View>
        <View style={{ flex: 1 }}>
          <Text
            variant="title"
            style={{ color: isOwnTeam ? colors.emberAccentStrong : colors.textInk }}
          >
            {row.teamDisplayName} {isOwnTeam ? '(You)' : ''}
          </Text>
        </View>
        <View style={{ alignItems: 'flex-end' }}>
          <Text
            variant="headline"
            style={{ color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}
          >
            {row.totalScore}
          </Text>
          <Text variant="label" muted>
            {formatResolutionTime(row.resolutionTime)}
          </Text>
        </View>
      </View>
    </Card>
  );
}

/**
 * HU-25B ranking view — Podium Leaderboard. Top-3 podium (2nd left, 1st centre
 * tallest, 3rd right) plus a scrollable list for positions 4+. The participant's
 * own team is highlighted with an ember accent border and text colour.
 *
 * Empty state: trophy emoji + "Standings will appear once the round begins."
 */
export function PodiumLeaderboard({ rows, ownTeamId }: PodiumLeaderboardProps) {
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
          Standings will appear once the round begins.
        </Text>
      </View>
    );
  }

  const topThree = rows.slice(0, 3);
  const rest = rows.slice(3);
  const own = rows.find((r) => r.teamId === ownTeamId);
  const ownInRest = own && own.position > 3;

  // Re-order top three so 2nd is left, 1st centre (tallest), 3rd right
  const podiumOrder = [topThree[1], topThree[0], topThree[2]].filter(Boolean);

  return (
    <View style={{ gap: spacing.lg }}>
      {/* Podium */}
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'flex-end',
          justifyContent: 'center',
          gap: spacing.sm,
          paddingTop: spacing.md,
        }}
      >
        {podiumOrder.map((row) => {
          if (!row) return null;
          const isFirst = row.position === 1;
          const isOwn = row.teamId === ownTeamId;
          const height = isFirst ? 140 : 110;
          return (
            <View key={row.teamId} style={{ alignItems: 'center', gap: spacing.xs }}>
              <View
                style={{
                  width: 92,
                  height,
                  backgroundColor: isOwn ? colors.emberAccentSoft : colors.paperSurface,
                  borderWidth: 2,
                  borderColor: isOwn ? colors.emberAccent : colors.borderSoft,
                  borderRadius: radii.card,
                  borderCurve: 'continuous',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: spacing.xs,
                  boxShadow: isFirst ? shadows.lanternHalo : shadows.card,
                }}
              >
                <Text
                  variant="display"
                  style={{ fontSize: 28, color: positionColor(row.position) }}
                >
                  {row.position === 1 ? '1' : row.position === 2 ? '2' : '3'}
                </Text>
                <Text
                  variant="label"
                  numberOfLines={1}
                  style={{
                    color: isOwn ? colors.emberAccentStrong : colors.textInk,
                    textAlign: 'center',
                    paddingHorizontal: spacing.xs,
                  }}
                >
                  {row.teamDisplayName}
                </Text>
                <Text
                  variant="headline"
                  style={{
                    color: colors.emberAccentStrong,
                    fontVariant: ['tabular-nums'],
                  }}
                >
                  {row.totalScore}
                </Text>
                <Text variant="label" muted numberOfLines={1}>
                  {formatResolutionTime(row.resolutionTime)}
                </Text>
              </View>
              {/* Podium base */}
              <View
                style={{
                  width: 92,
                  height: isFirst ? 24 : 12,
                  backgroundColor: positionColor(row.position),
                  borderRadius: 4,
                  borderCurve: 'continuous',
                  opacity: 0.35,
                }}
              />
            </View>
          );
        })}
      </View>

      {/* Remaining list */}
      <View style={{ gap: spacing.sm }}>
        {rest.map((row) => (
          <ListRow key={row.teamId} row={row} isOwnTeam={row.teamId === ownTeamId} />
        ))}
        {ownInRest ? (
          <ListRow key={own.teamId} row={own} isOwnTeam />
        ) : null}
      </View>
    </View>
  );
}
