/**
 * Ranking view UI prototype — Podium Leaderboard with 16 teams.
 *
 * Dev-only preview of session standings. Sticky podium for top 3, scrollable
 * list for remaining teams. Own team highlighted across both sections.
 */
import { ScrollView, View } from 'react-native';
import { Text } from '@/components/ui/text';
import { Card } from '@/components/ui/card';
import { colors, radii, shadows, spacing } from '@/constants/theme';

// ------------------------------------------------------------------
// Stub data — 16 teams, own team at position 3
// ------------------------------------------------------------------

type RankingRow = {
  teamId: string;
  teamName: string;
  position: number;
  totalScore: number;
  resolutionTime?: string;
};

const OWN_TEAM_ID = 'team-lantern-bearers';

const STUB_ROWS: RankingRow[] = [
  { teamId: 't1', teamName: 'Compass Rose', position: 1, totalScore: 580, resolutionTime: '00:35:12' },
  { teamId: 't2', teamName: 'Ember Foxes', position: 2, totalScore: 555, resolutionTime: '00:38:45' },
  { teamId: OWN_TEAM_ID, teamName: 'Lantern Bearers', position: 3, totalScore: 520, resolutionTime: '00:42:10' },
  { teamId: 't4', teamName: 'Silver Winds', position: 4, totalScore: 495, resolutionTime: '00:46:33' },
  { teamId: 't5', teamName: 'Parchment Moths', position: 5, totalScore: 470, resolutionTime: '00:50:18' },
  { teamId: 't6', teamName: 'Night Herons', position: 6, totalScore: 445, resolutionTime: '00:54:07' },
  { teamId: 't7', teamName: 'Dawn Chasers', position: 7, totalScore: 420, resolutionTime: '00:58:22' },
  { teamId: 't8', teamName: 'Iron Owls', position: 8, totalScore: 395, resolutionTime: '01:02:41' },
  { teamId: 't9', teamName: 'Brass Falcons', position: 9, totalScore: 370, resolutionTime: '01:06:55' },
  { teamId: 't10', teamName: 'Stone Merchants', position: 10, totalScore: 345, resolutionTime: '01:11:30' },
  { teamId: 't11', teamName: 'Velvet Spiders', position: 11, totalScore: 320, resolutionTime: '01:15:48' },
  { teamId: 't12', teamName: 'Crimson Kites', position: 12, totalScore: 295, resolutionTime: '01:20:15' },
  { teamId: 't13', teamName: 'Glass Serpents', position: 13, totalScore: 270, resolutionTime: '01:24:42' },
  { teamId: 't14', teamName: 'Copper Ravens', position: 14, totalScore: 245, resolutionTime: '01:29:18' },
  { teamId: 't15', teamName: 'Moss Hounds', position: 15, totalScore: 220, resolutionTime: '01:33:55' },
  { teamId: 't16', teamName: 'Sand Foxes', position: 16, totalScore: 195, resolutionTime: '01:38:27' },
];

// ------------------------------------------------------------------
// Shared helpers
// ------------------------------------------------------------------

function isOwnTeam(teamId: string) {
  return teamId === OWN_TEAM_ID;
}

const POSITION_COLORS = [
  colors.emberAccentStrong, // 1st
  '#B8A99A',                // 2nd  (warm silver)
  '#C49A6C',                // 3rd  (bronze)
  colors.textMuted,         // rest
];

function positionColor(position: number) {
  return POSITION_COLORS[Math.min(position - 1, POSITION_COLORS.length - 1)];
}

// ------------------------------------------------------------------
// Podium Leaderboard
// ------------------------------------------------------------------

function PodiumLeaderboard({ rows }: { rows: RankingRow[] }) {
  if (rows.length === 0) {
    return (
      <View style={{ paddingVertical: spacing.xl, alignItems: 'center', gap: spacing.sm }}>
        <Text variant="display" style={{ fontSize: 40 }}>🏆</Text>
        <Text variant="title" muted>Standings will appear once the round begins.</Text>
      </View>
    );
  }

  const topThree = rows.slice(0, 3);
  const rest = rows.slice(3);
  const own = rows.find((r) => isOwnTeam(r.teamId));
  const ownInRest = own && own.position > 3;

  // Re-order top three so 2nd is left, 1st center (tallest), 3rd right
  const podiumOrder = [topThree[1], topThree[0], topThree[2]].filter(Boolean);

  return (
    <View style={{ gap: spacing.lg }}>
      {/* Podium */}
      <View style={{ flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'center', gap: spacing.sm, paddingTop: spacing.md }}>
        {podiumOrder.map((row) => {
          if (!row) return null;
          const isFirst = row.position === 1;
          const height = isFirst ? 140 : 110;
          return (
            <View key={row.teamId} style={{ alignItems: 'center', gap: spacing.xs }}>
              <View
                style={{
                  width: 92,
                  height,
                  backgroundColor: isOwnTeam(row.teamId) ? colors.emberAccentSoft : colors.paperSurface,
                  borderWidth: 2,
                  borderColor: isOwnTeam(row.teamId) ? colors.emberAccent : colors.borderSoft,
                  borderRadius: radii.card,
                  borderCurve: 'continuous',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: spacing.xs,
                  boxShadow: isFirst ? shadows.lanternHalo : shadows.card,
                }}
              >
                <Text variant="display" style={{ fontSize: 28, color: positionColor(row.position) }}>
                  {row.position === 1 ? '1' : row.position === 2 ? '2' : '3'}
                </Text>
                <Text
                  variant="label"
                  numberOfLines={1}
                  style={{
                    color: isOwnTeam(row.teamId) ? colors.emberAccentStrong : colors.textInk,
                    textAlign: 'center',
                    paddingHorizontal: spacing.xs,
                  }}
                >
                  {row.teamName}
                </Text>
                <Text variant="headline" style={{ color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}>
                  {row.totalScore}
                </Text>
                <Text variant="label" muted numberOfLines={1}>
                  {row.resolutionTime}
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
          <ListRow key={row.teamId} row={row} />
        ))}
        {ownInRest ? <ListRow key={own.teamId} row={own} highlighted /> : null}
      </View>
    </View>
  );
}

function ListRow({ row, highlighted }: { row: RankingRow; highlighted?: boolean }) {
  return (
    <Card style={{ borderColor: highlighted ? colors.emberAccent : colors.borderSoft, borderWidth: highlighted ? 1.5 : 1 }}>
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
          <Text variant="label" style={{ color: positionColor(row.position), fontVariant: ['tabular-nums'] }}>
            {row.position}
          </Text>
        </View>
        <View style={{ flex: 1 }}>
          <Text
            variant="title"
            style={{ color: isOwnTeam(row.teamId) ? colors.emberAccentStrong : colors.textInk }}
          >
            {row.teamName} {isOwnTeam(row.teamId) ? '(You)' : ''}
          </Text>
        </View>
        <View style={{ alignItems: 'flex-end' }}>
          <Text variant="headline" style={{ color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}>
            {row.totalScore}
          </Text>
          <Text variant="label" muted>{row.resolutionTime}</Text>
        </View>
      </View>
    </Card>
  );
}

// ------------------------------------------------------------------
// Screen
// ------------------------------------------------------------------

export default function RankingPrototypeScreen() {
  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog }}>
      {/* Sticky header */}
      <View
        style={{
          paddingTop: 52,
          paddingHorizontal: spacing.lg,
          paddingBottom: spacing.sm,
          backgroundColor: colors.panelSurface,
          borderBottomWidth: 1,
          borderColor: colors.borderSoft,
        }}
      >
        <Text variant="label" muted>PROTOTYPE · RANKING VIEW (16 TEAMS)</Text>
      </View>

      {/* Content */}
      <ScrollView style={{ flex: 1 }} contentContainerStyle={{ padding: spacing.lg, paddingBottom: 80 }}>
        <PodiumLeaderboard rows={STUB_ROWS} />
      </ScrollView>
    </View>
  );
}
