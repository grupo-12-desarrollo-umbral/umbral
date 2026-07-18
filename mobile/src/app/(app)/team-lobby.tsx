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
  } = useTeamJoin();
  const [joiningTeamId, setJoiningTeamId] = useState<string | null>(null);
  const isJoining = joinStatus === 'joining';

  // Synchronous selection owner. React state (`joinStatus`/`joiningTeamId`) lags a render behind, so a
  // fast double tap — or a tap on a *different* team — can enter `handleTeamSelect` twice before either
  // render lands. This ref is claimed on the first line of the handler and gates every later selection,
  // so exactly one selection owns the join, its side effects, and navigation. The hook-level guard
  // (use-team-join) still collapses the POST; this ref additionally protects the caller's own side
  // effects (reconnect-context write, haptic, `router.replace`) and cross-team taps.
  const selectionRef = useRef<{ runtimeTeamId: string; referenceTeamId: string } | null>(null);
  // A late completion must not persist or navigate after the screen has unmounted.
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

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
    // Claim the selection synchronously before reading any render-lagging state: a second tap (same or
    // different team) that lands before the next render sees an owned ref and is ignored.
    if (selectionRef.current || !liveSessionId) return;
    const selection = { runtimeTeamId, referenceTeamId };
    selectionRef.current = selection;
    // No pre-join reset here: an unconditional reset could clear an active hook attempt. The hook's own
    // `join` clears the previous terminal banner (`setOutcome(null)`) as the new attempt starts.
    setJoiningTeamId(runtimeTeamId);
    const joinResult = await join(sessionCode, runtimeTeamId);

    // Only the still-owning selection on a mounted screen may run post-join side effects. A late
    // completion whose selection was invalidated (unmounted) does nothing.
    if (!mountedRef.current || selectionRef.current !== selection) return;

    if (joinResult.kind === 'unauthorized') {
      selectionRef.current = null;
      signOut();
      return;
    }

    if (joinResult.kind !== 'joined') {
      selectionRef.current = null;
      setJoiningTeamId(null);
      return;
    }

    const reconnectContext = buildReconnectContext({
      liveSessionId,
      teamId: referenceTeamId,
      displayName: profile?.displayName ?? '',
    });
    if (reconnectContext) {
      // The join already succeeded server-side; a rejected persist must not strand the player on the
      // lobby — `selectionRef` stays owned until navigation unmounts the screen, so a throw here would
      // lock out every later tap forever. Reconnect context is recovery defense-in-depth, so degrade to
      // entering the game without a persisted context rather than deadlocking the screen.
      try {
        await saveReconnectContext(reconnectContext);
      } catch {
        // Best-effort persist; proceed to gameplay regardless.
      }
    }

    // Re-check ownership after the awaited persist: nothing runs if the screen unmounted meanwhile.
    if (!mountedRef.current || selectionRef.current !== selection) return;

    fireHaptic('success');
    // Success retains the selection ownership; navigation unmounts the screen. This is the only path
    // that writes reconnect context, fires the success haptic, and navigates — exactly once.
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
          <Text variant="headline">Selecciona tu equipo</Text>
          <Text variant="body" muted>
            Sesión {sessionCode || '------'}. Los equipos marcados como
            bloqueados no están disponibles para ti.
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
                label="Reintentar"
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
              label="Actualizar equipos"
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
                ? 'Tu equipo'
                : team.joinState === 'locked'
                  ? 'Bloqueado'
                  : 'Disponible';
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
                        Bloqueado
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
        label="Atrás"
        variant="secondary"
        onPress={() => router.back()}
        disabled={isJoining}
      />
    </Screen>
  );
}
