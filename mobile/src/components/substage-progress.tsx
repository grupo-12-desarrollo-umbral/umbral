/**
 * Ordered substage progress (#171).
 *
 * Surfaces the whole ordered substage sequence to a live participant so a mixed-play-mode
 * session (e.g. Trivia → Treasure Hunt) is understandable on a phone: the active substage
 * name sits prominently at the top, and — for multi-substage sessions — a compact ordered
 * row of chips shows each substage's play mode and whether it is completed, active or
 * upcoming. Single-substage sessions render only the name (no chips) to avoid visual noise.
 */
import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { colors, radii, spacing } from '@/constants/theme';
import type {
  ParticipantTeamBoardDto,
  SubstageProgressDto,
  SubstageProgressStatus,
  SubstagePlayMode,
} from '@/lib/realtime/team-board-types';

const PLAY_MODE_LABELS: Record<SubstagePlayMode, string> = {
  Trivia: 'Trivia',
  TreasureHunt: 'Treasure Hunt',
};

// Status coloring mirrors the STATE_DOT_COLORS pattern: the active substage is emphasised in
// ember, completed ones read as done (success), and upcoming ones stay muted.
const STATUS_STYLES: Record<
  SubstageProgressStatus,
  { bg: string; border: string; text: string }
> = {
  Active: {
    bg: colors.emberAccent,
    border: colors.emberAccentStrong,
    text: colors.ivoryFog,
  },
  Completed: {
    bg: colors.emberAccentSoft,
    border: colors.emberAccentSoft,
    text: colors.emberAccentStrong,
  },
  Upcoming: {
    bg: colors.raisedSurface,
    border: colors.borderSoft,
    text: colors.textMuted,
  },
};

function playModeLabel(playMode: SubstagePlayMode): string {
  return PLAY_MODE_LABELS[playMode] ?? playMode;
}

function activeTitle(board: ParticipantTeamBoardDto): string | null {
  if (board.activeSubstage?.title) return board.activeSubstage.title;
  const active = board.substages.find((substage) => substage.status === 'Active');
  return active?.title ?? null;
}

function SubstageChip({ substage, position }: { substage: SubstageProgressDto; position: number }) {
  const style = STATUS_STYLES[substage.status];
  const label = playModeLabel(substage.playMode);
  const done = substage.status === 'Completed';

  return (
    <View
      accessibilityRole="text"
      accessibilityState={{ selected: substage.status === 'Active' }}
      accessibilityLabel={`Substage ${position}: ${substage.title}, ${label}, ${substage.status.toLowerCase()}`}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.one,
        backgroundColor: style.bg,
        borderWidth: 1,
        borderColor: style.border,
        borderRadius: radii.pill,
        borderCurve: 'continuous',
        paddingHorizontal: spacing.sm,
        paddingVertical: spacing.one,
      }}
    >
      <Text variant="label" style={{ color: style.text }}>
        {done ? '✓ ' : `${position}· `}
        {label}
      </Text>
    </View>
  );
}

export function SubstageProgress({ board }: { board: ParticipantTeamBoardDto }) {
  const substages = board.substages ?? [];
  const title = activeTitle(board);
  const isMulti = substages.length > 1;

  // Nothing meaningful to show if the board carries neither an active name nor a sequence.
  if (!title && substages.length === 0) return null;

  return (
    <View
      accessibilityLabel="Substage progress"
      style={{
        backgroundColor: colors.panelSurface,
        borderWidth: 1,
        borderColor: colors.borderSoft,
        borderRadius: radii.card,
        borderCurve: 'continuous',
        padding: spacing.md,
        gap: spacing.sm,
      }}
    >
      <View style={{ gap: 2 }}>
        <Text variant="label" muted style={{ textTransform: 'uppercase' }}>
          {isMulti ? 'Now playing' : 'Substage'}
        </Text>
        {title ? (
          <Text variant="headline" numberOfLines={2}>
            {title}
          </Text>
        ) : null}
      </View>

      {isMulti ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
          {substages.map((substage, index) => (
            <SubstageChip
              key={substage.substageSnapshotId}
              substage={substage}
              position={index + 1}
            />
          ))}
        </View>
      ) : null}
    </View>
  );
}
