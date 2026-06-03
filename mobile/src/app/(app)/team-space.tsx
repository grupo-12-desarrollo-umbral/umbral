import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { useAuth } from '@/lib/auth/use-auth';
import {
  clearReconnectContext,
  loadReconnectContext,
  saveReconnectContext,
} from '@/lib/realtime/reconnect-context';
import type { ReconnectOutcome } from '@/lib/realtime/reconnect-policy';
import { useReconnect } from '@/lib/realtime/use-reconnect';
import type { ReconnectContext } from '@/lib/realtime/sessions-hub-types';
import { colors, spacing } from '@/constants/theme';

function asParam(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? '') : (value ?? '');
}

function parsePositiveInt(value: string): number | null {
  if (!value) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
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
    teamCapacity?: string;
    reason?: string;
  }>();

  const { status, outcome, reconnect, stop, isHubReconnecting } = useReconnect();

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
      const teamCapacity = parsePositiveInt(asParam(params.teamCapacity));

      let resolved: ReconnectContext | null = null;
      if (liveSessionId && teamId && profile?.displayName && teamCapacity) {
        resolved = {
          liveSessionId,
          teamId,
          displayName: profile.displayName,
          teamCapacity,
          token: null,
        };
        await saveReconnectContext(resolved);
      } else {
        resolved = await loadReconnectContext();
      }

      if (!active) return;

      if (!resolved) {
        setPhase('no-context');
        return;
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
        <LiveTeamSpace outcome={outcome} onLeave={leaveToHome} />
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

function LiveTeamSpace({
  outcome,
  onLeave,
}: {
  outcome: Extract<ReconnectOutcome, { kind: 'reconnected' }>;
  onLeave: () => void;
}) {
  const { result } = outcome;

  return (
    <>
      <Panel>
        <View style={{ gap: spacing.sm }}>
          <View
            style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}
          >
            <View
              style={{
                width: 10,
                height: 10,
                borderRadius: 5,
                backgroundColor: colors.signalSuccess,
              }}
            />
            <Text variant="headline">
              {result.isReconnect ? 'Live session resumed' : 'Joined live session'}
            </Text>
          </View>
          <Text variant="body" muted>
            {result.isReconnect
              ? 'We restored you into your team space.'
              : 'You are in your team space.'}
          </Text>
        </View>
      </Panel>

      <Card>
        <View style={{ gap: spacing.sm }}>
          <Text variant="label" muted>
            TEAM
          </Text>
          <Text variant="title">{result.teamDisplayName}</Text>
          <Text variant="label" muted>
            PARTICIPANT
          </Text>
          <Text variant="body">{result.participantDisplayName}</Text>
          <Text variant="label" muted>
            SESSION STATE
          </Text>
          <Text variant="body">{result.sessionState}</Text>
        </View>
      </Card>

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
