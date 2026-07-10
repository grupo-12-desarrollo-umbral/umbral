/**
 * Treasure-hunt participant PLAY surface — chosen design ("Focus Tabs").
 *
 * This is the winner of the GitHub #153 / slice-3 UI prototype: a compact sticky
 * header (substage title + score + timer) over a segmented Map / Clues / Teams
 * body, with a persistent "your team" strip so team identity survives tab
 * switches. The other two explored layouts and the variant switcher have been
 * removed now that this one is picked.
 *
 * Still a dev-only preview on stub data. Before it replaces the live team-space
 * surface it needs: #153 slice 1 (backend target coordinates + runtime/roster
 * contracts) and a real map library — the map here is a labelled stub. Reach it
 * from the participant home (dev-only button).
 */
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';
import { Card } from '@/components/ui/card';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { SessionTimerBar } from '@/components/session-timer-bar';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';
import type { TimerDisplay } from '@/lib/realtime/timer-types';

// --- Stub play-state (in-memory only; the surface is wired to real data later) ---

type Clue = { id: number; title: string; scope: 'team' | 'global'; body: string };
type Team = { name: string; participants: string[] };

type PlayState = {
  substageTitle: string;
  missionName: string;
  score: number;
  target: { name: string; latitude: number; longitude: number; context: string };
  yourTeam: Team;
  otherTeams: Team[];
  clues: Clue[];
  timer: TimerDisplay;
};

const PLAY: PlayState = {
  substageTitle: 'The Cartographer’s Vault',
  missionName: 'Nightfall in the Old Quarter',
  score: 240,
  target: {
    name: 'Brass Astrolabe',
    latitude: -34.6037,
    longitude: -58.3816,
    context: 'Somewhere along the north colonnade of the Plaza Mayor.',
  },
  yourTeam: { name: 'Lantern Bearers', participants: ['You', 'Mara', 'Diego', 'Priya'] },
  otherTeams: [
    { name: 'Compass Rose', participants: ['Ivan', 'Lucía', 'Sam'] },
    { name: 'Ember Foxes', participants: ['Noor', 'Theo', 'Aiko', 'Ben'] },
  ],
  clues: [
    { id: 1, title: 'Clue I', scope: 'global', body: 'Where brass hands once told the tide, the vault keeps its second face.' },
    { id: 2, title: 'Clue II', scope: 'team', body: 'Count the arches from the fountain — your key is the third shadow at dusk.' },
  ],
  timer: { label: '12:47', pct: 63, tone: 'running' },
};

const coord = (lat: number, lng: number) =>
  `${Math.abs(lat).toFixed(4)}°${lat < 0 ? 'S' : 'N'}  ${Math.abs(lng).toFixed(4)}°${lng < 0 ? 'W' : 'E'}`;

// --- Presentational pieces ---

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

function MapStub({ style }: { style?: object }) {
  return (
    <View
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
      <View
        style={{
          position: 'absolute',
          bottom: spacing.sm,
          left: spacing.sm,
          backgroundColor: colors.paperSurface,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          borderWidth: 1,
          borderColor: colors.borderSoft,
          paddingHorizontal: spacing.sm,
          paddingVertical: spacing.one,
        }}
      >
        <Text variant="mono" style={{ fontSize: 12 }}>
          {coord(PLAY.target.latitude, PLAY.target.longitude)}
        </Text>
      </View>
    </View>
  );
}

function ScopeChip({ scope }: { scope: Clue['scope'] }) {
  return (
    <View
      style={{
        backgroundColor: scope === 'global' ? colors.emberAccentSoft : colors.raisedSurface,
        borderRadius: radii.pill,
        borderWidth: 1,
        borderColor: colors.borderSoft,
        paddingHorizontal: spacing.xs,
        paddingVertical: 2,
      }}
    >
      <Text variant="label" style={{ fontSize: 11, color: colors.textInk }}>
        {scope.toUpperCase()}
      </Text>
    </View>
  );
}

function ClueCard({ clue }: { clue: Clue }) {
  return (
    <Card parchment>
      <View style={{ gap: spacing.xs }}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <Text variant="label" muted>{clue.title}</Text>
          <ScopeChip scope={clue.scope} />
        </View>
        <Text variant="mono">{clue.body}</Text>
      </View>
    </Card>
  );
}

// --- Focus Tabs: compact header + one section at a time ---

export default function TreasureHuntPlayScreen() {
  const [tab, setTab] = useState<'map' | 'clues' | 'teams'>('map');
  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog }}>
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
            <Text variant="label" muted>TREASURE HUNT</Text>
            <Text variant="title" numberOfLines={1}>{PLAY.substageTitle}</Text>
          </View>
          <View style={{ alignItems: 'flex-end' }}>
            <Text variant="label" muted>SCORE</Text>
            <Text style={{ ...typography.headline, fontSize: 22, color: colors.emberAccentStrong, fontVariant: ['tabular-nums'] }}>
              {PLAY.score}
            </Text>
          </View>
        </View>

        <SessionTimerBar display={PLAY.timer} />

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
          {(['map', 'clues', 'teams'] as const).map((k) => (
            <Pressable
              key={k}
              onPress={() => setTab(k)}
              style={{
                flex: 1,
                alignItems: 'center',
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
            </Pressable>
          ))}
        </View>
      </View>

      <View style={{ flex: 1 }}>
        {tab === 'map' ? (
          <View style={{ flex: 1, padding: spacing.lg, paddingBottom: 100, gap: spacing.sm }}>
            <MapStub style={{ flex: 1 }} />
            <Card>
              <View style={{ gap: 2 }}>
                <Text variant="label" muted>TARGET</Text>
                <Text variant="title">{PLAY.target.name}</Text>
                <Text variant="body" muted>{PLAY.target.context}</Text>
              </View>
            </Card>
          </View>
        ) : tab === 'clues' ? (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            {PLAY.clues.map((c) => (
              <ClueCard key={c.id} clue={c} />
            ))}
          </Screen>
        ) : (
          <Screen contentContainerStyle={{ gap: spacing.sm, paddingBottom: 100 }}>
            <Card style={{ borderColor: colors.emberAccent, borderWidth: 1.5 }}>
              <View style={{ gap: spacing.xs }}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Text variant="title" accent>{PLAY.yourTeam.name}</Text>
                  <Text variant="label" muted>YOUR TEAM</Text>
                </View>
                {PLAY.yourTeam.participants.map((p) => (
                  <Text key={p} variant="body">{p}</Text>
                ))}
              </View>
            </Card>
            {PLAY.otherTeams.map((t) => (
              <Card key={t.name}>
                <View style={{ gap: spacing.xs }}>
                  <Text variant="title">{t.name}</Text>
                  <Text variant="body" muted>{t.participants.join('  ·  ')}</Text>
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
          YOUR TEAM · {PLAY.yourTeam.name}
        </Text>
        <Text variant="body" style={{ color: colors.ivoryFog }} numberOfLines={1}>
          {PLAY.yourTeam.participants.join('  ·  ')}
        </Text>
      </View>
    </View>
  );
}
