import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { ActiveQuestionStage, ActiveQuestionStageHeader } from '@/components/active-question-stage';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/ui/panel';
import { QuestionEmptyState } from '@/components/question-empty-state';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { useAuth } from '@/lib/auth/use-auth';
import {
  clearReconnectContext,
  loadReconnectContext,
  saveReconnectContext,
} from '@/lib/realtime/reconnect-context';
import { resolveReconnectContext } from '@/lib/realtime/reconnect-context-resolution';
import type { ReconnectOutcome } from '@/lib/realtime/reconnect-policy';
import { useReconnect } from '@/lib/realtime/use-reconnect';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import type { SessionsHubClient } from '@/lib/realtime/sessions-hub';
import type { ReconnectContext } from '@/lib/realtime/sessions-hub-types';
import { colors, radii, spacing } from '@/constants/theme';

function asParam(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? '') : (value ?? '');
}

function fireHaptic(type: 'success' | 'error') {
  if (process.env.EXPO_OS === 'ios') {
    Haptics.notificationAsync(
      type === 'success'
        ? Haptics.NotificationFeedbackType.Success
        : Haptics.NotificationFeedbackType.Error,
    );
  }
}

/**
 * Copy for each denied / error reconnect outcome. The backend rejects via
 * `HubException` (translated to a discriminated union by `reconnect-policy`);
 * the screen only renders that vocabulary — it invents no access rules.
 * `unauthorized` is omitted: the hook signs the participant out for that case.
 */
function deniedCopy(outcome: ReconnectOutcome): string | null {
  switch (outcome.kind) {
    case 'forbidden-late-join':
      return "The session has moved on — late join isn't allowed.";
    case 'invalid-session-state':
      return "This session isn't accepting participants right now.";
    case 'lost-access':
      return 'You no longer have access to this team.';
    case 'already-connected':
      return "You're already connected on another device.";
    case 'wrong-team':
      return "You're assigned to a different team — head back to the lobby to rejoin.";
    case 'network-error':
      return "Couldn't reach your live session. Check your connection and try again.";
    case 'unauthorized':
      return 'Your session expired. Signing you out…';
    case 'error':
      return 'Something went wrong restoring your team space.';
    default:
      return null;
  }
}

/**
 * Live team-space (HU-07B). On mount it resolves the reconnect context — from the
 * HU-07A lobby route params on a fresh join, or from persisted state on a resume —
 * and invokes the session-operations hub's `ReconnectAsync`. Final admission,
 * late-join vs. reconnect rules and runtime restoration are owned by the backend;
 * the client only renders the verified result DTO or the mapped denied state.
 */
export default function TeamSpaceScreen() {
  const router = useRouter();
  const { profile } = useAuth();
  const params = useLocalSearchParams<{
    liveSessionId?: string;
    teamId?: string;
    reason?: string;
  }>();

  const { status, outcome, reconnect, stop, isHubReconnecting, client, reconnectNonce } = useReconnect();

  const [context, setContext] = useState<ReconnectContext | null>(null);
  const [phase, setPhase] = useState<'resolving' | 'ready' | 'no-context'>(
    'resolving',
  );
  const startedRef = useRef(false);

  // Resolve the reconnect inputs once, then drive the hub invoke. Route params
  // are captured from the first render (a fresh lobby hand-off); a resume has no
  // params and falls back to the persisted context.
  useEffect(() => {
    let active = true;

    async function run() {
      const liveSessionId = asParam(params.liveSessionId);
      const teamId = asParam(params.teamId);
      const persisted = await loadReconnectContext();
      const resolved = resolveReconnectContext(
        {
          liveSessionId,
          teamId,
          displayName: profile?.displayName ?? '',
        },
        persisted,
      );

      if (!active) return;

      if (!resolved) {
        setPhase('no-context');
        return;
      }

      if (
        resolved.liveSessionId === liveSessionId &&
        resolved.teamId === teamId &&
        resolved.displayName === (profile?.displayName ?? '')
      ) {
        await saveReconnectContext(resolved);
      }

      setContext(resolved);
      setPhase('ready');

      if (!startedRef.current) {
        startedRef.current = true;
        await reconnect(resolved);
      }
    }

    void run();
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Haptic feedback mirrors the lobby: success on restore, error on any denial.
  useEffect(() => {
    if (status === 'reconnected') fireHaptic('success');
    else if (status === 'denied' || status === 'error') fireHaptic('error');
  }, [status]);

  async function leaveToHome() {
    await stop();
    await clearReconnectContext();
    router.replace('/(app)' as Href);
  }

  async function retry() {
    if (context) await reconnect(context);
  }

  const isLoading =
    phase === 'resolving' ||
    status === 'connecting' ||
    status === 'reconnecting';

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingVertical: spacing.lg }}>
        <BrandMark size="lg" />
      </View>

      {isHubReconnecting ? (
        <Panel>
          <View
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              gap: spacing.xs,
            }}
          >
            <ActivityIndicator size="small" color={colors.signalWarning} />
            <Text variant="body" style={{ color: colors.signalWarning }}>
              Reconnecting…
            </Text>
          </View>
        </Panel>
      ) : null}

      {isLoading ? (
        <View style={{ alignItems: 'center', paddingVertical: spacing.xl, gap: spacing.md }}>
          <ActivityIndicator size="large" color={colors.emberAccent} />
          <Text variant="body" muted>
            Restoring your team space…
          </Text>
        </View>
      ) : status === 'reconnected' && outcome?.kind === 'reconnected' ? (
        <LiveTeamSpace
            outcome={outcome}
            onLeave={leaveToHome}
            client={client}
            reconnectNonce={reconnectNonce}
            referenceTeamId={context?.teamId ?? outcome.result.teamId}
            token={context?.token}
          />
      ) : phase === 'no-context' ? (
        <>
          <Panel>
            <View style={{ gap: spacing.sm }}>
              <Text variant="headline">No active session</Text>
              <Text variant="body" muted>
                There&apos;s no live team space to restore. Join a session to get
                started.
              </Text>
            </View>
          </Panel>
          <Button
            label="Back to home"
            variant="primary"
            onPress={() => router.replace('/(app)' as Href)}
          />
        </>
      ) : outcome ? (
        <DeniedState outcome={outcome} onLeave={leaveToHome} onRetry={retry} />
      ) : null}
    </Screen>
  );
}

export function LiveTeamSpace({
  outcome,
  onLeave,
  client,
  reconnectNonce,
  referenceTeamId,
  token,
}: {
  outcome: Extract<ReconnectOutcome, { kind: 'reconnected' }>;
  onLeave: () => void;
  client: SessionsHubClient;
  reconnectNonce: number;
  referenceTeamId: string;
  token?: string | null;
}) {
  const { result } = outcome;
  const [teamsOpen, setTeamsOpen] = useState(false);
  const { display, activeQuestion, sessionState: snapshotSessionState } = useSessionTimer({
    client,
    liveSessionId: result.liveSessionId,
    teamId: referenceTeamId,
    token,
    isReconnected: true,
    reconnectNonce,
  });
  const { view, sessionState } = useActiveQuestion({
    client,
    liveSessionId: result.liveSessionId,
    isReconnected: true,
    reconnectNonce,
    snapshotActiveQuestion: activeQuestion,
    snapshotSessionState: snapshotSessionState ?? result.sessionState,
  });
  const score = 0;
  const teamMembers = [result.participantDisplayName];
  const otherTeams = [
    { name: 'Ember Owls', members: ['Ari', 'Sol'] },
    { name: 'Parchment Moths', members: ['Mira', 'Jules'] },
  ];

  return (
    <>
      <View style={{ marginHorizontal: -spacing.lg }}>
        {view.kind === 'active' ? (
          <ActiveQuestionStage
            question={view.question}
            sessionState={sessionState}
            score={score}
            timerDisplay={display}
          />
        ) : (
          <View style={{ alignSelf: 'stretch', backgroundColor: colors.ivoryFog }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={score} />
            <QuestionEmptyState kind={view.kind} sessionState={sessionState} />
          </View>
        )}
      </View>

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Open all teams for ${result.teamDisplayName}`}
        onPress={() => setTeamsOpen(true)}
        style={{
          backgroundColor: colors.charcoalRoom,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          padding: spacing.md,
          gap: spacing.xs,
        }}
      >
        <Text
          variant="label"
          style={{ color: colors.textInkNight }}
        >
          {`YOUR TEAM · ${result.teamDisplayName}`}
        </Text>
        <Text muted style={{ color: colors.textMuted }}>
          {teamMembers.join(', ')} · ALL TEAMS ›
        </Text>
      </Pressable>

      {teamsOpen ? (
        <Panel style={{ gap: spacing.md }}>
          <Text variant="headline">ALL TEAMS</Text>
          <View
            style={{
              borderWidth: 1,
              borderColor: colors.emberAccent,
              borderRadius: radii.card,
              borderCurve: 'continuous',
              padding: spacing.md,
              gap: spacing.xs,
            }}
          >
            <Text variant="title">{result.teamDisplayName}</Text>
            <Text muted>{teamMembers.join(', ')}</Text>
          </View>
          {otherTeams.map(team => (
            <View key={team.name} style={{ gap: spacing.xs }}>
              <Text variant="title">{team.name}</Text>
              <Text muted>{team.members.join(', ')}</Text>
            </View>
          ))}
          <Button label="CLOSE" variant="secondary" onPress={() => setTeamsOpen(false)} />
        </Panel>
      ) : null}

      <Button label="Leave team space" variant="secondary" onPress={onLeave} />
    </>
  );
}

function DeniedState({
  outcome,
  onLeave,
  onRetry,
}: {
  outcome: ReconnectOutcome;
  onLeave: () => void;
  onRetry: () => void;
}) {
  const message = deniedCopy(outcome);
  const isRetryable = outcome.kind === 'network-error' || outcome.kind === 'error';
  // `unauthorized` resolves via sign-out in the hook; show the notice only.
  const isUnauthorized = outcome.kind === 'unauthorized';

  return (
    <>
      <Panel>
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {message}
        </Text>
      </Panel>

      {isUnauthorized ? null : (
        <View style={{ gap: spacing.sm }}>
          {isRetryable ? (
            <Button label="Try again" variant="primary" onPress={onRetry} />
          ) : null}
          <Button label="Back to home" variant="secondary" onPress={onLeave} />
        </View>
      )}
    </>
  );
}
