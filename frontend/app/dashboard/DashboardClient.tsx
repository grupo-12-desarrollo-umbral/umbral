'use client';

import { useCallback, useEffect, useMemo, useReducer, useRef, useState, useSyncExternalStore, useTransition } from 'react';
import { logout } from '@/app/actions/auth';
import { refreshSession } from '@/app/actions/session';
import { getUsersPage, deactivateUser, assignUserRole, inviteUser } from '@/app/actions/users';
import { listSessionsForOperator, listSessionsForAssignment, transitionSessionState, getSessionTimerSnapshotAction, getTriviaAnsweredMonitorAction, getOperatorSessionPanelAction, getOperatorRankingAction, getOperatorEvidenceTraceAction, getSessionHistoryAction, getReleasableCluesAction, getTriviaAnswerReviewAction } from '@/app/actions/sessions';
import { TeamsPanel } from './TeamsPanel'
import { TriviasPanel } from './TriviasPanel'
import { MissionsPanel } from './MissionsPanel'
import { SessionsPanel } from './SessionsPanel'
import { SessionOperatorPanel } from './SessionOperatorPanel'
import { OperatorSessionTimerPanel, MissionSessionTimerPanel } from './OperatorSessionTimerPanel'
import { TriviaRoundPanel } from './TriviaRoundPanel'
import { AnsweredMonitorPanel, type AnsweredTeamRow } from './AnsweredMonitorPanel'
import { TriviaAnswerReviewPanel, type AnswerReviewTeamRow } from './TriviaAnswerReviewPanel'
import { OperatorTeamProgressPanel } from './OperatorTeamProgressPanel'
import { OperatorClueReleasePanel } from './OperatorClueReleasePanel'
import { OperativeCluePanel } from './OperativeCluePanel'
import { PenaltyPanel } from './PenaltyPanel'
import { RankingPanel } from './RankingPanel'
import { EvidenceSubmissionsPanel } from './EvidenceSubmissionsPanel'
import { SessionHistoryPanel } from './SessionHistoryPanel'
import { SessionActivityPanel } from './SessionActivityPanel'
import { OperatorLiveSession, type OperatorLivePanels } from './OperatorLiveSession'
import { evidenceReducer, emptyEvidence } from './evidence-trace'
import { sessionActivityReducer, emptySessionActivity } from './session-activity'
import { isNonLiveQuestionSnapshot, revealAnswerReviewSequenceOrder } from './timer-snapshot'
import { createSessionStateRealtimeClient, type SessionRealtimeStatus } from '@/app/lib/realtime/session-state-client'
import { createRankingRealtimeClient } from '@/app/lib/realtime/ranking-client'
import { lifecycleActions, toLifecycleState, lifecycleStateLabel, isTransitionAllowed } from '@/app/lib/session-lifecycle'
import { getAccountStatus, accountStatusLabel, accountStatusTone } from '@/app/lib/account-status'
import { useTriviaRoundState } from '@/app/lib/realtime/use-trivia-round-state'
import type {
  InvitableRole,
  PagedResult,
  SessionAssignmentSummaryDto,
  SessionHistoryRowDto,
  SessionLifecycleState,
  SessionStateChangedNotificationDto,
  SessionTimerSnapshotDto,
  SessionTimerUpdatedNotificationDto,
  TransitionSessionStateResultDto,
  TriviaAnsweredMonitorDto,
  TriviaAnswerReviewDto,
  OperatorSessionPanelDto,
  RankingSnapshotDto,
  ReleasableClueDto,
  UserAccessCatalogItemDto,
} from '@/app/lib/definitions';
import styles from './dashboard.module.css';

type DashboardRole = 'operator' | 'admin' | 'participant';
type Theme = 'dark' | 'light';

const transportStatusLabel: Record<SessionRealtimeStatus, string> = {
  Connected: 'Conectado',
  Reconnecting: 'Reconectando',
  Offline: 'Desconectado',
  AuthExpired: 'Autenticación expirada',
};

const roleLabel: Record<DashboardRole, string> = {
  operator: 'operador',
  admin: 'administrador',
  participant: 'participante',
};

// Spanish display for the backend role names (kept as raw enum strings in payloads).
const backendRoleLabel: Record<string, string> = {
  Administrator: 'Administrador',
  Operator: 'Operador',
  Participant: 'Participante',
};

function displayRole(role: string) {
  return backendRoleLabel[role] ?? role;
}

const navigation = [
  { key: 'overview', icon: '⌂' },
  { key: 'operator', icon: '◌' },
  { key: 'teams', icon: '◫' },
  { key: 'trivias', icon: '▤' },
  { key: 'missions', icon: '⚑' },
  { key: 'sessions', icon: '▶' },
  { key: 'users', icon: '⊞' },
];

const lifecycleTone: Record<SessionLifecycleState, 'success' | 'warning' | 'critical' | 'muted'> = {
  Scheduled: 'muted',
  Preparing: 'warning',
  Active: 'success',
  Paused: 'warning',
  Finished: 'muted',
  Cancelled: 'critical',
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return 'No disponible'
  return new Date(value).toLocaleString()
}

function getOperatorDefaultSession() {
  return 'assigned-list';
}

// Theme is owned by the root layout's bootstrap script, which sets
// `data-theme` on <html> before first paint. Reading it through an external
// store keeps the toggle's icon/label in sync with the real theme: SSR and the
// first client render both use the server snapshot, then React reconciles to
// the attribute after hydration — no frozen, mislabelled control.
const THEME_EVENT = 'umbral-theme-change';

function subscribeTheme(onChange: () => void) {
  window.addEventListener(THEME_EVENT, onChange);
  window.addEventListener('storage', onChange);
  return () => {
    window.removeEventListener(THEME_EVENT, onChange);
    window.removeEventListener('storage', onChange);
  };
}

function getThemeSnapshot(): Theme {
  return document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
}

function getThemeServerSnapshot(): Theme {
  return 'dark';
}

// Sidebar collapse lives in localStorage and is read through an external store so
// SSR and first client render agree (both "expanded"); React reconciles to the
// persisted value after hydration without a mismatch on the derived nav attributes.
const SIDEBAR_EVENT = 'umbral-sidebar-change';

function subscribeSidebar(onChange: () => void) {
  window.addEventListener(SIDEBAR_EVENT, onChange);
  window.addEventListener('storage', onChange);
  return () => {
    window.removeEventListener(SIDEBAR_EVENT, onChange);
    window.removeEventListener('storage', onChange);
  };
}

function getSidebarCollapsed() {
  return window.localStorage.getItem('umbral-sidebar') === 'collapsed';
}

function getSidebarServerSnapshot() {
  return false;
}

function getNavigationLabel(role: DashboardRole, key: string) {
  if (key === 'sessions') {
    return role === 'operator' ? 'Mis sesiones' : 'Asignar operadores'
  }

  return (
    {
      overview: 'Resumen',
      operator: 'Operador',
      teams: 'Equipos',
      trivias: 'Trivias',
      missions: 'Misiones',
      users: 'Usuarios',
    } satisfies Record<string, string>
  )[key] ?? key
}

interface TimerState {
  snapshot: SessionTimerSnapshotDto | null
  error: string | null
  loading: boolean
}

type TimerAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: SessionTimerSnapshotDto }
  | { type: 'failed'; error: string }
  | { type: 'patched'; patch: Partial<SessionTimerSnapshotDto> }

function timerReducer(state: TimerState, action: TimerAction): TimerState {
  switch (action.type) {
    case 'reset':
      return { snapshot: null, error: null, loading: false }
    case 'load':
      return { ...state, loading: true, error: null }
    case 'loaded':
      return { snapshot: action.data, error: null, loading: false }
    case 'failed':
      return { snapshot: null, error: action.error, loading: false }
    case 'patched':
      return state.snapshot === null
        ? state
        : { ...state, snapshot: { ...state.snapshot, ...action.patch } }
  }
}

// HU-36A operator answered/not-answered board state. `roster` is identity-only; whether a team has
// answered is tracked separately in `answeredAt` (key present ⇒ answered), so a question boundary
// clears answers without discarding the roster. Nothing here carries option/correctness/points.
type MonitorRosterTeam = { runtimeTeamId: string; displayName: string; teamCode: string }

interface AnsweredMonitorState {
  loading: boolean
  unauthorized: boolean // genuine auth failure (403/401/non-operator) → not-authorized state
  error: string | null // transient/unexpected read failure → error state (distinct from unauthorized)
  activeQuestionOrder: number | null // null ⇒ no active trivia question → empty state
  roster: MonitorRosterTeam[]
  answeredAt: Record<string, string | null> // runtimeTeamId → answeredAt; presence ⇒ answered
}

const emptyAnsweredMonitor: AnsweredMonitorState = {
  loading: false,
  unauthorized: false,
  error: null,
  activeQuestionOrder: null,
  roster: [],
  answeredAt: {},
}

type AnsweredMonitorAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: TriviaAnsweredMonitorDto }
  | { type: 'noActiveQuestion' }
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string } // transient/unexpected read failure — not an auth problem
  // A live TeamAnswered carries the question order it belongs to, so a late event for a
  // closed question can be dropped instead of marking the team answered on the next question.
  | { type: 'teamAnswered'; teamId: string; answeredAt: string; order: number }
  | { type: 'questionActivated'; order: number } // new question → same roster, answers cleared
  | { type: 'questionClosed' } // question/substage boundary → no active question

function answeredMonitorReducer(
  state: AnsweredMonitorState,
  action: AnsweredMonitorAction,
): AnsweredMonitorState {
  switch (action.type) {
    case 'reset':
      return emptyAnsweredMonitor
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded': {
      const answeredAt: Record<string, string | null> = {}
      for (const team of action.data.teams) {
        if (team.answered) answeredAt[team.teamId] = team.answeredAt
      }
      return {
        loading: false,
        unauthorized: false,
        error: null,
        activeQuestionOrder: action.data.questionSequenceOrder,
        roster: action.data.teams.map((team) => ({
          runtimeTeamId: team.teamId,
          displayName: team.displayName,
          teamCode: team.teamCode,
        })),
        answeredAt,
      }
    }
    case 'noActiveQuestion':
      // No question active right now — keep the known roster so the next activation shows it,
      // but drop the active-question identity + answers so the board renders its empty state.
      return { ...state, loading: false, unauthorized: false, error: null, activeQuestionOrder: null, answeredAt: {} }
    case 'unauthorized':
      return { ...emptyAnsweredMonitor, unauthorized: true }
    case 'failed':
      return { ...state, loading: false, error: action.error }
    case 'teamAnswered':
      // Ignore a late answer for a question that is no longer the active one (event reordering
      // vs. questionActivated/close) so it can't flip a team answered on the wrong question.
      if (action.order !== state.activeQuestionOrder) return state
      return { ...state, answeredAt: { ...state.answeredAt, [action.teamId]: action.answeredAt } }
    case 'questionActivated':
      return { ...state, activeQuestionOrder: action.order, answeredAt: {} }
    case 'questionClosed':
      return { ...state, activeQuestionOrder: null, answeredAt: {} }
  }
}

// HU-24A operator live session panel state. The snapshot fetch and the OperatorSessionPanelUpdated
// push both deliver the full DTO (a wholesale re-projection, not a delta), so `loaded` replaces it
// outright. `unauthorized` (403/401/non-operator) is kept distinct from a transient `error`.
interface OperatorPanelState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  panel: OperatorSessionPanelDto | null
}

const emptyOperatorPanel: OperatorPanelState = { loading: false, unauthorized: false, error: null, panel: null }

type OperatorPanelAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: OperatorSessionPanelDto } // from snapshot fetch OR SignalR push (full re-projection)
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

function operatorPanelReducer(state: OperatorPanelState, action: OperatorPanelAction): OperatorPanelState {
  switch (action.type) {
    case 'reset':
      return emptyOperatorPanel
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded':
      return { loading: false, unauthorized: false, error: null, panel: action.data }
    case 'unauthorized':
      return { ...emptyOperatorPanel, unauthorized: true }
    case 'failed':
      return { ...state, loading: false, error: action.error }
  }
}

// RF-15 session audit history. REST-only — no push feeds it, so unlike the evidence trace there is no
// merge case: `loaded` replaces outright, and the list is only as fresh as the last read.
interface SessionHistoryState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  events: SessionHistoryRowDto[]
}

const emptySessionHistory: SessionHistoryState = { loading: false, unauthorized: false, error: null, events: [] }

type SessionHistoryAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; events: SessionHistoryRowDto[] }
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

function sessionHistoryReducer(state: SessionHistoryState, action: SessionHistoryAction): SessionHistoryState {
  switch (action.type) {
    case 'reset':
      return emptySessionHistory
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded':
      return { loading: false, unauthorized: false, error: null, events: action.events }
    case 'unauthorized':
      return { ...emptySessionHistory, unauthorized: true }
    case 'failed':
      return { ...state, loading: false, error: action.error }
  }
}

// HU-24B ranking state. Like the operator panel, both the REST snapshot and the RankingChanged push
// carry the full standings (a wholesale re-projection), so `loaded` replaces outright.
interface RankingState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  snapshot: RankingSnapshotDto | null
}

const emptyRanking: RankingState = { loading: false, unauthorized: false, error: null, snapshot: null }

type RankingAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: RankingSnapshotDto } // from snapshot fetch OR SignalR push (full re-projection)
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

function rankingReducer(state: RankingState, action: RankingAction): RankingState {
  switch (action.type) {
    case 'reset':
      return emptyRanking
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded':
      return { loading: false, unauthorized: false, error: null, snapshot: action.data }
    case 'unauthorized':
      return { ...emptyRanking, unauthorized: true }
    case 'failed':
      return { ...state, loading: false, error: action.error }
  }
}

// HU-36B operator post-close answer review state. Loaded when a question closes; cleared on
// question activation / substage advance / session switch. Three outcomes: data, unauthorized, error.
interface AnswerReviewState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  questionSequenceOrder: number | null
  teams: AnswerReviewTeamRow[]
}

const emptyAnswerReview: AnswerReviewState = {
  loading: false,
  unauthorized: false,
  error: null,
  questionSequenceOrder: null,
  teams: [],
}

type AnswerReviewAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: TriviaAnswerReviewDto }
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

function answerReviewReducer(state: AnswerReviewState, action: AnswerReviewAction): AnswerReviewState {
  switch (action.type) {
    case 'reset':
      return emptyAnswerReview
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded':
      return {
        loading: false,
        unauthorized: false,
        error: null,
        questionSequenceOrder: action.data.questionSequenceOrder,
        teams: action.data.teams.map((team) => ({
          teamId: team.teamId,
          displayName: team.displayName,
          teamCode: team.teamCode,
          selectedOptionSequenceOrder: team.selectedOptionSequenceOrder,
          isCorrect: team.isCorrect,
          scoreValue: team.scoreValue,
        })),
      }
    case 'unauthorized':
      return { ...emptyAnswerReview, unauthorized: true }
    case 'failed':
      return { ...state, loading: false, error: action.error }
  }
}

// HU-28 releasable-clues store. A wholesale replace (the GET returns the full active-substage list) or
// a reset on session switch — no deltas, so a plain replace/reset reducer suffices.
type ReleasableCluesAction =
  | { type: 'reset' }
  | { type: 'loaded'; clues: ReleasableClueDto[] }

function releasableCluesReducer(
  _state: ReleasableClueDto[],
  action: ReleasableCluesAction,
): ReleasableClueDto[] {
  switch (action.type) {
    case 'reset':
      return []
    case 'loaded':
      return action.clues
  }
}

export default function DashboardClient({
  role: initialRole,
  displayName,
}: {
  role: DashboardRole;
  displayName: string;
}) {
  const [role, setRole] = useState<DashboardRole>(initialRole);
  const theme = useSyncExternalStore(subscribeTheme, getThemeSnapshot, getThemeServerSnapshot);
  const sidebarCollapsed = useSyncExternalStore(
    subscribeSidebar,
    getSidebarCollapsed,
    getSidebarServerSnapshot,
  );

  useEffect(() => {
    refreshSession().then((result) => {
      if (result) {
        const mapped: DashboardRole =
          result.role === 'Administrator' ? 'admin'
          : result.role === 'Operator' ? 'operator'
          : 'participant';
        setRole((current) => (current === mapped ? current : mapped));
      }
    })
  }, []);
  const [selectedSessionId, setSelectedSessionId] = useState(() =>
    role === 'operator' ? getOperatorDefaultSession() : ''
  );
  const [activeNav, setActiveNav] = useState(() =>
    role === 'operator' ? 'sessions' : 'overview'
  );
  const [toast, setToast] = useState<{ title: string; body: string } | null>(null);
  const [operatorSessions, setOperatorSessions] = useState<SessionAssignmentSummaryDto[]>([]);
  const [operatorSessionsError, setOperatorSessionsError] = useState<string | null>(null);
  const [operatorSessionsRequestState, setOperatorSessionsRequestState] = useState<'idle' | 'loaded' | 'failed'>('idle');
  // Admin overview cross-session feed. The same GET /api/sessions the "Assign operators" view uses,
  // so the overview's metrics and live-session list are the real backend state, not a mock snapshot.
  const [adminSessions, setAdminSessions] = useState<SessionAssignmentSummaryDto[]>([]);
  const [adminSessionsRequestState, setAdminSessionsRequestState] = useState<'idle' | 'loaded' | 'failed'>('idle');
  const [realtimeStatus, setRealtimeStatus] = useState<SessionRealtimeStatus>('Offline');
  // Status of the SEPARATE scoring hub that feeds live ranking. Kept apart from `realtimeStatus`
  // (which reports the session transport) so the ranking panel can tell the operator when live
  // standings have stopped arriving instead of silently showing a stale snapshot.
  const [rankingRealtimeStatus, setRankingRealtimeStatus] = useState<SessionRealtimeStatus>('Offline');
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [pendingTransition, setPendingTransition] = useState<SessionLifecycleState | null>(null);
  const [confirmTransition, setConfirmTransition] = useState<SessionLifecycleState | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [liveUpdateNote, setLiveUpdateNote] = useState<string | null>(null);
  const [timerState, dispatchTimer] = useReducer(timerReducer, { snapshot: null, error: null, loading: false })
  const [monitorState, dispatchMonitor] = useReducer(answeredMonitorReducer, emptyAnsweredMonitor)
  const [operatorPanelState, dispatchOperatorPanel] = useReducer(operatorPanelReducer, emptyOperatorPanel)
  const [rankingState, dispatchRanking] = useReducer(rankingReducer, emptyRanking)
  const [evidenceState, dispatchEvidence] = useReducer(evidenceReducer, emptyEvidence)
  // Operator live activity feed — a client-side projection of the SignalR pushes handled below. No REST
  // snapshot backs it (nothing to load/merge); it resets on session switch and rebuilds from live events.
  const [activityState, dispatchActivity] = useReducer(sessionActivityReducer, emptySessionActivity)

  // The evidence trace names teams by runtime teamId only, which is unreadable to an operator, so
  // resolve display names off the operator panel rollup (same runtime teamId). Until that panel loads
  // the map is empty and rows fall back to "Unknown team" rather than blocking on it.
  const evidenceTeamNames = useMemo(
    () =>
      Object.fromEntries(
        (operatorPanelState.panel?.teamProgress ?? []).map((team) => [team.teamId, team.displayName]),
      ),
    [operatorPanelState.panel],
  )
  const [sessionHistoryState, dispatchSessionHistory] = useReducer(sessionHistoryReducer, emptySessionHistory)
  const [answerReviewState, dispatchAnswerReview] = useReducer(answerReviewReducer, emptyAnswerReview)
  // HU-28 release-clue picker source: the active substage's still-releasable hidden clues.
  // No SignalR push exists for it, so it is (re)loaded on session select, reconnect, and substage advance.
  // A reducer (not useState) so the session-switch reset dispatches inline in the same effect as the other
  // resets without tripping the "no setState in effect" lint (mirrors dispatchTimer / dispatchOperatorPanel).
  const [releasableClues, dispatchReleasableClues] = useReducer(releasableCluesReducer, [])
  const triviaRound = useTriviaRoundState()
  const {
    reset: resetTriviaRound,
    complete: completeTriviaRound,
    handlePregameTimerTick,
    handleQuestionActivated,
    hydrateActiveQuestion,
    handleQuestionClosed,
    handleSubstageAdvanced,
  } = triviaRound
  const isOperatorSessionsWorkspace = role === 'operator' && activeNav === 'sessions';

  useEffect(() => {
    if (!toast) {
      return;
    }

    const timeout = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  useEffect(() => {
    if (role !== 'operator') return

    listSessionsForOperator()
      .then((result) => {
        setOperatorSessions(result)
        setOperatorSessionsRequestState('loaded')
      })
      .catch(() => {
        setOperatorSessionsError(
          'No se pudieron cargar las sesiones asignadas a través del gateway. El backend necesita un endpoint de listado de sesiones autorizado para operadores.',
        )
        setOperatorSessionsRequestState('failed')
      })
  }, [role])

  useEffect(() => {
    if (role !== 'admin') return

    listSessionsForAssignment()
      .then((result) => {
        setAdminSessions(result)
        setAdminSessionsRequestState('loaded')
      })
      .catch(() => {
        setAdminSessionsRequestState('failed')
      })
  }, [role])

  const selectedOperatorSession =
    role === 'operator'
      ? operatorSessions.find((session) => session.liveSessionId === selectedSessionId) ?? null
      : null
  // HU-20 criterion 4: switcher and overview lists stay focused on active sessions;
  // finished/cancelled sessions are reviewed read-only from the Sessions panel.
  const activeOperatorSessions = operatorSessions.filter(
    (session) => session.sessionState !== 'Finished' && session.sessionState !== 'Cancelled',
  )
  const selectedOperatorState = selectedOperatorSession
    ? toLifecycleState(selectedOperatorSession.sessionState)
    : null
  // Admin overview derives entirely from the real GET /api/sessions feed. The topbar switcher and
  // the "Live sessions" list stay focused on sessions that aren't yet concluded (mirrors the operator
  // switcher rule); the metric cards summarise the full list.
  const selectedAdminSession =
    role !== 'operator'
      ? adminSessions.find((session) => session.liveSessionId === selectedSessionId) ?? null
      : null
  const selectedAdminState = selectedAdminSession
    ? toLifecycleState(selectedAdminSession.sessionState)
    : null
  const activeAdminSessions = adminSessions.filter(
    (session) => session.sessionState !== 'Finished' && session.sessionState !== 'Cancelled',
  )
  const adminActiveCount = adminSessions.filter(
    (session) => toLifecycleState(session.sessionState) === 'Active',
  ).length
  const adminPausedCount = adminSessions.filter(
    (session) => toLifecycleState(session.sessionState) === 'Paused',
  ).length
  const adminScheduledCount = adminSessions.filter(
    (session) => toLifecycleState(session.sessionState) === 'Scheduled',
  ).length
  const adminMetrics = [
    {
      label: 'Sesiones activas',
      value: String(adminActiveCount),
      hint: adminPausedCount > 0 ? `${adminPausedCount} en pausa` : 'Ninguna en pausa',
      pill: 'Resumen en vivo',
    },
    {
      label: 'Sesiones programadas',
      value: String(adminScheduledCount),
      hint: 'A la espera del inicio',
      pill: 'Próximas',
    },
    {
      label: 'Total de sesiones',
      value: String(adminSessions.length),
      hint: 'En todos los eventos',
      pill: 'Todas las sesiones',
    },
  ]

  // Key this effect on the stable session id, not the derived `selectedOperatorSession` object.
  // The object is re-created via `.find()` on every render, so depending on it tore down and
  // restarted the SignalR connection each render — aborting the in-flight negotiate ("connection
  // was stopped during negotiation"). Keying on the id connects once per selected session.
  const selectedRealtimeSessionId = selectedOperatorSession?.liveSessionId ?? null

  // Latest selected session, read by the async snapshot loaders below to drop a late response that
  // belongs to a previously-selected session (rapid session switch) instead of painting stale data.
  const selectedRealtimeSessionIdRef = useRef(selectedRealtimeSessionId)
  // Active question sequence order captured at QuestionActivated time so QuestionClosed can
  // dispatch the review fetch without reading from triviaRound state inside the SignalR effect.
  const activeQuestionSeqRef = useRef<number | null>(null)

  // Answered/not-answered rows: roster enumeration marks each team answered iff it appears in the
  // answered map (seeded by snapshot + filled by live TeamAnswered events) — never by broadcast absence.
  const answeredMonitorTeams: AnsweredTeamRow[] = monitorState.roster.map((team) => ({
    runtimeTeamId: team.runtimeTeamId,
    displayName: team.displayName,
    teamCode: team.teamCode,
    answered: team.runtimeTeamId in monitorState.answeredAt,
    answeredAt: monitorState.answeredAt[team.runtimeTeamId] ?? null,
  }))

  // Drive the client trivia countdown from an authoritative timer snapshot (a fetched snapshot or a
  // lifecycle-transition response). Skip a non-live question snapshot (pre-game placeholder / just-expired):
  // hydrating it would freeze the countdown at zero and clobber the live one SignalR's QuestionActivated
  // starts. A paused question still carries remaining time, so it is hydrated frozen as intended.
  const applyTimerSnapshotToTriviaRound = useCallback((timer: SessionTimerSnapshotDto) => {
    if (timer.activeQuestion) {
      // Freeze (don't tick) when the authoritative timer is frozen, e.g. a paused session.
      if (!isNonLiveQuestionSnapshot(timer)) hydrateActiveQuestion(timer.activeQuestion, timer.isAdvancing)
    } else if (timer.sessionState !== 'Active') {
      resetTriviaRound()
    }
  }, [hydrateActiveQuestion, resetTriviaRound])

  const loadAnswerReview = useCallback(async (liveSessionId: string, questionSequenceOrder: number) => {
    dispatchAnswerReview({ type: 'load' })
    const result = await getTriviaAnswerReviewAction(liveSessionId, questionSequenceOrder)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    if ('data' in result) {
      dispatchAnswerReview({ type: 'loaded', data: result.data })
    } else if ('unauthorized' in result) {
      dispatchAnswerReview({ type: 'unauthorized' })
    } else {
      dispatchAnswerReview({ type: 'failed', error: result.error })
    }
  }, [])

  const loadTimerSnapshot = useCallback(async (liveSessionId: string) => {
    dispatchTimer({ type: 'load' })
    const result = await getSessionTimerSnapshotAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    if ('error' in result) {
      dispatchTimer({ type: 'failed', error: result.error })
    } else {
      dispatchTimer({ type: 'loaded', data: result.data })
      applyTimerSnapshotToTriviaRound(result.data)
      // HU-36B AC4: opening/reconnecting into the reveal window (question already closed, no active
      // question) — with no QuestionActivated push captured — still populates the answer review. The
      // snapshot carries the just-closed sequence order; the live activate→close path is unaffected
      // (it fetches via onQuestionClosed and never reloads the snapshot at close).
      const revealSeq = revealAnswerReviewSequenceOrder(result.data)
      if (revealSeq != null) {
        void loadAnswerReview(liveSessionId, revealSeq)
      }
    }
  }, [applyTimerSnapshotToTriviaRound, loadAnswerReview])

  const loadAnsweredMonitor = useCallback(async (liveSessionId: string) => {
    dispatchMonitor({ type: 'load' })
    const result = await getTriviaAnsweredMonitorAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    if ('data' in result) {
      dispatchMonitor({ type: 'loaded', data: result.data })
    } else if ('noActiveQuestion' in result) {
      dispatchMonitor({ type: 'noActiveQuestion' })
    } else if ('unauthorized' in result) {
      dispatchMonitor({ type: 'unauthorized' })
    } else {
      dispatchMonitor({ type: 'failed', error: result.error })
    }
  }, [])

  const loadOperatorPanel = useCallback(async (liveSessionId: string) => {
    dispatchOperatorPanel({ type: 'load' })
    const result = await getOperatorSessionPanelAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    if ('data' in result) dispatchOperatorPanel({ type: 'loaded', data: result.data })
    else if ('unauthorized' in result) dispatchOperatorPanel({ type: 'unauthorized' })
    else dispatchOperatorPanel({ type: 'failed', error: result.error })
  }, [])

  const loadRanking = useCallback(async (liveSessionId: string) => {
    dispatchRanking({ type: 'load' })
    const result = await getOperatorRankingAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    if ('data' in result) dispatchRanking({ type: 'loaded', data: result.data })
    else if ('unauthorized' in result) dispatchRanking({ type: 'unauthorized' })
    else dispatchRanking({ type: 'failed', error: result.error })
  }, [])

  const loadEvidence = useCallback(async (liveSessionId: string) => {
    dispatchEvidence({ type: 'load' })
    const result = await getOperatorEvidenceTraceAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    // `snapshot`, not `loaded`: pushes that beat the RabbitMQ-fed projection are merged, not discarded.
    if ('data' in result) dispatchEvidence({ type: 'snapshot', data: result.data })
    else if ('unauthorized' in result) dispatchEvidence({ type: 'unauthorized' })
    else dispatchEvidence({ type: 'failed', error: result.error })
  }, [])

  const loadSessionHistory = useCallback(async (liveSessionId: string) => {
    dispatchSessionHistory({ type: 'load' })
    const result = await getSessionHistoryAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    // Rendered in the server's order — the repository already sorts by OccurredAt.
    if ('data' in result) dispatchSessionHistory({ type: 'loaded', events: result.data.events })
    else if ('unauthorized' in result) dispatchSessionHistory({ type: 'unauthorized' })
    else dispatchSessionHistory({ type: 'failed', error: result.error })
  }, [])

  const loadReleasableClues = useCallback(async (liveSessionId: string) => {
    const result = await getReleasableCluesAction(liveSessionId)
    // Drop a late response for a session the operator has since switched away from.
    if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
    // On unauthorized/error the picker simply has no options (the release control renders its empty note);
    // a transient read blip must not strand a stale clue list from a previous substage.
    dispatchReleasableClues({ type: 'loaded', clues: 'data' in result ? result.data.clues : [] })
  }, [])

  useEffect(() => {
    if (!selectedRealtimeSessionId) return

    const client = createSessionStateRealtimeClient({
      liveSessionId: selectedRealtimeSessionId,
      onStatusChange: setRealtimeStatus,
      onStateChanged: (notification: SessionStateChangedNotificationDto) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return

        setOperatorSessions((current) =>
          current.map((session) =>
            session.liveSessionId === notification.liveSessionId
              ? {
                  ...session,
                  sessionState: notification.currentState,
                  lastTransitionedAt: notification.changedAt,
                }
              : session
          )
        )
        if (notification.currentState === 'Finished') {
          completeTriviaRound()
          // Do NOT reset here: reset collapses the round panel to idle, and (worse) a SubstageAdvanced
          // racing in behind this push would re-strand it at "finishing session…". complete() is
          // terminal/sticky in the round machine; cleanup happens on session switch (effect teardown).
          // Pull a fresh timer snapshot so the mission timer reflects Finished instead of "Running".
          void loadTimerSnapshot(selectedRealtimeSessionId)
        } else if (notification.currentState === 'Cancelled') {
          resetTriviaRound()
          void loadTimerSnapshot(selectedRealtimeSessionId)
        } else if (notification.currentState === 'Active') {
          void loadReleasableClues(selectedRealtimeSessionId)
        }
        dispatchActivity({ type: 'state', data: notification })
        setLiveUpdateNote('Estado actualizado en vivo desde otro cliente o pestaña.')
      },
      onTimerUpdated: (notification: SessionTimerUpdatedNotificationDto) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return

        // The pre-game countdown reuses SessionTimerUpdated, distinguished by the backend's
        // authoritative IsPregameCountdown flag — NOT by a short total window. A short trivia
        // question (or a near-expiry remainder re-emitted on resume) also has a tiny total, so
        // inferring "pregame" from the duration misroutes real ticks into the countdown panel.
        // Route genuine pre-game ticks to the trivia state machine; do NOT patch the session timer.
        const isPregame =
          notification.isPregameCountdown === true &&
          notification.sessionState === 'Active'

        if (isPregame) {
          handlePregameTimerTick(
            notification.remainingMilliseconds,
            notification.totalMilliseconds,
          )
          return
        }

        dispatchTimer({
          type: 'patched',
          patch: {
            remainingSeconds: Math.round(notification.remainingMilliseconds / 1000),
            totalSeconds: Math.round(notification.totalMilliseconds / 1000),
            timerStatus: notification.isExpired
              ? 'Expired'
              : notification.isPaused
                ? 'Frozen'
                : 'Advancing',
            isAdvancing: !notification.isPaused && !notification.isExpired,
            isExpired: notification.isExpired,
            sessionState: notification.sessionState,
            observedAt: notification.emittedAt,
            // Keep the whole-mission clock live off the same tick. Absent (deadline not yet seeded)
            // leaves the prior value untouched — the tick only refines a mission clock it also carries.
            ...(notification.missionRemainingMilliseconds != null &&
            notification.missionTotalMilliseconds != null
              ? {
                  missionRemainingSeconds: Math.round(notification.missionRemainingMilliseconds / 1000),
                  missionTotalSeconds: Math.round(notification.missionTotalMilliseconds / 1000),
                }
              : {}),
          },
        })
      },
      onQuestionActivated: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        activeQuestionSeqRef.current = notification.sequenceOrder
        handleQuestionActivated(notification)
        dispatchActivity({ type: 'questionActivated', data: notification })
        // New question → same roster, answers cleared (HU-36A: board resets in lockstep). This is an
        // optimistic update; refetch the authoritative roster below so the board is populated even when
        // the operator opened the session before a question was active (mount fetched an empty roster).
        dispatchMonitor({ type: 'questionActivated', order: notification.sequenceOrder })
        void loadAnsweredMonitor(selectedRealtimeSessionId)
        // A new question activation clears any prior answer review (the old question is history).
        dispatchAnswerReview({ type: 'reset' })
        // Keep the timer snapshot's active-question window fresh so the panel shows the
        // countdown (not the no-question state) between snapshot reloads. Worker ticks refine it.
        dispatchTimer({
          type: 'patched',
          patch: {
            activeQuestion: { ...notification, remainingSeconds: notification.timeLimitSeconds },
            totalSeconds: notification.timeLimitSeconds,
            remainingSeconds: notification.timeLimitSeconds,
            timerStatus: 'Advancing',
            isAdvancing: true,
            isExpired: false,
            observedAt: notification.activatedAt,
          },
        })
      },
      onQuestionClosed: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        const closedSeq = activeQuestionSeqRef.current
        activeQuestionSeqRef.current = null
        handleQuestionClosed(notification)
        dispatchActivity({ type: 'questionClosed', data: notification })
        // Question closed → no active question; the board returns to its empty state.
        dispatchMonitor({ type: 'questionClosed' })
        // Fetch the post-close answer review for the just-closed question.
        if (closedSeq !== null) {
          void loadAnswerReview(selectedRealtimeSessionId, closedSeq)
        }
        // Question closed → no active question window; panel returns to the no-countdown state.
        dispatchTimer({
          type: 'patched',
          patch: {
            activeQuestion: null,
            totalSeconds: 0,
            remainingSeconds: 0,
            isAdvancing: false,
            observedAt: notification.closedAt,
          },
        })
      },
      onSubstageAdvanced: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        activeQuestionSeqRef.current = null
        handleSubstageAdvanced(notification)
        // SubstageAdvanced is the only push without its own timestamp — stamp receipt time.
        dispatchActivity({ type: 'substageAdvanced', data: notification, receivedAt: new Date().toISOString() })
        // The active substage changed, so its releasable hidden-clue targets did too; refetch the picker.
        void loadReleasableClues(selectedRealtimeSessionId)
        // Substage boundary retires the prior question; clear the board (mirror onQuestionClosed).
        dispatchMonitor({ type: 'questionClosed' })
        // Substage boundary also clears any prior answer review.
        dispatchAnswerReview({ type: 'reset' })
        // A substage boundary retires the prior question; drop the active-question window
        // (mirror onQuestionClosed) so the timer panel returns to no-active-question.
        dispatchTimer({
          type: 'patched',
          patch: {
            activeQuestion: null,
            totalSeconds: 0,
            remainingSeconds: 0,
            isAdvancing: false,
          },
        })
      },
      onTeamAnswered: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        // Option-free by design: the event only tells us this team answered + when.
        // `order` lets the reducer drop a late answer for an already-closed question.
        dispatchMonitor({
          type: 'teamAnswered',
          teamId: notification.teamId,
          answeredAt: notification.answeredAt,
          order: notification.questionSequenceOrder,
        })
        dispatchActivity({ type: 'teamAnswered', data: notification })
      },
      onOperatorPanel: (panel) => {
        if (panel.liveSessionId !== selectedRealtimeSessionId) return
        // Full re-projection (SessionStateChanged / SubstageAdvanced) — replace panel state wholesale.
        dispatchOperatorPanel({ type: 'loaded', data: panel })
      },
      onEvidenceSubmissionRegistered: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        // One row, not a re-projection — merged by evidenceSubmissionId. This can land before the REST
        // trace has the row at all, which is why it inserts rather than patching.
        dispatchEvidence({ type: 'registered', data: notification })
        dispatchActivity({ type: 'evidenceRegistered', data: notification })
      },
      onEvidenceSubmissionResolved: (notification) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
        // May arrive before its own registration; the reducer inserts a row in that case.
        dispatchEvidence({ type: 'resolved', data: notification })
        dispatchActivity({ type: 'evidenceResolved', data: notification })
      },
      onReconnected: () => {
        if (selectedRealtimeSessionId) {
          void loadTimerSnapshot(selectedRealtimeSessionId)
          void loadAnsweredMonitor(selectedRealtimeSessionId)
          void loadOperatorPanel(selectedRealtimeSessionId)
          void loadReleasableClues(selectedRealtimeSessionId)
          // Refetch the trace: pushes fired while the hub was down are gone, and only the REST
          // projection can tell us what we missed.
          void loadEvidence(selectedRealtimeSessionId)
          // No push feeds the history, so a hub reconnect is not itself a staleness signal for it —
          // but the outage that dropped the hub is what leaves the panel stranded in its error state,
          // whose copy promises an automatic refresh. This is the only thing that keeps that promise.
          void loadSessionHistory(selectedRealtimeSessionId)
        }
      },
    })

    void client.start()
    return () => {
      resetTriviaRound()
      void client.stop()
    }
  }, [
    selectedRealtimeSessionId,
    loadTimerSnapshot,
    loadAnsweredMonitor,
    loadOperatorPanel,
    loadReleasableClues,
    loadEvidence,
    loadSessionHistory,
    loadAnswerReview,
    resetTriviaRound,
    completeTriviaRound,
    handlePregameTimerTick,
    handleQuestionActivated,
    handleQuestionClosed,
    handleSubstageAdvanced,
  ])

  // HU-24B ranking lives on a SECOND hub (/hubs/scoring, scoring-monitoring-service) with its own
  // connection and lifecycle, so it gets its own effect rather than riding the /hubs/sessions client.
  // Its status is deliberately not fed into `realtimeStatus`: that badge reports the session transport,
  // and a scoring-hub blip must not make live session operation look offline.
  useEffect(() => {
    if (!selectedRealtimeSessionId) return

    // A superseded client (session switched, or its async stop() still settling) must not write the
    // new session's live status — gate every callback on this flag, flipped in cleanup.
    let active = true
    // A penalty recomputes standings asynchronously (scoring outbox → RankingChanged). The push can be
    // missed or clamp to an unchanged snapshot, so re-fetch across the recalc window; a later push still
    // wins if it lands. Cleared on cleanup so a session switch cannot fire a stale session's refetch.
    const penaltyCatchUpTimers: ReturnType<typeof setTimeout>[] = []
    const client = createRankingRealtimeClient({
      liveSessionId: selectedRealtimeSessionId,
      onStatusChange: (status) => {
        if (active) setRankingRealtimeStatus(status)
      },
      onRankingChanged: (snapshot) => {
        if (!active || snapshot.liveSessionId !== selectedRealtimeSessionId) return
        // Full standings on every push (the ledger recomputes the whole ranking) — replace wholesale.
        dispatchRanking({ type: 'loaded', data: snapshot })
      },
      onReconnected: () => {
        if (active && selectedRealtimeSessionId) void loadRanking(selectedRealtimeSessionId)
      },
      onPenaltyApplied: () => {
        penaltyCatchUpTimers.forEach(clearTimeout)
        penaltyCatchUpTimers.length = 0
        for (const delay of [1500, 3500, 6000]) {
          penaltyCatchUpTimers.push(
            setTimeout(() => {
              if (active && selectedRealtimeSessionId) void loadRanking(selectedRealtimeSessionId)
            }, delay),
          )
        }
      },
    })

    void client.start()
    return () => {
      active = false
      penaltyCatchUpTimers.forEach(clearTimeout)
      // Reset so a session switch never leaves the panel showing the prior session's live state.
      setRankingRealtimeStatus('Offline')
      void client.stop()
    }
  }, [selectedRealtimeSessionId, loadRanking])

  useEffect(() => {
    // Update the ref before dispatching loads so any still-in-flight load for the previous
    // session sees the new selection and drops its late response.
    selectedRealtimeSessionIdRef.current = selectedRealtimeSessionId
    activeQuestionSeqRef.current = null
    dispatchTimer({ type: 'reset' })
    dispatchMonitor({ type: 'reset' })
    dispatchOperatorPanel({ type: 'reset' })
    dispatchRanking({ type: 'reset' })
    dispatchEvidence({ type: 'reset' })
    dispatchActivity({ type: 'reset' })
    dispatchSessionHistory({ type: 'reset' })
    dispatchAnswerReview({ type: 'reset' })
    dispatchReleasableClues({ type: 'reset' })
    if (!selectedRealtimeSessionId) return
    void loadTimerSnapshot(selectedRealtimeSessionId)
    void loadAnsweredMonitor(selectedRealtimeSessionId)
    void loadOperatorPanel(selectedRealtimeSessionId)
    void loadRanking(selectedRealtimeSessionId)
    void loadEvidence(selectedRealtimeSessionId)
    void loadSessionHistory(selectedRealtimeSessionId)
    void loadReleasableClues(selectedRealtimeSessionId)
  }, [selectedRealtimeSessionId, loadTimerSnapshot, loadAnsweredMonitor, loadOperatorPanel, loadRanking, loadEvidence, loadSessionHistory, loadReleasableClues])

  function announce(title: string, body: string) {
    setToast({ title, body });
  }

  function toggleTheme() {
    const next: Theme = getThemeSnapshot() === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    window.localStorage.setItem('umbral-theme', next);
    window.dispatchEvent(new Event(THEME_EVENT));
  }

  function toggleSidebar() {
    const next = !getSidebarCollapsed();
    window.localStorage.setItem('umbral-sidebar', next ? 'collapsed' : 'expanded');
    window.dispatchEvent(new Event(SIDEBAR_EVENT));
  }

  function applyTransitionResult(result: TransitionSessionStateResultDto) {
    setOperatorSessions((current) =>
      current.map((session) =>
        session.liveSessionId === result.liveSessionId
          ? {
              ...session,
              sessionState: result.currentState,
              lastTransitionedAt: result.transitionedAt,
            }
          : session
      )
    )
  }

  function mapTransitionError(code: string) {
    // Note: invalid_transition carries the rejected from->to edge in the backend's `detail`
    // (InvalidSessionStateTransitionException.PublicDetail), but that string is English backend copy
    // ("Session cannot transition from 'Active' to 'Active'.") — never surfaced to the operator here.
    // The pre-flight guard in runTransition already pre-empts the common stale-button case; anything
    // that still reaches the backend gets the localized message below.
    return (
      {
        not_assigned_operator: 'No eres el operador asignado a esta sesión.',
        session_not_found: 'Sesión no encontrada. Puede que se haya eliminado o reasignado.',
        no_teams: 'Esta sesión aún no tiene equipos. Agrega al menos un equipo antes de iniciarla.',
        session_unassigned: 'Esta sesión no tiene operador asignado. Pide a un administrador que asigne uno antes de cambiar su estado.',
        invalid_transition: 'Ese cambio de estado no está permitido desde el estado actual de la sesión.',
        invalid_payload: 'Datos de cambio de estado no válidos. Recarga la página e inténtalo de nuevo.',
        forbidden: 'Solo un operador puede cambiar el estado de una sesión.',
      } satisfies Record<string, string>
    )[code] ?? 'Falló el cambio de estado. Inténtalo de nuevo.'
  }

  function requestTransition(targetState: SessionLifecycleState) {
    const action = selectedOperatorState
      ? lifecycleActions[selectedOperatorState].find((item) => item.targetState === targetState)
      : null

    setTransitionError(null)
    setLiveUpdateNote(null)
    if (action?.destructive) {
      setConfirmTransition(targetState)
      return
    }

    void runTransition(targetState)
  }

  async function runTransition(targetState: SessionLifecycleState) {
    if (!selectedOperatorSession) return

    // Pre-flight guard against a stale double-transition: if the live session state has already moved
    // on (a resume that landed via another tab or the SignalR echo), the lifecycle button the operator
    // clicked no longer maps to a valid edge. Firing it anyway makes the backend reject e.g. Active->Active
    // and surfaces a raw conflict. Bail benignly — the live state already reflects reality.
    const currentState = toLifecycleState(selectedOperatorSession.sessionState)
    if (!currentState || !isTransitionAllowed(currentState, targetState)) {
      setConfirmTransition(null)
      setCancelReason('')
      setLiveUpdateNote('La sesión ya cambió de estado (actualizada en vivo). No se aplicó la acción.')
      return
    }

    setPendingTransition(targetState)
    setTransitionError(null)
    try {
      const outcome = await transitionSessionState(
        selectedOperatorSession.liveSessionId,
        targetState,
        targetState === 'Cancelled' ? cancelReason : undefined,
      )
      if ('error' in outcome) {
        setTransitionError(mapTransitionError(outcome.error))
        return
      }
      const result = outcome.data
      applyTransitionResult(result)
      setConfirmTransition(null)
      setCancelReason('')
      announce(`${selectedOperatorSession.title} cambió a ${lifecycleStateLabel[result.currentState as SessionLifecycleState] ?? result.currentState}`, 'El backend aceptó la transición de ciclo de vida.')
      if (result.timer) {
        // The Start (→ Active) response carries a pre-game placeholder timer (expired, zero remaining);
        // don't display it — SignalR's QuestionActivated/timer ticks deliver the real countdown.
        if (!isNonLiveQuestionSnapshot(result.timer)) {
          dispatchTimer({ type: 'loaded', data: result.timer })
        }
        applyTimerSnapshotToTriviaRound(result.timer)
      } else {
        void loadTimerSnapshot(selectedOperatorSession.liveSessionId)
      }
    } catch {
      // Only unexpected faults reach here now — expected failures come back as { error }.
      setTransitionError('Falló el cambio de estado. Inténtalo de nuevo.')
    } finally {
      setPendingTransition(null)
    }
  }

  const statusTone =
    (selectedOperatorSession ? realtimeStatus : 'Offline') === 'Offline' ||
    (selectedOperatorSession ? realtimeStatus : 'Offline') === 'AuthExpired' ? 'critical'
    : selectedOperatorState ? lifecycleTone[selectedOperatorState]
    : selectedAdminState === 'Paused' ? 'warning'
    : 'success';
  const transportStatus = selectedOperatorSession ? realtimeStatus : 'Offline'
  const transportStatusText = transportStatusLabel[transportStatus]
  const realtimeAuthExpired = transportStatus === 'AuthExpired'
  const isLoadingOperatorSessions = role === 'operator' && operatorSessionsRequestState === 'idle'
  const healthIsGood = role !== 'operator' || transportStatus === 'Connected'

  const visibleNavigation = navigation.filter((item) => {
    if (role === 'participant') return item.key === 'overview'
    // HU-19: admins get the sessions nav for operator assignment; operator stub is dead
    // Issue #173: trivia authoring is Operator-owned, so admins no longer see the trivias nav.
    if (role === 'admin') return item.key !== 'operator' && item.key !== 'trivias'
    // HU-09 (DES-14): mission authoring is admin-only; operators never see the missions nav.
    // Issue #173: trivia authoring is now Operator-owned, so operators keep the trivias nav.
    // Issue #148: the Users view is Administrator-only; operators never see the users nav.
    if (role === 'operator') return item.key !== 'operator' && item.key !== 'missions' && item.key !== 'users'
    return true
  })

  // Builds the live-session panels as nodes and hands them to OperatorLiveSession, which
  // arranges them into the sticky-rail + tabs workspace. Built here in the component's direct
  // render scope (not a nested fn) so the react-hooks linter still sees the panel event handlers.
  const operatorLivePanels: OperatorLivePanels | null =
    role === 'operator' && selectedOperatorSession && selectedOperatorState
      ? {
      hero: (
        <section className={styles.hero} aria-labelledby="session-title" data-testid="operator-panel">
          <div className={styles.heroHeader}>
            <div className={styles.heroTitleWrap}>
              <div className={styles.heroMark} aria-hidden="true">
                <CompassMark />
              </div>

              <div className={styles.heroHeading}>
                <div className={styles.headerButtons}>
                  <h1 id="session-title">{selectedOperatorSession.title}</h1>
                  <span className={styles.chip} data-tone={statusTone}>
                    {lifecycleStateLabel[selectedOperatorState]}
                  </span>
                </div>

                {operatorPanelState.panel?.missionTitle ? (
                  <p className={styles.heroMission} data-testid="operator-hero-mission">
                    Misión: <strong>{operatorPanelState.panel.missionTitle}</strong>
                  </p>
                ) : null}

                <div className={styles.sessionMeta}>
                  <span>{selectedOperatorSession.sessionCode}</span>
                  <span>•</span>
                  <span>Programada {formatDateTime(selectedOperatorSession.scheduledAt)}</span>
                  <span>•</span>
                  <span>{selectedOperatorSession.assignedOperatorUserId == null ? 'Sin asignar' : 'Asignada a ti'}</span>
                </div>
              </div>
            </div>

            <div className={styles.heroMetrics}>
              <div className={styles.metricBlock}>
                <span className={styles.metricValue}>{lifecycleStateLabel[selectedOperatorState]}</span>
                <span className={styles.metricLabel}>Estado actual</span>
              </div>
              <div className={styles.metricBlock}>
                <span className={styles.metricValue}>{transportStatusText}</span>
                <span className={styles.metricLabel}>Transporte</span>
              </div>
              <div className={styles.metricBlock}>
                <span className={styles.metricValue}>
                  {selectedOperatorSession.lastTransitionedAt
                    ? new Date(selectedOperatorSession.lastTransitionedAt).toLocaleTimeString()
                    : 'Ninguna'}
                </span>
                <span className={styles.metricLabel}>Última transición</span>
              </div>
            </div>
          </div>
        </section>
      ),
      timer: (
        <OperatorSessionTimerPanel
          timer={timerState.snapshot}
          isLoading={timerState.loading}
          error={timerState.error}
        />
      ),
      missionTimer: (
        <MissionSessionTimerPanel
          timer={timerState.snapshot}
          isLoading={timerState.loading}
          error={timerState.error}
        />
      ),
      teamProgress: (
        <OperatorTeamProgressPanel
          panel={operatorPanelState.panel}
          ranking={rankingState.snapshot}
          unauthorized={operatorPanelState.unauthorized}
          error={operatorPanelState.error}
          loading={operatorPanelState.loading}
        />
      ),
      ranking: (
        <RankingPanel
          snapshot={rankingState.snapshot}
          unauthorized={rankingState.unauthorized}
          error={rankingState.error}
          loading={rankingState.loading}
          live={rankingRealtimeStatus === 'Connected'}
        />
      ),
      evidence: (
        <EvidenceSubmissionsPanel
          items={evidenceState.items}
          teamNames={evidenceTeamNames}
          unauthorized={evidenceState.unauthorized}
          error={evidenceState.error}
          loading={evidenceState.loading}
          // Evidence rides /hubs/sessions, so it reports the SESSION transport — unlike the
          // ranking above, which has its own scoring-hub status.
          live={realtimeStatus === 'Connected'}
        />
      ),
      history: (
        <SessionHistoryPanel
          events={sessionHistoryState.events}
          teamNames={evidenceTeamNames}
          unauthorized={sessionHistoryState.unauthorized}
          error={sessionHistoryState.error}
          loading={sessionHistoryState.loading}
        />
      ),
      activity: (
        <SessionActivityPanel
          entries={activityState.entries}
          teamNames={evidenceTeamNames}
        />
      ),
      cluesRow: (
        <div className={styles.cluePanelsRow}>
          <OperatorClueReleasePanel
            liveSessionId={selectedOperatorSession.liveSessionId}
            state={selectedOperatorState}
            teams={(operatorPanelState.panel?.teamProgress ?? []).map((t) => ({
              teamId: t.teamId,
              displayName: t.displayName,
            }))}
            releasableClues={releasableClues}
            onReleased={(label, count) =>
              announce('Pista liberada', `${label} revelada a ${count} equipo${count === 1 ? '' : 's'}.`)
            }
          />

          <OperativeCluePanel
            liveSessionId={selectedOperatorSession.liveSessionId}
            state={selectedOperatorState}
            teams={(operatorPanelState.panel?.teamProgress ?? []).map((t) => ({
              teamId: t.teamId,
              displayName: t.displayName,
            }))}
            onAdded={(count) =>
              announce('Pista operativa asignada', `Pista asignada a ${count} equipo${count === 1 ? '' : 's'}.`)
            }
          />

          <PenaltyPanel
            liveSessionId={selectedOperatorSession.liveSessionId}
            state={selectedOperatorState}
            // Penalties are a SCORING action, keyed on the cross-context referenceTeamId — NOT the
            // runtime teamId the clue panels above use. Sending teamId here would file the penalty
            // under an id ranking never groups on, so it would never reduce the team's score. Drop
            // any team missing a referenceTeamId (can't be penalized) rather than send a bad id.
            teams={(operatorPanelState.panel?.teamProgress ?? [])
              .filter((t) => t.referenceTeamId != null)
              .map((t) => ({
                teamId: t.referenceTeamId as string,
                displayName: t.displayName,
              }))}
            onApplied={(teamId, amount) =>
              announce('Penalización aplicada', `−${amount} pts registrados para el equipo seleccionado.`)
            }
          />
        </div>
      ),
      triviaRound: (
        <TriviaRoundPanel
          phase={triviaRound.phase}
          pregameSecondsLeft={triviaRound.pregameSecondsLeft}
          activeQuestion={triviaRound.activeQuestion}
          questionSecondsLeft={triviaRound.questionSecondsLeft}
          substageOrdinal={triviaRound.substageOrdinal}
          finalizing={triviaRound.finalizing}
        />
      ),
      answeredMonitor: (
        <AnsweredMonitorPanel
          activeQuestionOrder={monitorState.activeQuestionOrder}
          teams={answeredMonitorTeams}
          unauthorized={monitorState.unauthorized}
          error={monitorState.error}
          loading={monitorState.loading}
        />
      ),
      answerReview: (
        <TriviaAnswerReviewPanel
          questionSequenceOrder={answerReviewState.questionSequenceOrder}
          teams={answerReviewState.teams}
          unauthorized={answerReviewState.unauthorized}
          error={answerReviewState.error}
          loading={answerReviewState.loading}
        />
      ),
      controls: (
        <section className={styles.panel} aria-labelledby="session-controls-title">
          <div className={styles.panelHeader}>
            <div>
              <h2 id="session-controls-title">Controles de la sesión</h2>
              <div className={styles.panelMeta}>Las próximas acciones permitidas se derivan del estado del ciclo de vida en el backend.</div>
            </div>
          </div>

          {transitionError && (
            <p className={styles.errorBanner} role="alert" data-testid="session-transition-error">
              {transitionError}
            </p>
          )}

          <div className={styles.controlGrid}>
            {lifecycleActions[selectedOperatorState].map((action) => (
              <button
                className={styles.controlTile}
                data-testid={`session-action-${action.targetState}`}
                data-tone={action.destructive ? 'critical' : action.targetState === 'Active' ? 'success' : 'accent'}
                disabled={pendingTransition != null}
                key={action.targetState}
                onClick={() => requestTransition(action.targetState)}
                type="button"
              >
                <span className={styles.controlKicker}>{action.destructive ? '!' : '>'}</span>
                <span className={styles.controlTitle}>
                  {pendingTransition === action.targetState ? 'Procesando...' : action.label}
                </span>
                <span className={styles.controlCopy}>{action.description}</span>
              </button>
            ))}
          </div>

          {lifecycleActions[selectedOperatorState].length === 0 && (
            <p className={styles.emptyStateCopy} data-testid="session-no-actions">
              Esta sesión es terminal. No hay más acciones de ciclo de vida disponibles.
            </p>
          )}

          {confirmTransition && (
            <section className={styles.confirmPanel} aria-label="Confirmar transición terminal">
              <h3>Confirmar «{lifecycleStateLabel[confirmTransition]}»</h3>
              <p className={styles.panelMeta}>
                Esta es una acción de ciclo de vida terminal. El backend la rechazará si la transición ya no es válida.
              </p>
              {confirmTransition === 'Cancelled' && (
                <label className={styles.reasonField}>
                  <span>Motivo de cancelación (opcional)</span>
                  <textarea
                    value={cancelReason}
                    onChange={(event) => setCancelReason(event.target.value)}
                    rows={3}
                    disabled={pendingTransition != null}
                  />
                </label>
              )}
              <div className={styles.confirmRow}>
                <button
                  className={styles.dangerButton}
                  data-testid="session-confirm-terminal"
                  disabled={pendingTransition != null}
                  onClick={() => void runTransition(confirmTransition)}
                  type="button"
                >
                  Confirmar «{lifecycleStateLabel[confirmTransition]}»
                </button>
                <button
                  className={styles.inlineButton}
                  disabled={pendingTransition != null}
                  onClick={() => {
                    setConfirmTransition(null)
                    setCancelReason('')
                  }}
                  type="button"
                >
                  Mantener la sesión abierta
                </button>
              </div>
            </section>
          )}
        </section>
      ),
      detail: (
        <section className={styles.panel} aria-labelledby="session-detail-title">
          <div className={styles.panelHeader}>
            <div>
              <h2 id="session-detail-title">Detalle de la sesión</h2>
              <div className={styles.panelMeta}>Resumen del backend para la sesión de operador seleccionada.</div>
            </div>
          </div>

          <dl className={styles.detailList}>
            <dt>Título</dt>
            <dd>{selectedOperatorSession.title}</dd>
            <dt>Código</dt>
            <dd>{selectedOperatorSession.sessionCode}</dd>
            <dt>Horario programado</dt>
            <dd>{formatDateTime(selectedOperatorSession.scheduledAt)}</dd>
            <dt>Titularidad</dt>
            <dd>{selectedOperatorSession.assignedOperatorUserId == null ? 'Sin operador asignado' : 'Operador asignado presente'}</dd>
            <dt>Última transición</dt>
            <dd>{formatDateTime(selectedOperatorSession.lastTransitionedAt)}</dd>
            <dt>Transporte</dt>
            <dd>{transportStatusText}</dd>
          </dl>
        </section>
      ),
      banners: (
        <>
          {liveUpdateNote && (
            <p className={styles.liveNote} role="status" data-testid="session-live-update-note">
              {liveUpdateNote}
            </p>
          )}

          {realtimeAuthExpired && (
            <section className={styles.authBanner} role="alert" data-testid="session-auth-expired-banner">
              <div>
                <strong>La autenticación en tiempo real expiró.</strong> Tu sesión del panel sigue activa, pero SignalR no puede reconectarse hasta que inicies sesión de nuevo.
              </div>
              <div className={styles.authBannerActions}>
                <a className={styles.primaryButton} href="/api/auth/login">
                  Iniciar sesión de nuevo
                </a>
                <form action={logout}>
                  <button className={styles.inlineButton} type="submit">
                    Cerrar sesión
                  </button>
                </form>
              </div>
            </section>
          )}
        </>
      ),
        }
      : null

  return (
    <div className={styles.page}>
      <a className={styles.skipLink} href="#main-content">
        Saltar al contenido principal
      </a>

      <div className={styles.frame} data-collapsed={sidebarCollapsed}>
        <aside className={styles.sidebar} aria-label="Navegación principal">
          <div className={styles.brand}>
            <div className={styles.brandMark} aria-hidden="true">
              <CompassMark />
            </div>
            <div>
              <div className={styles.brandTitle}>Umbral</div>
              <div className={styles.brandMeta}>Centro de mando</div>
            </div>
          </div>

          <div className={styles.rolePill} data-testid="role-chip">{roleLabel[role]}</div>

          <nav className={styles.nav}>
            {visibleNavigation.map((item) => (
              <button
                key={item.key}
                className={styles.navItem}
                data-active={activeNav === item.key}
                data-testid={`nav-${item.key}`}
                aria-current={activeNav === item.key ? 'page' : undefined}
                onClick={() => setActiveNav(item.key)}
                title={sidebarCollapsed ? getNavigationLabel(role, item.key) : undefined}
                type="button"
              >
                <span className={styles.navIcon} aria-hidden="true">{item.icon}</span>
                <span className={styles.navLabel}>{getNavigationLabel(role, item.key)}</span>
                <span className={styles.navMarker} aria-hidden="true" />
              </button>
            ))}
          </nav>

          <div className={styles.sidebarFooter}>
            {/* Operator-only: it reports the operator's live-session SignalR transport. Admins have no
                admin-scoped hub connection, so there is no real health signal to show them. */}
            {role === 'operator' && (
              <section className={styles.healthCard} aria-labelledby="session-health-title">
                <div className={styles.eyebrow} id="session-health-title">
                  Estado de la sesión
                </div>
                <div className={styles.healthStatus}>
                  <span className={styles.statusDot} aria-hidden="true" data-tone={healthIsGood ? 'success' : 'critical'} />
                  <span>{healthIsGood ? 'Correcto' : 'Requiere atención'}</span>
                </div>
                <p className={styles.healthText}>
                  {realtimeAuthExpired
                    ? 'La autenticación en tiempo real expiró. Inicia sesión de nuevo para restaurar las actualizaciones en vivo.'
                    : `Transporte en tiempo real: ${transportStatusText}.`}
                </p>
              </section>
            )}

            <button
              className={styles.collapseButton}
              type="button"
              onClick={toggleSidebar}
              aria-pressed={sidebarCollapsed}
              title={sidebarCollapsed ? 'Expandir barra lateral' : 'Contraer barra lateral'}
            >
              <span aria-hidden="true">{sidebarCollapsed ? '›' : '‹'}</span>
              <span className={styles.navLabel}>{sidebarCollapsed ? 'Expandir' : 'Contraer'}</span>
            </button>
          </div>
        </aside>

        <main className={styles.main} id="main-content">
          <section className={styles.topbar} aria-label="Controles del panel">
            <div className={styles.topbarLeft}>
              {isOperatorSessionsWorkspace ? (
                <>
                  <span className={styles.chip}>Mis sesiones</span>
                  <span className={styles.panelMeta}>
                    {selectedOperatorSession
                      ? `${selectedOperatorSession.title} forma parte de tu carga de trabajo actual. Selecciona una de tus sesiones para revisarla antes de la operación en vivo.`
                      : 'Revisa las sesiones que te fueron asignadas, o pide a un administrador que asigne o reasigne una sesión.'}
                  </span>
                </>
              ) : (
                <>
                  <label>
                    <span className="sr-only">Selector de sesión</span>
                    <select
                      className={styles.sessionSelect}
                      data-testid="session-switcher"
                      onChange={(event) => setSelectedSessionId(event.target.value)}
                      value={selectedSessionId}
                    >
                    {role === 'operator' ? (
                      <>
                        <option value="assigned-list">Mis sesiones</option>
                        {activeOperatorSessions.map((session) => (
                          <option key={session.liveSessionId} value={session.liveSessionId}>
                            {session.title}
                          </option>
                        ))}
                      </>
                    ) : (
                      <>
                        <option value="">Selecciona una sesión</option>
                        {activeAdminSessions.map((session) => (
                          <option key={session.liveSessionId} value={session.liveSessionId}>
                            {session.title}
                          </option>
                        ))}
                      </>
                    )}
                    </select>
                  </label>
                  <span
                    className={selectedOperatorState === 'Active' && transportStatus === 'Connected' ? styles.liveChip : styles.chip}
                    data-tone={
                      role === 'operator'
                        ? transportStatus === 'Offline' || transportStatus === 'AuthExpired'
                          ? 'critical'
                          : selectedOperatorState
                            ? lifecycleTone[selectedOperatorState]
                            : 'muted'
                        : selectedAdminState
                          ? lifecycleTone[selectedAdminState]
                          : 'muted'
                    }
                  >
                    {role === 'operator'
                      ? (selectedOperatorState ? lifecycleStateLabel[selectedOperatorState] : transportStatusText)
                      : (selectedAdminState ? lifecycleStateLabel[selectedAdminState] : 'Sin sesión seleccionada')}
                  </span>
                  <span className={styles.panelMeta}>
                    {role === 'operator'
                      ? selectedOperatorSession
                        ? `Programada ${formatDateTime(selectedOperatorSession.scheduledAt)}`
                        : 'Elige una sesión para inspeccionar'
                      : selectedAdminSession
                        ? `Programada ${formatDateTime(selectedAdminSession.scheduledAt)}`
                        : 'Elige una sesión para inspeccionar'}
                  </span>
                </>
              )}
            </div>

            <div className={styles.topbarRight}>
              {/* The transport pill reflects the operator's live-session hub status. Admins have no
                  such connection, so the pill is operator-only rather than a constant "SignalR" label. */}
              {role === 'operator' && (
                <div className={styles.statusRow}>
                  <span className={styles.statusToggle} data-testid="session-transport-status">
                    {transportStatusText}
                  </span>
                </div>
              )}

              <button
                className={styles.themeButton}
                onClick={toggleTheme}
                type="button"
                aria-label={theme === 'dark' ? 'Cambiar al tema claro' : 'Cambiar al tema oscuro'}
                title={theme === 'dark' ? 'Cambiar al tema claro' : 'Cambiar al tema oscuro'}
              >
                {theme === 'dark' ? '☾' : '☼'}
              </button>

              <form action={logout}>
                <button
                  className={styles.smallButton}
                  type="submit"
                  data-testid="logout-button"
                  title="Cerrar sesión"
                >
                  Cerrar sesión
                </button>
              </form>
            </div>
          </section>

          {role === 'participant' ? (
            <section className={styles.emptyState} aria-labelledby="participant-title" data-testid="participant-panel">
              <div>
                <h1 id="participant-title">Bienvenido, {displayName}</h1>
                <p className={styles.emptyStateCopy}>
                  Has iniciado sesión como participante. Los controles de operador y administrador no están
                  disponibles en esta vista.
                </p>
              </div>
            </section>
          ) : activeNav === 'users' && role === 'admin' ? (
            <UsersPanel role={role} />
          ) : activeNav === 'teams' ? (
            <TeamsPanel role={role} />
          ) : activeNav === 'trivias' ? (
            <TriviasPanel role={role} />
          ) : activeNav === 'missions' ? (
            <MissionsPanel role={role} />
          ) : activeNav === 'sessions' ? (
            role === 'admin'
              ? <SessionOperatorPanel />
              : (
                  <SessionsPanel
                    assignedSessions={operatorSessions}
                    isLoadingAssignedSessions={isLoadingOperatorSessions}
                    assignedSessionsError={operatorSessionsError}
                    selectedSessionId={selectedSessionId === 'assigned-list' ? null : selectedSessionId}
                    onSelectSession={setSelectedSessionId}
                    onOpenLiveOperation={(sessionId) => {
                      setSelectedSessionId(sessionId)
                      setActiveNav('overview')
                    }}
                  />
                )
          ) : role === 'operator' && selectedSessionId === 'assigned-list' ? (
            <section className={styles.emptyState} aria-labelledby="assigned-sessions-title" data-testid="operator-panel">
              <div>
                <h1 id="assigned-sessions-title">Mis sesiones</h1>
                <p className={styles.emptyStateCopy}>
                  Revisa las sesiones de las que eres responsable, o pide a un administrador que asigne o reasigne una sesión cuando cambie la operación en vivo.
                </p>
              </div>

              {operatorSessionsError && (
                <p className={styles.errorBanner} role="alert">
                  {operatorSessionsError}
                </p>
              )}

              {isLoadingOperatorSessions && (
                <p className={styles.panelMeta}>Cargando tus sesiones asignadas…</p>
              )}

              {operatorSessionsRequestState === 'loaded' && activeOperatorSessions.length === 0 && (
                <p className={styles.emptyStateCopy} data-testid="no-assigned-sessions">
                  Ahora mismo no tienes ninguna sesión activa asignada. Cuando un administrador te
                  asigne una, aparecerá aquí.
                </p>
              )}

              <div className={styles.sessionCards}>
                {activeOperatorSessions.map((session) => {
                  const sessionState = toLifecycleState(session.sessionState);

                  return (
                    <button
                      key={session.liveSessionId}
                      className={styles.sessionButton}
                      data-current={false}
                      onClick={() => setSelectedSessionId(session.liveSessionId)}
                      type="button"
                    >
                      <div className={styles.sessionCardHeader}>
                        <div>
                          <h3>{session.title}</h3>
                          <div className={styles.sessionCardMeta}>
                            {session.sessionCode} • Programada {formatDateTime(session.scheduledAt)}
                          </div>
                        </div>
                        <span className={styles.chip} data-tone={sessionState ? lifecycleTone[sessionState] : 'muted'}>
                          {sessionState ? lifecycleStateLabel[sessionState] : session.sessionState}
                        </span>
                      </div>
                    </button>
                  );
                })}
              </div>

              <div className={styles.emptyActions}>
                <button className={styles.primaryButton} onClick={() => activeOperatorSessions[0] && setSelectedSessionId(activeOperatorSessions[0].liveSessionId)} type="button" disabled={activeOperatorSessions.length === 0}>
                  Abrir sesión en vivo
                </button>
              </div>
            </section>
          ) : role === 'operator' && selectedOperatorSession && selectedOperatorState ? (
            operatorLivePanels && <OperatorLiveSession panels={operatorLivePanels} />
          ) : role === 'operator' ? (
            <section className={styles.emptyState} aria-labelledby="operator-session-unavailable-title" data-testid="operator-panel">
              <div>
                <h1 id="operator-session-unavailable-title">Ninguna sesión de operador seleccionada</h1>
                <p className={styles.emptyStateCopy}>
                  Selecciona una de tus sesiones asignadas antes de abrir los controles de ciclo de vida.
                </p>
              </div>
              <div className={styles.emptyActions}>
                <button className={styles.primaryButton} onClick={() => setActiveNav('sessions')} type="button">
                  Volver a mis sesiones
                </button>
              </div>
            </section>
          ) : (
            <>
              <section className={styles.hero} aria-labelledby="admin-title" data-testid="admin-panel">
                <div className={styles.heroHeader}>
                  <div className={styles.heroTitleWrap}>
                    <div className={styles.heroMark} aria-hidden="true">
                      <CompassMark />
                    </div>
                    <div className={styles.heroHeading}>
                      <div className={styles.headerButtons}>
                        <h1 id="admin-title">Resumen de la noche de trivia</h1>
                      </div>
                      <div className={styles.sessionMeta}>
                        <span>Los eventos de trivia de esta noche</span>
                        <span>•</span>
                        <span>{adminActiveCount} {adminActiveCount === 1 ? 'sesión activa' : 'sesiones activas'}</span>
                        <span>•</span>
                        <span>{adminSessions.length} en total</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.heroActions}>
                    <button className={styles.secondaryButton} type="button" onClick={() => setActiveNav('sessions')}>
                      Asignar operadores
                    </button>
                  </div>
                </div>
              </section>

              <section className={styles.adminMetrics} aria-label="Métricas del resumen del administrador">
                {adminMetrics.map((metric) => (
                  <article className={styles.metricCard} key={metric.label}>
                    <div className={styles.metricNumber}>{metric.value}</div>
                    <div className={styles.metricContent}>
                      <h2>{metric.label}</h2>
                      <div className={styles.metricHint}>{metric.hint}</div>
                      <span className={styles.metricPill}>{metric.pill}</span>
                    </div>
                  </article>
                ))}
              </section>

              <section className={styles.adminPanels} aria-label="Paneles de control del administrador">
                <article className={`${styles.panel} ${styles.adminPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2>Accesos de configuración</h2>
                      <div className={styles.panelMeta}>La gestión de cuestionarios, equipos y sesiones se mantiene cerca de la parte superior de la vista.</div>
                    </div>
                  </div>
                  <div className={styles.controlGrid}>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>▤</span>
                      <span className={styles.controlTitle}>Cuestionarios</span>
                      <span className={styles.controlCopy}>Equilibra la dificultad de la trivia y los tiempos de respuesta.</span>
                    </button>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>◫</span>
                      <span className={styles.controlTitle}>Equipos</span>
                      <span className={styles.controlCopy}>Revisa las listas, los distintivos de equipo y los estados de registro.</span>
                    </button>
                  </div>
                </article>

                <article className={`${styles.panel} ${styles.adminPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2>Sesiones en vivo</h2>
                      <div className={styles.panelMeta}>Sesiones activas y en pausa en todos los eventos.</div>
                    </div>
                  </div>
                  <div className={styles.sessionList}>
                    {(() => {
                      const liveList = activeAdminSessions.filter(
                        (session) => toLifecycleState(session.sessionState) !== 'Scheduled',
                      )
                      if (adminSessionsRequestState === 'failed') {
                        return <p className={styles.panelMeta}>No se pudieron cargar las sesiones en vivo a través del gateway.</p>
                      }
                      if (liveList.length === 0) {
                        return (
                          <p className={styles.panelMeta}>
                            {adminSessionsRequestState === 'loaded' ? 'No hay sesiones en vivo ahora mismo.' : 'Cargando sesiones…'}
                          </p>
                        )
                      }
                      return liveList.map((session) => {
                        const state = toLifecycleState(session.sessionState)
                        return (
                          <button
                            key={session.liveSessionId}
                            className={styles.sessionButton}
                            type="button"
                            onClick={() => setSelectedSessionId(session.liveSessionId)}
                          >
                            <div className={styles.sessionCardHeader}>
                              <div>
                                <h3>{session.title}</h3>
                                <div className={styles.sessionCardMeta}>
                                  {session.sessionCode} • Programada {formatDateTime(session.scheduledAt)}
                                </div>
                              </div>
                              <span className={styles.chip} data-tone={state ? lifecycleTone[state] : 'muted'}>
                                {state ? lifecycleStateLabel[state] : session.sessionState}
                              </span>
                            </div>
                          </button>
                        )
                      })
                    })()}
                  </div>
                </article>
              </section>
            </>
          )}
        </main>
      </div>

      {toast && (
        <div aria-live="polite" className={styles.toast} role="status">
          <div className={styles.toastContent}>
            <div className={styles.toastTitle}>{toast.title}</div>
            <div className={styles.toastBody}>{toast.body}</div>
          </div>
          <button
            className={styles.toastClose}
            onClick={() => setToast(null)}
            type="button"
            aria-label="Descartar notificación"
          >
            ×
          </button>
        </div>
      )}
    </div>
  );
}

function UsersPanel({ role }: { role: DashboardRole }) {
  const [data, setData] = useState<PagedResult<UserAccessCatalogItemDto> | null>(null)
  const [page, setPage] = useState(1)
  const [isPending, startTransition] = useTransition()
  const [error, setError] = useState<string | null>(null)
  const [confirmId, setConfirmId] = useState<number | null>(null)
  const [roleEditId, setRoleEditId] = useState<number | null>(null)
  const [pendingRole, setPendingRole] = useState<string>('')
  const [roleError, setRoleError] = useState<string | null>(null)
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState<InvitableRole>('Operator')
  const [inviteError, setInviteError] = useState<string | null>(null)
  const [inviteNotice, setInviteNotice] = useState<string | null>(null)

  const loadUsers = useCallback(
    (target: number) => {
      startTransition(async () => {
        setError(null)
        try {
          const result = await getUsersPage(target)
          setData(result)
        } catch {
          setError('No se pudieron cargar los usuarios.')
        }
      })
    },
    [startTransition],
  )

  // Fetch on mount and page change
  useEffect(() => {
    loadUsers(page)
  }, [page, loadUsers])

  async function handleInvite() {
    const email = inviteEmail.trim()
    setInviteError(null)
    setInviteNotice(null)
    if (!email) {
      setInviteError('Ingresa una dirección de correo para invitar.')
      return
    }
    startTransition(async () => {
      let result
      try {
        result = await inviteUser(email, inviteRole)
      } catch {
        // The action returns expected failures as data; a throw here is an unexpected
        // infrastructure error (e.g. the session expired mid-request).
        setInviteError('No se pudo enviar la invitación. Inténtalo de nuevo.')
        return
      }
      if (!result.ok) {
        setInviteError(result.error)
        return
      }
      setInviteNotice(`Invitación enviada a ${email}. Aparecerá en la lista de usuarios como invitación pendiente una vez que la cuenta se aprovisione; con muchos usuarios podría estar en una página posterior.`)
      setInviteEmail('')
      setInviteRole('Operator')
      // Re-read from the first page so the newly invited (pending) account is visible immediately.
      if (page === 1) {
        loadUsers(1)
      } else {
        setPage(1)
      }
    })
  }

  async function handleDeactivate(id: number) {
    startTransition(async () => {
      setError(null)
      try {
        await deactivateUser(id)
        // Optimistic update: mark row inactive
        setData((prev) =>
          prev
            ? {
                ...prev,
                items: prev.items.map((u) =>
                  u.id === id ? { ...u, isActive: false } : u
                ),
              }
            : prev
        )
        setConfirmId(null)
      } catch {
        setError('Falló la desactivación. Inténtalo de nuevo.')
        setConfirmId(null)
      }
    })
  }

  async function handleRoleChange(id: number, previousRole: string) {
    startTransition(async () => {
      setRoleError(null)
      try {
        await assignUserRole(id, pendingRole)
        setData((prev) =>
          prev
            ? {
                ...prev,
                items: prev.items.map((u) =>
                  u.id === id ? { ...u, role: pendingRole } : u
                ),
              }
            : prev
        )
        setRoleEditId(null)
      } catch {
        setRoleError('Falló el cambio de rol. Inténtalo de nuevo.')
        setPendingRole(previousRole)
        setRoleEditId(null)
      }
    })
  }

  return (
    <section
      className={styles.panel}
      aria-labelledby="users-panel-title"
      data-testid="users-panel"
    >
      <div className={styles.panelHeader}>
        <div>
          <h2 id="users-panel-title">Usuarios registrados</h2>
          <div className={styles.panelMeta}>
            {role === 'admin'
              ? 'Cuentas de administrador y operador. Los usuarios desactivados no pueden iniciar sesión.'
              : 'Cuentas registradas visibles para los operadores.'}
          </div>
        </div>
        {isPending && <span className={styles.chip}>Cargando…</span>}
      </div>

      {role === 'admin' && (
        <form
          className={styles.formGroup}
          data-testid="invite-user-form"
          onSubmit={(e) => { e.preventDefault(); void handleInvite() }}
        >
          <div className={styles.fieldLabel}>Invitar a un usuario</div>
          <div className={styles.panelMeta}>
            La persona invitada define su propia contraseña desde el correo que recibe; aquí no se establece ninguna contraseña.
          </div>

          <label>
            <span>Correo</span>
            <input
              className={styles.formInput}
              data-testid="invite-email-input"
              type="email"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              placeholder="nombre@ejemplo.com"
              disabled={isPending}
            />
          </label>

          <label>
            <span>Rol</span>
            <select
              className={styles.inlineSelect}
              data-testid="invite-role-select"
              value={inviteRole}
              onChange={(e) => setInviteRole(e.target.value as InvitableRole)}
              disabled={isPending}
            >
              <option value="Operator">Operador</option>
              <option value="Administrator">Administrador</option>
            </select>
          </label>

          {inviteError && (
            <span className={styles.fieldError} role="alert" data-testid="invite-error">
              {inviteError}
            </span>
          )}

          {inviteNotice && (
            <span className={styles.panelMeta} role="status" data-testid="invite-notice">
              {inviteNotice}
            </span>
          )}

          <div className={styles.panelActions}>
            <button
              className={styles.primaryButton}
              data-testid="invite-submit"
              type="submit"
              disabled={isPending}
            >
              {isPending ? 'Enviando…' : 'Enviar invitación'}
            </button>
          </div>
        </form>
      )}

      {error && (
        <p className={styles.errorBanner} role="alert">
          {error}
        </p>
      )}

      {roleError && (
        <p className={styles.errorBanner} role="alert">
          {roleError}
        </p>
      )}

      {data && (
        <>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Correo</th>
                <th>Rol</th>
                <th>Estado</th>
                {role === 'admin' && <th>Acciones</th>}
              </tr>
            </thead>
            <tbody>
              {data.items.map((user) => {
                const status = getAccountStatus(user)
                return (
                <tr key={user.id} data-testid={`user-row-${user.id}`} data-status={status}>
                  <td data-label="Nombre">{status === 'pending' ? <span className={styles.mutedText}>Registro pendiente</span> : user.displayName}</td>
                  <td data-label="Correo">{user.email}</td>
                  <td data-label="Rol">
                    {role === 'admin' && roleEditId === user.id ? (
                      <select
                        className={styles.inlineSelect}
                        data-testid={`role-select-${user.id}`}
                        value={pendingRole}
                        onChange={(e) => setPendingRole(e.target.value)}
                      >
                        <option value="Administrator">Administrador</option>
                        <option value="Operator">Operador</option>
                        <option value="Participant">Participante</option>
                      </select>
                    ) : (
                      displayRole(user.role)
                    )}
                  </td>
                  <td data-label="Estado">
                    <span
                      className={styles.chip}
                      data-tone={accountStatusTone[status]}
                      data-testid={`user-status-${user.id}`}
                    >
                      {accountStatusLabel[status]}
                    </span>
                  </td>
                  {role === 'admin' && (
                    <td data-label="Acciones">
                      {user.isActive && confirmId !== user.id && roleEditId !== user.id && (
                        <button
                          className={styles.inlineButton}
                          data-testid={`deactivate-btn-${user.id}`}
                          disabled={isPending}
                          onClick={() => setConfirmId(user.id)}
                          type="button"
                        >
                          Desactivar
                        </button>
                      )}
                      {user.isActive && confirmId === user.id && (
                        <span className={styles.confirmRow}>
                          <button
                            className={styles.smallButton}
                            data-testid={`confirm-deactivate-btn-${user.id}`}
                            data-tone="critical"
                            disabled={isPending}
                            onClick={() => handleDeactivate(user.id)}
                            type="button"
                          >
                            Confirmar
                          </button>
                          <button
                            className={styles.inlineButton}
                            disabled={isPending}
                            onClick={() => setConfirmId(null)}
                            type="button"
                          >
                            Cancelar
                          </button>
                        </span>
                      )}
                      {/* Role edit trigger — only shown when not already in deactivate confirm mode */}
                      {user.isActive && confirmId !== user.id && roleEditId !== user.id && (
                        <button
                          className={styles.inlineButton}
                          data-testid={`change-role-btn-${user.id}`}
                          disabled={isPending}
                          onClick={() => { setRoleEditId(user.id); setPendingRole(user.role) }}
                          type="button"
                        >
                          Cambiar rol
                        </button>
                      )}
                      {/* Role save/cancel — only shown when this row is in role-edit mode */}
                      {roleEditId === user.id && (
                        <span className={styles.confirmRow}>
                          <button
                            className={styles.smallButton}
                            data-testid={`save-role-btn-${user.id}`}
                            disabled={isPending || pendingRole === user.role}
                            onClick={() => handleRoleChange(user.id, user.role)}
                            type="button"
                          >
                            Guardar
                          </button>
                          <button
                            className={styles.inlineButton}
                            disabled={isPending}
                            onClick={() => { setRoleEditId(null); setRoleError(null) }}
                            type="button"
                          >
                            Cancelar
                          </button>
                        </span>
                      )}
                      {!user.isActive && (
                        <span className={styles.mutedText}>—</span>
                      )}
                    </td>
                  )}
                </tr>
                )
              })}
            </tbody>
          </table>

          <div className={styles.pagination} data-testid="users-pagination">
            <span className={styles.panelMeta}>
              Página {data.page} de {data.totalPages} ({data.totalCount} usuarios)
            </span>
            <span className={styles.paginationButtons}>
              <button
                className={styles.inlineButton}
                disabled={!data.hasPreviousPage || isPending}
                onClick={() => setPage((p) => p - 1)}
                type="button"
              >
                ← Anterior
              </button>
              <button
                className={styles.inlineButton}
                disabled={!data.hasNextPage || isPending}
                onClick={() => setPage((p) => p + 1)}
                type="button"
              >
                Siguiente →
              </button>
            </span>
          </div>
        </>
      )}
    </section>
  )
}

function CompassMark() {
  return (
    <svg aria-hidden="true" height="34" viewBox="0 0 34 34" width="34">
      <circle cx="17" cy="17" fill="none" r="15.5" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="17" cy="17" fill="none" r="10.4" opacity="0.35" stroke="currentColor" strokeWidth="1" />
      <path d="M17 4.5 19.8 14.2 29.5 17 19.8 19.8 17 29.5 14.2 19.8 4.5 17 14.2 14.2Z" fill="none" stroke="currentColor" strokeLinejoin="round" strokeWidth="1.15" />
      <path d="M17 7.5V26.5M7.5 17H26.5" opacity="0.42" stroke="currentColor" strokeLinecap="round" strokeWidth="1" />
    </svg>
  );
}
