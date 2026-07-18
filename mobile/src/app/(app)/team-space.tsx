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
import { SessionTimerBar } from '@/components/session-timer-bar';
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
import { useRanking } from '@/lib/realtime/use-ranking';
import { usePenaltyToast } from '@/lib/realtime/use-penalty-toast';
import { useRankingReveal } from '@/lib/realtime/use-ranking-reveal';
import { createScoringHubConnection, type ScoringHubClient } from '@/lib/realtime/scoring-hub';
import { TreasureHuntBoard } from '@/components/treasure-hunt-board';
import { RankingLeaderboard } from '@/components/ranking-leaderboard';
import { RankingReveal } from '@/components/ranking-reveal';
import { rankingErrorCopy } from '@/lib/realtime/ranking-error-copy';
import { PenaltyToast } from '@/components/penalty-toast';
import { TargetScanner } from '@/components/target-scanner';
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
      return 'La sesión ya avanzó — no se permite unirse tarde.';
    case 'invalid-session-state':
      return 'Esta sesión no está aceptando participantes en este momento.';
    case 'lost-access':
      return 'Ya no tienes acceso a este equipo.';
    case 'already-connected':
      return 'Ya estás conectado en otro dispositivo.';
    case 'wrong-team':
      return 'Estás asignado a un equipo diferente — vuelve a la sala para reingresar.';
    case 'network-error':
      return 'No pudimos conectar con tu sesión en vivo. Revisa tu conexión y reintenta.';
    case 'unauthorized':
      return 'Tu sesión expiró. Cerrando tu sesión…';
    case 'error':
      return 'Algo salió mal al restaurar tu espacio de equipo.';
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
      return 'No pudimos acceder al tablero en vivo — revisa tu conexión.';
    case 'unauthorized':
      return 'Tu sesión expiró — no se pudo cargar el tablero del equipo.';
    case 'forbidden':
      return 'No tienes acceso al tablero de este equipo.';
    case 'not-found':
      return 'El tablero de esta sesión no está disponible.';
    case 'timer-unavailable':
      return 'El tablero aún no está listo — espera un momento.';
    default:
      return 'No se pudo cargar el tablero del equipo.';
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
      let persisted: ReconnectContext | null = null;
      try {
        // The persisted context lives in SecureStore; a rejection here (locked keychain, etc.) must
        // not leave `run()` rejecting unhandled with `phase` stuck on 'resolving' — the screen would
        // show "Restoring your team space…" forever. Treat a read failure as "no context".
        persisted = await loadReconnectContext();
      } catch {
        if (active) setPhase('no-context');
        return;
      }

      const liveSessionId = asParam(params.liveSessionId);
      const teamId = asParam(params.teamId);
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

      // Re-check after the persist await: the screen may have unmounted while it was in flight.
      // Without this, `reconnect()` below would call `client.start()` after the unmount cleanup has
      // already run `client.stop()` (a no-op on the not-yet-started connection), leaving an orphan
      // connection with a live backend presence for a participant who has left the screen.
      if (!active) return;

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
          Reconectando…
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
                Restaurando tu espacio de equipo…
              </Text>
            </View>
          ) : phase === 'no-context' ? (
            <>
              <Panel>
                <View style={{ gap: spacing.sm }}>
                  <Text variant="headline">No hay sesión activa</Text>
                  <Text variant="body" muted>
                    No hay un espacio de equipo en vivo para restaurar. Únete a una
                    sesión para comenzar.
                  </Text>
                </View>
              </Panel>
              <Button
                label="Volver al inicio"
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
  banner?: ReactNode;
}) {
  const { result } = outcome;
  // HU-25B Slice 3: one scoring-hub connection per LiveTeamSpace lifecycle.
  // Started on mount and joined to the session group so `useRanking` receives
  // live `RankingChanged` pushes. Stopped on unmount. Auto-reconnect re-joins
  // the group internally via the scoring-hub module's `onreconnected` handler.
  // Wrapped in try-catch so an unresolvable URL (e.g. test env) degrades to null
  // (ranking stays REST-only) instead of crashing the component during mount.
  const [scoringClient] = useState<ScoringHubClient | null>(() => {
    try {
      return createScoringHubConnection();
    } catch {
      return null;
    }
  });
  const [teamsOpen, setTeamsOpen] = useState(false);
  // #223 QR scanner: open state + a board re-fetch trigger bumped on every accepted scan (there is no
  // board push after a scan resolves, so the target-progress numerator is pulled on demand).
  const [scannerOpen, setScannerOpen] = useState(false);
  const [boardRefreshNonce, setBoardRefreshNonce] = useState(0);
  // A QuestionClosed for the displayed question bumps this to re-fetch the timer snapshot and reconcile.
  const [resyncNonce, setResyncNonce] = useState(0);

  // Manage the scoring-hub connection: start once + join the session group so
  // `RankingChanged` pushes reach `useRanking`. On unmount the effect tears
  // down the connection. Transport-level drops are handled by SignalR's
  // `withAutomaticReconnect` + the scoring-hub internal `onreconnected` handler
  // (which re-joins `joinedSessionId`).
  useEffect(() => {
    if (!scoringClient) {
      return;
    }

    const client = scoringClient;
    let active = true;

    async function setup() {
      try {
        await client.start();
        if (!active) return;
        // The hub validates session-scoped team membership on join, keyed on the cross-context
        // ReferenceTeamId (same id the REST ranking guard uses) — pass referenceTeamId, not the
        // session-scoped board team id.
        await client.joinSessionGroup(result.liveSessionId, referenceTeamId);
      } catch {
        // Scoring hub unavailable — ranking remains REST-only
      }
    }

    void setup();

    return () => {
      active = false;
      void client.stop();
    };
  }, [scoringClient, result.liveSessionId, referenceTeamId]);
  const requestResync = useCallback(() => setResyncNonce(n => n + 1), []);
  const {
    display,
    missionDisplay,
    activeQuestion,
    revealReconciliation,
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
    // …and after an accepted target scan, to advance the resolved-target count (#223).
    refreshNonce: boardRefreshNonce,
  });
  // HU-25B: session ranking snapshot. Fetched in parallel with the board; the TEAMS tab shows the
  // live RankingLeaderboard when rows are available, falling back to the own-team-only placeholder.
  // When `scoringClient` is present (Slice 3), subscribes to live `RankingChanged` push events.
  const {
    snapshot: rankingSnapshot,
    error: rankingError,
    refetch: refetchRanking,
  } = useRanking(
    result.liveSessionId,
    referenceTeamId,
    token,
    scoringClient,
  );
  // A treasure-hunt scan's score is awarded asynchronously and cross-service (scan → outbox → RabbitMQ →
  // RecordScoreEntry → RecalculateRanking → RankingChanged), landing ~1s after the scan POST returns — and
  // the scan response deliberately omits the score. The header normally catches up via the `RankingChanged`
  // SignalR push, but the full-screen scanner modal covers it while scanning, so if that push is missed the
  // header still reads the pre-scan score when the scanner closes (the number only appearing later when the
  // substage-reveal leaderboard force-refetches). A single immediate refetch would race the pipeline and re-read
  // the stale score, so schedule a few staggered REST refetches across the eventual-consistency window as a
  // fallback to the push. Cheap and idempotent (each is a re-GET of the snapshot).
  const rankingCatchUpTimersRef = useRef<ReturnType<typeof setTimeout>[]>([]);
  useEffect(
    () => () => {
      rankingCatchUpTimersRef.current.forEach(clearTimeout);
      rankingCatchUpTimersRef.current = [];
    },
    [],
  );
  // Staggered REST refetches across the eventual-consistency window described above. Shared by the two
  // signals that a treasure-hunt team score has changed: the scanning participant's own accepted scan,
  // and — for every teammate — the board push that advances the resolved-target count (see below).
  const scheduleRankingCatchUp = useCallback(() => {
    rankingCatchUpTimersRef.current.forEach(clearTimeout);
    rankingCatchUpTimersRef.current = [1500, 3500, 6000].map((delay) =>
      setTimeout(() => refetchRanking(), delay),
    );
  }, [refetchRanking]);
  const handleScanResolved = useCallback(() => {
    // Advance the board's resolved-target count immediately (#223).
    setBoardRefreshNonce((n) => n + 1);
    // …then pull the ranking across the async scoring window so the header reflects the new score.
    scheduleRankingCatchUp();
  }, [scheduleRankingCatchUp]);
  // Every accepted target scan by ANY team member advances this count via the reliable `TeamBoardUpdated`
  // push (session-operations' BroadcastTeamBoardNotificationHandler re-projects the whole team's board on
  // each TargetResolvedEvent) — which is why the X/n numerator moves live for teammates. But the board's
  // own score always projects 0 (scoring lives in the scoring-monitoring ledger, read via the ranking
  // snapshot), so a teammate's scan reaches the HEADER score only through the `RankingChanged` scoring-hub
  // push — and that push has no catch-up fallback here, so a missed one leaves the header stuck on the
  // pre-scan score until the substage reveal force-refetches. The scanning participant is covered by
  // handleScanResolved; mirror it for everyone else by treating a resolved-target increase as the same
  // "team score changed" cue and pulling the ranking across the scoring window. Idempotent re-GETs; a
  // later RankingChanged push still wins the race if it arrives first.
  const resolvedTargets = board?.activeSubstage?.resolvedTargets ?? 0;
  const prevResolvedTargetsRef = useRef(resolvedTargets);
  useEffect(() => {
    // Only a growth is a scan; a reset to 0 (or a lower count) is a substage boundary, not a resolution.
    if (resolvedTargets > prevResolvedTargetsRef.current) {
      scheduleRankingCatchUp();
    }
    prevResolvedTargetsRef.current = resolvedTargets;
  }, [resolvedTargets, scheduleRankingCatchUp]);
  // A correct trivia answer is scored asynchronously (submit → outbox → RabbitMQ → RecordScoreEntry →
  // recalc → RankingChanged), so the header's live `score` only reflects the "+points" once the ranking
  // snapshot refreshes. Unlike the treasure-hunt scan (covered above) and penalties (covered in
  // use-ranking), the trivia flow had NO ranking catch-up — a missed RankingChanged push left the award
  // invisible until the substage reveal force-refetched, which read as "the correct answer added no
  // points." When a question closes into the reveal view, pull the ranking across the scoring window so
  // the reveal shows the awarded points. Idempotent re-GETs; a real RankingChanged push still wins the
  // race if it arrives first. (view.kind only changes on a real transition, so this fires once per close.)
  useEffect(() => {
    if (view.kind === 'reveal') {
      scheduleRankingCatchUp();
    }
  }, [view.kind, scheduleRankingCatchUp]);
  // D-3 substage ranking reveal. Play-mode agnostic: the cue arrives for a cleared treasure hunt and
  // a closed trivia round alike, which is why it is read here rather than inside either surface.
  const { isRevealing } = useRankingReveal({
    client,
    liveSessionId: result.liveSessionId,
    reconciliation: revealReconciliation,
  });
  const playMode = board?.activeSubstage?.playMode;
  // Only surface an error while it actually masks the board: a still-good board
  // kept from an earlier fetch (a failed re-fetch leaves `board` intact) renders
  // normally; a failed first fetch (`board` null) would otherwise fall silently
  // to the trivia surface.
  const maskedBoardError = board ? null : boardError;
  // Scoring is owned by the scoring-monitoring ledger (the ranking snapshot), not by the
  // session-operations board — `Team.CurrentScore` there is never awarded and always projects 0.
  // So the own-team live score is read from the ranking rows (works for both trivia and treasure
  // hunt, which both award into the ledger), falling back to the board value only until the first
  // ranking fetch resolves. The ledger keys on the cross-context `ReferenceTeamId` (see the scoring
  // service's AnswerRegisteredConsumer), so ranking `row.teamId` is that reference id — match on
  // `referenceTeamId`, NOT `board.teamId` (the session-scoped id, which never compares equal).
  const ownScore = rankingSnapshot?.rows.find(
    (row) => row.teamId === referenceTeamId,
  )?.totalScore;
  const score = ownScore ?? board?.currentScore ?? 0;
  // Hold the header score steady while a trivia question is live. The scoring ledger awards points the
  // instant an answer is registered, so a `RankingChanged` push lands ~1s after submit and would bump
  // the header mid-question — the participant should only see their score move at the reveal, next to
  // the +points chip. Freeze the pre-question score on entering the active view; every other view
  // (waiting/reveal/none) and the treasure-hunt board fall back to the live score.
  // Freezing in an effect would read a stale value: the ref still holds the previous question's score on
  // the render that enters the active view, and a ref write schedules no re-render to correct it. Adjust
  // during render instead, so the frozen score is the score at the moment the question opened.
  const [frozenScore, setFrozenScore] = useState(score);
  // Freeze the displayed ranking snapshot alongside the header score for the same reason: the "TODOS LOS
  // EQUIPOS" leaderboard renders the live ranking, so a `RankingChanged` push ~1s after a correct answer
  // would move a team's standing there while the frozen header stays put — leaking the post-answer score
  // before the reveal and desyncing the two surfaces. Capture the pre-question snapshot on entering the
  // active view so the leaderboard reveals in lockstep with the header. Same render-time (not effect)
  // freeze so the captured snapshot is the one visible at the moment the question opened.
  const [frozenSnapshot, setFrozenSnapshot] = useState(rankingSnapshot);
  // A penalty must break through the freeze. Unlike a correct-answer award — which the freeze hides until
  // the reveal, alongside the +points chip — an operator penalty is an explicit, immediate event: its toast
  // shows the instant it lands, so the header number has to drop with it (to zero if the team can't cover
  // the full deduction). Accumulate the deduction of each penalty that arrives during the frozen question
  // and subtract it from the displayed score below; reset when the next question opens, by which point the
  // live `score` already carries every prior deduction, so nothing is double-counted.
  const [frozenPenalty, setFrozenPenalty] = useState(0);
  const [prevViewKind, setPrevViewKind] = useState(view.kind);
  if (view.kind !== prevViewKind) {
    setPrevViewKind(view.kind);
    if (view.kind === 'active') {
      setFrozenScore(score);
      setFrozenSnapshot(rankingSnapshot);
      setFrozenPenalty(0);
    }
  }
  // The penalty toast is driven by the explicit ScoringHub `PenaltyApplied` push, not inferred from a
  // score decrease: a penalty against a low-scoring team clamps the ranking total to zero (leaving the
  // snapshot unchanged), and a partial clamp would misreport the magnitude. The push carries the true
  // deduction and always fires, so it surfaces mid-question regardless of the header score freeze.
  const { penalty: penaltyToast, clear: clearPenaltyToast } = usePenaltyToast(
    result.liveSessionId,
    referenceTeamId,
    scoringClient,
  );
  // `usePenaltyToast` mints a monotonic `id` per penalty against the own team. Fold each into the frozen
  // deduction exactly once — a re-render, or the toast lingering until its auto-dismiss, must not subtract
  // the same penalty twice.
  const lastPenaltyIdRef = useRef(0);
  useEffect(() => {
    if (!penaltyToast || penaltyToast.id === lastPenaltyIdRef.current) return;
    lastPenaltyIdRef.current = penaltyToast.id;
    // Cross-event memory sync: fold the just-pushed penalty into the running deduction.
    setFrozenPenalty((total) => total + penaltyToast.magnitude);
  }, [penaltyToast]);
  // Active question: show the frozen pre-question score, less any penalties applied since it opened
  // (clamped at zero). Every other view falls back to the live score, which already reflects them.
  const displayScore =
    view.kind === 'active' ? Math.max(0, frozenScore - frozenPenalty) : score;
  const displaySnapshot = view.kind === 'active' ? frozenSnapshot : rankingSnapshot;
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
  // Remember this participant's own pick while the question is live so the reveal can mark it red
  // immediately (like the prototype, which held the selection in hand). Flipping into the reveal view
  // empties `activeQuestionProps`, which resets the submit hook's selection to null — so it can't be
  // read once revealing; capture it here first. Reset to null on each new question (the hook clears
  // its selection then too) so a stale pick never bleeds into a question the team didn't answer.
  // State (not a ref) so the reveal view reads the captured pick reactively — reading a
  // ref's value during render is disallowed, and the reveal render needs this value.
  const [submittedSelection, setSubmittedSelection] = useState<number | null>(null);
  useEffect(() => {
    if (view.kind === 'active') {
      // Capture the live pick while the question is active so the reveal (which empties the
      // submit hook) can still show it. Cross-render memory syncing — needs an effect.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSubmittedSelection(submitHook.selectedOptionSequenceOrder);
    }
  }, [view.kind, submitHook.selectedOptionSequenceOrder]);

  // The ranking takes the whole screen, above both play-mode branches: the reveal fires in either
  // mode, and the treasure-hunt return below would otherwise swallow it in exactly the mixed-mode
  // mission it exists for.
  //
  // `Finished` renders it too, and not only after a terminal reveal — a session that runs out its
  // MaximumTime is cut short with no reveal event at all (D-4), so the state itself has to be enough
  // to land on the final ranking. `Cancelled` is deliberately excluded: an aborted session keeps the
  // host's red notice rather than being dressed up with standings.
  const isFinished = sessionState === 'Finished';
  if (isRevealing || isFinished) {
    return (
      <RankingReveal
        rows={rankingSnapshot?.rows}
        ownTeamId={referenceTeamId}
        isFinished={isFinished}
        missionDisplay={missionDisplay}
        error={rankingError}
        onRetry={refetchRanking}
        onRefetch={refetchRanking}
        onLeave={onLeave}
      />
    );
  }

  if (playMode === 'TreasureHunt' && board) {
    const progress = targetProgress(board.activeSubstage);
    // The board takes the whole screen. It was previously boxed at a hard-coded 640px inside the
    // outer Screen's ScrollView, which both cropped it on most devices and put its Clues/Teams
    // ScrollViews inside a same-direction parent — a combination that leaves a long clue list barely
    // scrollable. Given the viewport it sizes itself, and its body is the only vertical scroller.
    return (
      <>
        {penaltyToast ? (
          <PenaltyToast
            key={penaltyToast.id}
            penalty={penaltyToast}
            onDismiss={clearPenaltyToast}
          />
        ) : null}
        <TreasureHuntBoard
          teamDisplayName={board.teamDisplayName}
          currentScore={displayScore}
          timerDisplay={display}
          resolvedTargets={progress.resolved}
          totalActiveTargets={progress.total}
          visibleClues={board.visibleClues}
          activeTargets={board.activeTargets ?? []}
          substageId={board.activeSubstage?.substageSnapshotId}
          headerSlot={
            <>
              {banner}
              <SubstageProgress board={board} />
            </>
          }
          onLeave={onLeave}
          onScan={() => setScannerOpen(true)}
          rankingRows={rankingSnapshot?.rows}
          ownTeamId={referenceTeamId}
          rankingError={rankingError}
          onRetryRanking={refetchRanking}
        />
        {scannerOpen ? (
          <TargetScanner
            liveSessionId={result.liveSessionId}
            teamId={referenceTeamId}
            token={token}
            onClose={() => setScannerOpen(false)}
            onResolved={handleScanResolved}
          />
        ) : null}
      </>
    );
  }

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      {penaltyToast ? (
        <PenaltyToast
          key={penaltyToast.id}
          penalty={penaltyToast}
          onDismiss={clearPenaltyToast}
        />
      ) : null}
      {/* No brand mark on the live play surface: mid-game, prime above-the-fold space belongs to the
          question, not branding. The wordmark stays on the pre-game loading/denied screens below. */}
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
            score={displayScore}
            timerDisplay={display}
            missionDisplay={missionDisplay}
            selectedOptionSequenceOrder={submitHook.selectedOptionSequenceOrder}
            isSubmitting={submitHook.isSubmitting}
            isLocked={submitHook.isLocked}
            isClosed={isQuestionClosed}
            rejection={submitHook.rejection}
            onSelectOption={submitHook.selectOption}
            onSubmit={submitHook.submit}
            onDismissRejection={submitHook.clearRejection}
          />
        ) : view.kind === 'reveal' ? (
          <ActiveQuestionStage
            question={view.question}
            sessionState={sessionState}
            score={displayScore}
            timerDisplay={display}
            // No missionDisplay here: on the reveal view the question is closed, so the header's main
            // clock already carries the mission deadline (the backend fills the primary window with it
            // once no question is active). A second line would just duplicate it. See the active branch.
            isClosed={isQuestionClosed}
            selectedOptionSequenceOrder={submittedSelection}
            correctOptionSequenceOrder={view.correctOptionSequenceOrder}
            explanation={view.explanation}
            teamResult={view.teamResult}
          />
        ) : (
          // Between questions the header drops the question countdown (correct — no question is
          // active), but RF-06 still requires the session clock. Render the shared SessionTimerBar
          // here, as the treasure board does, so the two clocks stay distinct: the countdown is
          // per-question, this one runs for the whole session.
          <View style={{ alignSelf: 'stretch', backgroundColor: colors.ivoryFog }}>
            <ActiveQuestionStageHeader sessionState={sessionState} score={displayScore} />
            <View style={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.md }}>
              <SessionTimerBar display={display} />
            </View>
            <QuestionEmptyState kind={view.kind} sessionState={sessionState} />
          </View>
        )}
      </View>

      {/* HU-28 B2: the trivia surface has no Clues tab, so every clue kind (operative, substage-initial,
          scheduled/target) lands in the collapsed CLUES chip (durable store + dot) + arrival toast, fed
          from the live board. Suppress the toast during pre-game countdown so it doesn't overlap. */}
      {board ? (
        <OperativeClueSurface
          visibleClues={board.visibleClues}
          suppressToast={pregameSecondsLeft != null}
          substageId={board.activeSubstage?.substageSnapshotId}
        />
      ) : null}

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Abrir todos los equipos de ${result.teamDisplayName}`}
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
          {`TU EQUIPO · ${result.teamDisplayName}`}
        </Text>
        <Text muted style={{ color: colors.textMuted }}>
          {teamMembers.join(', ')} · TODOS LOS EQUIPOS ›
        </Text>
      </Pressable>

      {teamsOpen ? (
        <Panel style={{ gap: spacing.md }}>
          <Text variant="headline">TODOS LOS EQUIPOS</Text>
          {/* Live standings (HU-35 AC4). The scoring ledger keys ranking rows on the cross-context
              ReferenceTeamId, so own-team highlighting matches on `referenceTeamId` (same id the leaderboard
              on the treasure-hunt board uses). RankingLeaderboard owns the empty state ("Standings will
              appear once the round begins."); a failed fetch surfaces an error + retry instead of the
              old placeholder teams, so a real load failure never reads as data. */}
          {displaySnapshot ? (
            <RankingLeaderboard
              rows={displaySnapshot.rows}
              ownTeamId={referenceTeamId}
              missionDisplay={missionDisplay}
            />
          ) : rankingError ? (
            <View style={{ gap: spacing.sm, alignItems: 'center' }}>
              <Text
                variant="body"
                style={{ color: colors.signalCritical, textAlign: 'center' }}
              >
                {rankingErrorCopy(rankingError)}
              </Text>
              <Button label="REINTENTAR" variant="secondary" onPress={refetchRanking} />
            </View>
          ) : (
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
          )}
          <Button label="CERRAR" variant="secondary" onPress={() => setTeamsOpen(false)} />
        </Panel>
      ) : null}

      <Button label="Salir del espacio de equipo" variant="secondary" onPress={onLeave} />
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
            <Button label="Reintentar" variant="primary" onPress={onRetry} />
          ) : null}
          <Button label="Volver al inicio" variant="secondary" onPress={onLeave} />
        </View>
      )}
    </>
  );
}
