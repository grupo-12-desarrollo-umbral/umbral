import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { evaluateTeamLobbyState } from '@/lib/membership/team-lobby-state';
import { useTeamJoin } from '@/lib/membership/use-team-join';
import { useTeamLobby } from '@/lib/membership/use-team-lobby';
import { useAuth } from '@/lib/auth/use-auth';
import { saveReconnectContext } from '@/lib/realtime/reconnect-context';
import { buildReconnectContext } from '@/lib/realtime/reconnect-context-resolution';
import { colors, spacing } from '@/constants/theme';

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

export default function TeamLobbyScreen() {
  const router = useRouter();
  const { profile, signOut } = useAuth();
  const params = useLocalSearchParams<{ sessionCode?: string }>();
  const sessionCode = asParam(params.sessionCode);

  const { status: lobbyStatus, liveSessionId, teams, failure, errorMessage, load } =
    useTeamLobby(sessionCode);
  const {
    status: joinStatus,
    outcome: joinOutcome,
    join,
    reset: resetJoin,
  } = useTeamJoin();
  const [joiningTeamId, setJoiningTeamId] = useState<string | null>(null);
  const isJoining = joinStatus === 'joining';

  const banner =
    joinOutcome?.kind === 'failed'
      ? joinOutcome.message
      : null;

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (failure?.kind === 'unauthorized') {
      signOut();
    }
  }, [failure, signOut]);

  const prevBannerRef = useRef<string | null>(null);
  useEffect(() => {
    if (banner && banner !== prevBannerRef.current) {
      fireHaptic('error');
    }
    prevBannerRef.current = banner;
  }, [banner]);

  // Two distinct ids per team: the runtime id targets the self-join route; downstream gameplay and
  // reconnect use the reference/catalog id that session operations stores on the attached team.
  async function handleTeamSelect(runtimeTeamId: string, referenceTeamId: string) {
    if (isJoining || !liveSessionId) return;
    resetJoin();
    setJoiningTeamId(runtimeTeamId);
    const joinResult = await join(sessionCode, runtimeTeamId);

    if (joinResult.kind === 'unauthorized') {
      signOut();
      return;
    }

    if (joinResult.kind !== 'joined') {
      setJoiningTeamId(null);
      return;
    }

    const reconnectContext = buildReconnectContext({
      liveSessionId,
      teamId: referenceTeamId,
      displayName: profile?.displayName ?? '',
    });
    if (reconnectContext) {
      await saveReconnectContext(reconnectContext);
    }

    fireHaptic('success');
    router.replace({
      pathname: '/(app)/team-space',
      params: {
        liveSessionId,
        teamId: referenceTeamId,
      },
    } as Href);
  }

  const isLoadingTeams = lobbyStatus === 'idle' || lobbyStatus === 'loading';
  const collectionState = evaluateTeamLobbyState(teams);

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingVertical: spacing.lg }}>
        <BrandMark size="lg" />
      </View>

      <Panel>
        <View style={{ gap: spacing.xs }}>
          <Text variant="headline">Select your team</Text>
          <Text variant="body" muted>
            Session {sessionCode || '------'}. Teams marked locked are
            unavailable to you.
          </Text>
        </View>
      </Panel>

      {isLoadingTeams ? (
        <View style={{ alignItems: 'center', paddingVertical: spacing.xl }}>
          <ActivityIndicator size="large" color={colors.emberAccent} />
        </View>
      ) : lobbyStatus === 'error' ? (
        failure?.kind === 'unauthorized' ? (
          <View style={{ alignItems: 'center', paddingVertical: spacing.xl }}>
            <ActivityIndicator size="large" color={colors.emberAccent} />
          </View>
        ) : (
          <Panel>
            <View style={{ gap: spacing.sm }}>
              <Text
                variant="body"
                style={{ color: colors.signalCritical, textAlign: 'center' }}
              >
                {errorMessage}
              </Text>
              <Button
                label="Try again"
                variant="secondary"
                onPress={() => {
                  void load();
                }}
                disabled={isJoining}
              />
            </View>
          </Panel>
        )
      ) : collectionState.kind === 'empty' ? (
        <Panel>
          <View style={{ gap: spacing.sm }}>
            <Text variant="body" muted style={{ textAlign: 'center' }}>
              {collectionState.message}
            </Text>
            <Button
              label="Refresh teams"
              variant="secondary"
              onPress={() => {
                void load();
              }}
              disabled={isJoining}
            />
          </View>
        </Panel>
      ) : (
        <View style={{ gap: spacing.md }}>
          {collectionState.kind === 'assigned-team-unavailable' ? (
            <Panel>
              <Text
                variant="body"
                style={{ color: colors.signalCritical, textAlign: 'center' }}
              >
                {collectionState.message}
              </Text>
            </Panel>
          ) : null}

          {teams.map((team) => {
            const isThisJoining = joiningTeamId === team.teamId && isJoining;
            const isLocked = team.joinState === 'locked';
            const statusLabel =
              team.joinState === 'mine'
                ? 'Your team'
                : team.joinState === 'locked'
                  ? 'Locked'
                  : 'Joinable';
            return (
              <Pressable
                key={team.teamId}
                onPress={() => handleTeamSelect(team.teamId, team.referenceTeamId ?? team.teamId)}
                disabled={isJoining || isLocked}
                style={({ pressed }) => ({
                  opacity: isLocked ? 0.45 : pressed && !isJoining ? 0.7 : 1,
                })}
              >
                <Card>
                  <View
                    style={{
                      flexDirection: 'row',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <View style={{ gap: 2 }}>
                      <Text variant="title">{team.displayName}</Text>
                      <Text variant="label" muted>
                        {statusLabel}
                      </Text>
                    </View>
                    {isThisJoining ? (
                      <ActivityIndicator size="small" color={colors.emberAccent} />
                    ) : isLocked ? (
                      <Text
                        variant="label"
                        style={{ color: colors.signalCritical }}
                      >
                        Locked
                      </Text>
                    ) : null}
                  </View>
                </Card>
              </Pressable>
            );
          })}
        </View>
      )}

      {banner ? (
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {banner}
        </Text>
      ) : null}

      <Button
        label="Back"
        variant="secondary"
        onPress={() => router.back()}
        disabled={isJoining}
      />
    </Screen>
  );
}
