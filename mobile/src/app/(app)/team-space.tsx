import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
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
import type { TimerSnapshotError } from '@/lib/api/sessions';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useSubmitAnswer } from '@/lib/realtime/use-submit-answer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';
import { TreasureHuntBoard } from '@/components/treasure-hunt-board';
import {
  OperativeCluePortalHost,
  OperativeClueSurface,
} from '@/components/operative-clue-surface';
import { SubstageProgress } from '@/components/substage-progress';
import { SubstageCountdown } from '@/components/substage-countdown';
import { targetProgress } from '@/lib/realtime/team-board-types';
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
 * Copy for a failed team-board fetch (HU-23). Without this the screen would fall
 * back to the trivia surface on any board error — indistinguishable from "no
 * active substage" — so a failing (e.g. 401/403/offline) board fetch is surfaced
 * instead of silently masked.
 */
function boardErrorCopy(error: TimerSnapshotError): string {
  switch (error) {
    case 'network-error':
      return "Couldn't reach the live board — check your connection.";
    case 'unauthorized':
      return 'Your session expired — the team board couldn’t load.';
    case 'forbidden':
      return "You don't have access to this team's board.";
    case 'not-found':
      return "This session's board isn't available.";
    case 'timer-unavailable':
      return "The board isn't ready yet — hang tight.";
    default:
      return "Couldn't load the team board.";
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

  const isLive = status === 'reconnected' && outcome?.kind === 'reconnected';

  // Shared chrome. `LiveTeamSpace` owns its own container (the treasure-hunt board is a full-screen
  // surface and must not be nested in a ScrollView), so the chrome is handed to it rather than
  // wrapped around it: the brand mark only suits the scrolling trivia surface, while the transient
  // connection banner has to reach both branches.
  const brandMark = (
    <View style={{ alignItems: 'center', paddingVertical: spacing.lg }}>
      <BrandMark size="lg" />
    </View>
  );
  const reconnectingBanner = isHubReconnecting ? (
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
  ) : null;

  return (
    // Host the viewport-pinned operative-clue toast overlay as a sibling of the play surface
    // (HU-28 B2, design doc decision 2) so it stays fixed while the content below it scrolls.
    <OperativeCluePortalHost>
      {!isLoading && isLive ? (
        <LiveTeamSpace
          outcome={outcome}
          onLeave={leaveToHome}
          client={client}
          reconnectNonce={reconnectNonce}
          referenceTeamId={context?.teamId ?? outcome.result.teamId}
          token={context?.token}
          brandMark={brandMark}
          banner={reconnectingBanner}
        />
      ) : (
        <Screen contentContainerStyle={{ gap: spacing.md }}>
          {brandMark}
          {reconnectingBanner}

          {isLoading ? (
            <View style={{ alignItems: 'center', paddingVertical: spacing.xl, gap: spacing.md }}>
              <ActivityIndicator size="large" color={colors.emberAccent} />
              <Text variant="body" muted>
                Restoring your team space…
              </Text>
            </View>
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
      )}
    </OperativeCluePortalHost>
  );
}

export function LiveTeamSpace({
  outcome,
  onLeave,
  client,
  reconnectNonce,
  referenceTeamId,
  token,
  brandMark,
  banner,
}: {
  outcome: Extract<ReconnectOutcome, { kind: 'reconnected' }>;
  onLeave: () => void;
  client: SessionsHubClient;
  reconnectNonce: number;
  referenceTeamId: string;
  token?: string | null;
  // Screen chrome, injected because the play-mode branch below picks the container: only this
  // component knows the play mode (it owns the board fetch), and the two modes need different roots.
  brandMark?: ReactNode;
  banner?: ReactNode;
}) {
  const { result } = outcome;
  const [teamsOpen, setTeamsOpen] = useState(false);
  // A QuestionClosed for the displayed question bumps this to re-fetch the timer snapshot and reconcile.
  const [resyncNonce, setResyncNonce] = useState(0);
  const requestResync = useCallback(() => setResyncNonce(n => n + 1), []);
  const {
    display,
    activeQuestion,
    sessionState: snapshotSessionState,
    pregameSecondsLeft,
    snapshotVersion,
  } = useSessionTimer({
    client,
    liveSessionId: result.liveSessionId,
    teamId: referenceTeamId,
    token,
    isReconnected: true,
    reconnectNonce,
    resyncNonce,
  });
  const { view, sessionState, isQuestionClosed } = useActiveQuestion({
    client,
    liveSessionId: result.liveSessionId,
    isReconnected: true,
    reconnectNonce,
    snapshotVersion,
    requestResync,
    snapshotActiveQuestion: activeQuestion,
    snapshotSessionState: snapshotSessionState ?? result.sessionState,
  });
  // HU-23 team board. Its `activeSubstage.playMode` is the play-mode branch key; the
  // treasure-hunt branch renders the live board, everything else keeps the trivia surface.
  const { board, error: boardError } = useTeamBoard({
    client,
    liveSessionId: result.liveSessionId,
    teamId: referenceTeamId,
    token,
    isReconnected: true,
    reconnectNonce,
    // Re-fetch the board when the session state advances (e.g. Preparing → Active on Start).
    sessionState: snapshotSessionState,
  });
  const playMode = board?.activeSubstage?.playMode;
  // Only surface an error while it actually masks the board: a still-good board
  // kept from an earlier fetch (a failed re-fetch leaves `board` intact) renders
  // normally; a failed first fetch (`board` null) would otherwise fall silently
  // to the trivia surface.
  const maskedBoardError = board ? null : boardError;
  const score = 0;
  const teamMembers = [result.participantDisplayName];

  const activeQuestionProps = view.kind === 'active'
    ? {
        triviaSubstageSnapshotId: view.question.triviaSubstageSnapshotId,
        questionSequenceOrder: view.question.sequenceOrder,
      }
    : { triviaSubstageSnapshotId: '', questionSequenceOrder: 0 };

  const submitHook = useSubmitAnswer({
    liveSessionId: result.liveSessionId,
    teamId: referenceTeamId,
    triviaSubstageSnapshotId: activeQuestionProps.triviaSubstageSnapshotId,
    questionSequenceOrder: activeQuestionProps.questionSequenceOrder,
    token,
  });
  const otherTeams = [
    { name: 'Ember Owls', members: ['Ari', 'Sol'] },
    { name: 'Parchment Moths', members: ['Mira', 'Jules'] },
  ];

  if (playMode === 'TreasureHunt' && board) {
    const progress = targetProgress(board.activeSubstage);
    // The board takes the whole screen. It was previously boxed at a hard-coded 640px inside the
    // outer Screen's ScrollView, which both cropped it on most devices and put its Clues/Teams
    // ScrollViews inside a same-direction parent — a combination that leaves a long clue list barely
    // scrollable. Given the viewport it sizes itself, and its body is the only vertical scroller.
    return (
      <TreasureHuntBoard
        teamDisplayName={board.teamDisplayName}
        currentScore={board.currentScore}
        timerDisplay={display}
        resolvedTargets={progress.resolved}
        totalActiveTargets={progress.total}
        visibleClues={board.visibleClues}
        activeTargets={board.activeTargets ?? []}
        headerSlot={
          <>
            {banner}
            <SubstageProgress board={board} />
          </>
        }
        onLeave={onLeave}
      />
    );
  }

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      {brandMark}
      {banner}

      {maskedBoardError ? (
        <Panel>
          <Text
            variant="body"
            style={{ color: colors.signalCritical, textAlign: 'center' }}
          >
            {boardErrorCopy(maskedBoardError)}
          </Text>
        </Panel>
      ) : null}

      {board ? <SubstageProgress board={board} /> : null}

      {/* Trivia pre-game 5s countdown (backend emits it only for a trivia substage, so it surfaces on
          this play surface, never the parked treasure-hunt board). Off the main timer — see useSessionTimer.
          Hidden once a question is active so the countdown never overlaps the live question. */}
      {pregameSecondsLeft != null && view.kind !== 'active' ? (
        <SubstageCountdown secondsLeft={pregameSecondsLeft} />
      ) : null}

      <View style={{ marginHorizontal: -spacing.lg }}>
        {view.kind === 'active' ? (
          <ActiveQuestionStage
            question={view.question}
            sessionState={sessionState}
            score={score}
            timerDisplay={display}
            selectedOptionSequenceOrder={submitHook.selectedOptionSequenceOrder}
            isSubmitting={submitHook.isSubmitting}
            isLocked={submitHook.isLocked}
            isClosed={isQuestionClosed}
            rejection={submitHook.rejection}
            onSelectOption={submitHook.selectOption}
            onSubmit={submitHook.submit}
            onDismissRejection={submitHook.clearRejection}
          />
        ) : (
          <View style={{ alignSelf: 'stretch', backgroundColor: colors.ivoryFog }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={score} />
            <QuestionEmptyState kind={view.kind} sessionState={sessionState} />
          </View>
        )}
      </View>

      {/* HU-28 B2: the trivia surface has no Clues tab, so every clue kind (operative, substage-initial,
          scheduled/target) lands in the collapsed CLUES chip (durable store + dot) + arrival toast, fed
          from the live board. Suppress the toast during pre-game countdown so it doesn't overlap. */}
      {board ? <OperativeClueSurface visibleClues={board.visibleClues} suppressToast={pregameSecondsLeft != null} /> : null}

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
    </Screen>
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
