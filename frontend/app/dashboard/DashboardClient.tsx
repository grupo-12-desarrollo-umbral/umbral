'use client';

import { useEffect, useReducer, useState, useTransition } from 'react';
import { logout } from '@/app/actions/auth';
import { refreshSession } from '@/app/actions/session';
import { getUsersPage, deactivateUser, assignUserRole } from '@/app/actions/users';
import { listSessionsForOperator, transitionSessionState, getSessionTimerSnapshotAction } from '@/app/actions/sessions';
import { TeamsPanel } from './TeamsPanel'
import { TriviasPanel } from './TriviasPanel'
import { SessionsPanel } from './SessionsPanel'
import { SessionOperatorPanel } from './SessionOperatorPanel'
import { OperatorSessionTimerPanel } from './OperatorSessionTimerPanel'
import { createSessionStateRealtimeClient, type SessionRealtimeStatus } from '@/app/lib/realtime/session-state-client'
import type {
  PagedResult,
  SessionAssignmentSummaryDto,
  SessionLifecycleState,
  SessionStateChangedNotificationDto,
  SessionTimerSnapshotDto,
  SessionTimerUpdatedNotificationDto,
  TransitionSessionStateResultDto,
  UserAccessCatalogItemDto,
} from '@/app/lib/definitions';
import styles from './dashboard.module.css';

type DashboardRole = 'operator' | 'admin' | 'participant';
type Theme = 'dark' | 'light';
type SessionState = 'live' | 'paused' | 'draft';

const transportStatusLabel: Record<SessionRealtimeStatus, string> = {
  Connected: 'Connected',
  Reconnecting: 'Reconnecting',
  Offline: 'Offline',
  AuthExpired: 'Auth expired',
};

type Session = {
  id: string;
  title: string;
  subtitle: string;
  district: string;
  night: string;
  state: SessionState;
  startedAt: string;
  timeRemaining: string;
  teamsActive: number;
  teamsTotal: number;
  questionsAnswered: number;
  questionsTotal: number;
  assignedToOperator: boolean;
  recent: boolean;
};

type ActivityEntry = {
  id: string;
  time: string;
  badge: string;
  team: string;
  message: string;
};

const navigation = [
  { key: 'overview', icon: '⌂' },
  { key: 'operator', icon: '◌' },
  { key: 'teams', icon: '◫' },
  { key: 'trivias', icon: '▤' },
  { key: 'sessions', icon: '▶' },
  { key: 'users', icon: '⊞' },
];

const sessions: Session[] = [
  {
    id: 'downtown-trivia',
    title: 'Downtown Trivia Night',
    subtitle: '12 Teams',
    district: 'Main Hall',
    night: 'Round 1',
    state: 'live',
    startedAt: '19:52',
    timeRemaining: '00:07:18',
    teamsActive: 12,
    teamsTotal: 12,
    questionsAnswered: 5,
    questionsTotal: 7,
    assignedToOperator: true,
    recent: true,
  },
  {
    id: 'science-quiz',
    title: 'Science Quiz Run',
    subtitle: '8 Teams',
    district: 'Lab Wing',
    night: 'Round 2',
    state: 'paused',
    startedAt: '18:20',
    timeRemaining: '00:12:40',
    teamsActive: 8,
    teamsTotal: 8,
    questionsAnswered: 4,
    questionsTotal: 7,
    assignedToOperator: true,
    recent: false,
  },
  {
    id: 'history-bowl',
    title: 'History Knowledge Bowl',
    subtitle: '10 Teams',
    district: 'Lecture Hall',
    night: 'Preview',
    state: 'draft',
    startedAt: 'Pending',
    timeRemaining: 'Not started',
    teamsActive: 0,
    teamsTotal: 10,
    questionsAnswered: 0,
    questionsTotal: 6,
    assignedToOperator: false,
    recent: false,
  },
];

const activity: ActivityEntry[] = [
  { id: 'activity-1', time: '8:00:36 PM', badge: '✺', team: 'Pocket Compass', message: 'submitted the first correct answer for Question 5' },
  { id: 'activity-2', time: '8:00:31 PM', badge: '❉', team: 'Iron Magnolias', message: 'submitted an answer that is awaiting validation' },
  { id: 'activity-3', time: '8:00:24 PM', badge: '🧭', team: 'The Wayfinders', message: 'answered Question 4 and advanced to Question 5' },
  { id: 'activity-4', time: '8:00:18 PM', badge: '🦉', team: 'Gilded Owls', message: 'scored 10 points on Question 3' },
  { id: 'activity-5', time: '8:00:15 PM', badge: '⬢', team: 'Red Herrings', message: 'requested a hint for Question 2' },
];

const adminMetrics = [
  { label: 'Active sessions', value: '2', hint: '1 live, 1 paused', pill: 'Live overview' },
  { label: 'Teams competing', value: '20', hint: 'Across tonight\'s trivia events', pill: 'Cross-session' },
  { label: 'Questions answered', value: '9', hint: '5 correct, 4 pending review', pill: 'Scoring' },
];

const stateLabels: Record<SessionState, string> = {
  draft: 'Draft',
  live: 'Live',
  paused: 'Paused',
};

const lifecycleStates = new Set<SessionLifecycleState>([
  'Scheduled',
  'Preparing',
  'Active',
  'Paused',
  'Finished',
  'Cancelled',
])

const lifecycleTone: Record<SessionLifecycleState, 'success' | 'warning' | 'critical' | 'muted'> = {
  Scheduled: 'muted',
  Preparing: 'warning',
  Active: 'success',
  Paused: 'warning',
  Finished: 'muted',
  Cancelled: 'critical',
}

const lifecycleActions: Record<
  SessionLifecycleState,
  Array<{
    label: string
    targetState: SessionLifecycleState
    description: string
    destructive?: boolean
    allowsReason?: boolean
  }>
> = {
  Scheduled: [
    { label: 'Prepare', targetState: 'Preparing', description: 'Open operator preparation for this session.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this scheduled session.', destructive: true, allowsReason: true },
  ],
  Preparing: [
    { label: 'Start', targetState: 'Active', description: 'Move teams into active answering.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this preparing session.', destructive: true, allowsReason: true },
  ],
  Active: [
    { label: 'Pause', targetState: 'Paused', description: 'Freeze the live session while preserving progress.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this live session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this live session.', destructive: true, allowsReason: true },
  ],
  Paused: [
    { label: 'Resume', targetState: 'Active', description: 'Return the paused session to active operation.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this paused session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this paused session.', destructive: true, allowsReason: true },
  ],
  Finished: [],
  Cancelled: [],
}

function toLifecycleState(value: string): SessionLifecycleState | null {
  return lifecycleStates.has(value as SessionLifecycleState) ? value as SessionLifecycleState : null
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return 'Not available'
  return new Date(value).toLocaleString()
}

function getPreferredTheme(): Theme {
  if (typeof window === 'undefined') {
    return 'dark';
  }

  const saved = window.localStorage.getItem('umbral-theme');
  if (saved === 'dark' || saved === 'light') {
    return saved;
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function getOperatorDefaultSession() {
  return 'assigned-list';
}

function getNavigationLabel(role: DashboardRole, key: string) {
  if (key === 'sessions') {
    return role === 'operator' ? 'My sessions' : 'Assign operators'
  }

  return (
    {
      overview: 'Overview',
      operator: 'Operator',
      teams: 'Teams',
      trivias: 'Trivias',
      users: 'Users',
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

export default function DashboardClient({
  role: initialRole,
  displayName,
}: {
  role: DashboardRole;
  displayName: string;
}) {
  const [role, setRole] = useState<DashboardRole>(initialRole);
  const [theme, setTheme] = useState<Theme>(() => getPreferredTheme());

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
    role === 'operator' ? getOperatorDefaultSession() : 'downtown-trivia'
  );
  const [activeNav, setActiveNav] = useState(() =>
    role === 'operator' ? 'sessions' : 'overview'
  );
  const [toast, setToast] = useState<{ title: string; body: string } | null>(null);
  const [operatorSessions, setOperatorSessions] = useState<SessionAssignmentSummaryDto[]>([]);
  const [operatorSessionsError, setOperatorSessionsError] = useState<string | null>(null);
  const [operatorSessionsRequestState, setOperatorSessionsRequestState] = useState<'idle' | 'loaded' | 'failed'>('idle');
  const [realtimeStatus, setRealtimeStatus] = useState<SessionRealtimeStatus>('Offline');
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [pendingTransition, setPendingTransition] = useState<SessionLifecycleState | null>(null);
  const [confirmTransition, setConfirmTransition] = useState<SessionLifecycleState | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [liveUpdateNote, setLiveUpdateNote] = useState<string | null>(null);
  const [timerState, dispatchTimer] = useReducer(timerReducer, { snapshot: null, error: null, loading: false })
  const isOperatorSessionsWorkspace = role === 'operator' && activeNav === 'sessions';

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    window.localStorage.setItem('umbral-theme', theme);
  }, [theme]);

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
          'Assigned sessions could not be loaded through the gateway. The backend needs an operator-authorized session listing endpoint.',
        )
        setOperatorSessionsRequestState('failed')
      })
  }, [role])

  const selectedOperatorSession =
    role === 'operator'
      ? operatorSessions.find((session) => session.liveSessionId === selectedSessionId) ?? null
      : null
  const selectedOperatorState = selectedOperatorSession
    ? toLifecycleState(selectedOperatorSession.sessionState)
    : null
  const activeSession = sessions.find((session) => session.id === selectedSessionId);
  const derivedSession = activeSession
    ? { ...activeSession }
    : null;

  // Key this effect on the stable session id, not the derived `selectedOperatorSession` object.
  // The object is re-created via `.find()` on every render, so depending on it tore down and
  // restarted the SignalR connection each render — aborting the in-flight negotiate ("connection
  // was stopped during negotiation"). Keying on the id connects once per selected session.
  const selectedRealtimeSessionId = selectedOperatorSession?.liveSessionId ?? null

  async function loadTimerSnapshot(liveSessionId: string) {
    dispatchTimer({ type: 'load' })
    const result = await getSessionTimerSnapshotAction(liveSessionId)
    if ('error' in result) {
      dispatchTimer({ type: 'failed', error: result.error })
    } else {
      dispatchTimer({ type: 'loaded', data: result.data })
    }
  }

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
        setLiveUpdateNote('State updated live from another client or tab.')
      },
      onTimerUpdated: (notification: SessionTimerUpdatedNotificationDto) => {
        if (notification.liveSessionId !== selectedRealtimeSessionId) return
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
          },
        })
      },
      onReconnected: () => {
        if (selectedRealtimeSessionId) void loadTimerSnapshot(selectedRealtimeSessionId)
      },
    })

    void client.start()
    return () => {
      void client.stop()
    }
  }, [selectedRealtimeSessionId])

  useEffect(() => {
    dispatchTimer({ type: 'reset' })
    if (!selectedRealtimeSessionId) return
    void loadTimerSnapshot(selectedRealtimeSessionId)
  }, [selectedRealtimeSessionId])

  function announce(title: string, body: string) {
    setToast({ title, body });
  }

  function toggleTheme() {
    setTheme((current) => (current === 'dark' ? 'light' : 'dark'));
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

  function mapTransitionError(err: unknown) {
    if (!(err instanceof Error)) return 'State transition failed. Try again.'

    return (
      {
        not_assigned_operator: 'You are not the assigned operator for this session.',
        session_not_found: 'Session not found. It may have been removed or reassigned.',
        no_teams: 'This session has no teams yet. Add at least one team before starting it.',
        session_unassigned: 'This session has no assigned operator. Ask an administrator to assign one before changing its state.',
        invalid_transition: 'That state change is not allowed from the current session state.',
        invalid_payload: 'Invalid state change payload. Reload the page and try again.',
      } satisfies Record<string, string>
    )[err.message] ?? 'State transition failed. Try again.'
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

    setPendingTransition(targetState)
    setTransitionError(null)
    try {
      const result = await transitionSessionState(
        selectedOperatorSession.liveSessionId,
        targetState,
        targetState === 'Cancelled' ? cancelReason : undefined,
      )
      applyTransitionResult(result)
      setConfirmTransition(null)
      setCancelReason('')
      announce(`${selectedOperatorSession.title} moved to ${result.currentState}`, 'The backend accepted the lifecycle transition.')
      if (result.timer) {
        dispatchTimer({ type: 'loaded', data: result.timer })
      } else {
        void loadTimerSnapshot(selectedOperatorSession.liveSessionId)
      }
    } catch (err) {
      setTransitionError(mapTransitionError(err))
    } finally {
      setPendingTransition(null)
    }
  }

  const statusTone =
    (selectedOperatorSession ? realtimeStatus : 'Offline') === 'Offline' ||
    (selectedOperatorSession ? realtimeStatus : 'Offline') === 'AuthExpired' ? 'critical'
    : selectedOperatorState ? lifecycleTone[selectedOperatorState]
    : derivedSession?.state === 'paused' ? 'warning'
    : 'success';
  const transportStatus = selectedOperatorSession ? realtimeStatus : 'Offline'
  const transportStatusText = transportStatusLabel[transportStatus]
  const realtimeAuthExpired = transportStatus === 'AuthExpired'
  const isLoadingOperatorSessions = role === 'operator' && operatorSessionsRequestState === 'idle'
  const healthIsGood = role !== 'operator' || transportStatus === 'Connected'

  const visibleNavigation = navigation.filter((item) => {
    if (role === 'participant') return item.key === 'overview'
    // HU-19: admins get the sessions nav for operator assignment; operator stub is dead
    if (role === 'admin') return item.key !== 'operator'
    if (role === 'operator') return item.key !== 'trivias' && item.key !== 'operator'
    return true
  })

  return (
    <div className={styles.page}>
      <a className={styles.skipLink} href="#main-content">
        Skip to main content
      </a>

      <div className={styles.frame}>
        <aside className={styles.sidebar} aria-label="Primary navigation">
          <div className={styles.brand}>
            <div className={styles.brandMark} aria-hidden="true">
              <CompassMark />
            </div>
            <div>
              <div className={styles.brandTitle}>Umbral</div>
              <div className={styles.brandMeta}>Command center</div>
            </div>
          </div>

          <div className={styles.rolePill} data-testid="role-chip">{role}</div>

          <nav className={styles.nav}>
            {visibleNavigation.map((item) => (
              <button
                key={item.key}
                className={styles.navItem}
                data-active={activeNav === item.key}
                data-testid={`nav-${item.key}`}
                onClick={() => setActiveNav(item.key)}
                type="button"
              >
                <span aria-hidden="true">{item.icon}</span>
                <span>{getNavigationLabel(role, item.key)}</span>
                <span className={styles.navMarker} aria-hidden="true" />
              </button>
            ))}
          </nav>

          <div className={styles.sidebarFooter}>
            <section className={styles.healthCard} aria-labelledby="session-health-title">
              <div className={styles.eyebrow} id="session-health-title">
                Session health
              </div>
              <div className={styles.healthStatus}>
                <span className={styles.statusDot} aria-hidden="true" data-tone={healthIsGood ? 'success' : 'critical'} />
                <span>{healthIsGood ? 'Good' : 'Needs attention'}</span>
              </div>
              <p className={styles.healthText}>
                {role === 'operator'
                  ? realtimeAuthExpired
                    ? 'Realtime authentication expired. Sign in again to restore live updates.'
                    : `Realtime transport: ${transportStatusText}.`
                  : 'SignalR is healthy, validator sync is stable.'}
              </p>

              <div className={styles.eventTime}>
                <div className={styles.eyebrow}>Event time</div>
                <div className={styles.clock}>8:00:42 PM</div>
                <div className={styles.healthText}>May 24, 2026</div>
              </div>
            </section>

            <button className={styles.collapseButton} type="button">
              <span aria-hidden="true">‹</span>
              Collapse
            </button>
          </div>
        </aside>

        <main className={styles.main} id="main-content">
          <section className={styles.topbar} aria-label="Dashboard controls">
            <div className={styles.topbarLeft}>
              {isOperatorSessionsWorkspace ? (
                <>
                  <span className={styles.chip}>My sessions</span>
                  <span className={styles.panelMeta}>
                    {selectedOperatorSession
                      ? `${selectedOperatorSession.title} is part of your current workload. Select one of your sessions to review it before live operation.`
                      : 'Review the sessions assigned to you, or ask an administrator to assign or reassign a session.'}
                  </span>
                </>
              ) : (
                <>
                  <label>
                    <span className="sr-only">Session switcher</span>
                    <select
                      className={styles.sessionSelect}
                      data-testid="session-switcher"
                      onChange={(event) => setSelectedSessionId(event.target.value)}
                      value={selectedSessionId}
                    >
                    {role === 'operator' ? (
                      <>
                        <option value="assigned-list">My sessions</option>
                        {operatorSessions.map((session) => (
                          <option key={session.liveSessionId} value={session.liveSessionId}>
                            {session.title}
                          </option>
                        ))}
                      </>
                    ) : (
                      sessions.map((session) => (
                        <option key={session.id} value={session.id}>
                          {session.title}
                        </option>
                      ))
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
                        : derivedSession?.state === 'live'
                          ? undefined
                          : 'warning'
                    }
                  >
                    {role === 'operator'
                      ? selectedOperatorState ?? transportStatusText
                      : derivedSession ? stateLabels[derivedSession.state] : 'Live'}
                  </span>
                  <span className={styles.panelMeta}>
                    {role === 'operator'
                      ? selectedOperatorSession
                        ? `Scheduled ${formatDateTime(selectedOperatorSession.scheduledAt)}`
                        : 'Choose a session to inspect'
                      : derivedSession ? `Started ${derivedSession.startedAt}` : 'Choose a session to inspect'}
                  </span>
                </>
              )}
            </div>

            <div className={styles.topbarRight}>
              <div className={styles.statusRow}>
                <span className={styles.statusToggle} data-testid="session-transport-status">
                  {role === 'operator' ? transportStatusText : 'SignalR'}
                </span>
              </div>

              <button className={styles.themeButton} onClick={toggleTheme} type="button" suppressHydrationWarning>
                {theme === 'dark' ? '☾' : '☼'}
              </button>

              <form action={logout}>
                <button
                  className={styles.smallButton}
                  type="submit"
                  data-testid="logout-button"
                  title="Sign out"
                >
                  Sign out
                </button>
              </form>
            </div>
          </section>

          {role === 'participant' ? (
            <section className={styles.emptyState} aria-labelledby="participant-title" data-testid="participant-panel">
              <div>
                <h1 id="participant-title">Welcome, {displayName}</h1>
                <p className={styles.emptyStateCopy}>
                  You are logged in as a participant. Operator and admin controls are not available
                  in this view.
                </p>
              </div>
            </section>
          ) : activeNav === 'users' ? (
            <UsersPanel role={role} />
          ) : activeNav === 'teams' ? (
            <TeamsPanel role={role} />
          ) : activeNav === 'trivias' ? (
            <TriviasPanel role={role} />
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
                <h1 id="assigned-sessions-title">My sessions</h1>
                <p className={styles.emptyStateCopy}>
                  Review the sessions you are responsible for, or ask an administrator to assign or reassign a session when the live floor changes.
                </p>
              </div>

              <div className={styles.sessionCards}>
                {operatorSessions.map((session) => {
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
                            {session.sessionCode} • Scheduled {formatDateTime(session.scheduledAt)}
                          </div>
                        </div>
                        <span className={styles.chip} data-tone={sessionState ? lifecycleTone[sessionState] : 'muted'}>
                          {sessionState ?? session.sessionState}
                        </span>
                      </div>
                    </button>
                  );
                })}
              </div>

              <div className={styles.emptyActions}>
                <button className={styles.primaryButton} onClick={() => operatorSessions[0] && setSelectedSessionId(operatorSessions[0].liveSessionId)} type="button" disabled={operatorSessions.length === 0}>
                  Open live session
                </button>
              </div>
            </section>
          ) : role === 'operator' && selectedOperatorSession && selectedOperatorState ? (
            <>
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
                          {selectedOperatorState}
                        </span>
                      </div>

                      <div className={styles.sessionMeta}>
                        <span>{selectedOperatorSession.sessionCode}</span>
                        <span>•</span>
                        <span>Scheduled {formatDateTime(selectedOperatorSession.scheduledAt)}</span>
                        <span>•</span>
                        <span>{selectedOperatorSession.assignedOperatorUserId == null ? 'Unassigned' : 'Assigned to you'}</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.heroMetrics}>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>{selectedOperatorState}</span>
                      <span className={styles.metricLabel}>Current state</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>{transportStatusText}</span>
                      <span className={styles.metricLabel}>Transport</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>
                        {selectedOperatorSession.lastTransitionedAt
                          ? new Date(selectedOperatorSession.lastTransitionedAt).toLocaleTimeString()
                          : 'None'}
                      </span>
                      <span className={styles.metricLabel}>Last transition</span>
                    </div>
                  </div>
                </div>

                <OperatorSessionTimerPanel
                  timer={timerState.snapshot}
                  isLoading={timerState.loading}
                  error={timerState.error}
                />

                {liveUpdateNote && (
                  <p className={styles.liveNote} role="status" data-testid="session-live-update-note">
                    {liveUpdateNote}
                  </p>
                )}

                {realtimeAuthExpired && (
                  <section className={styles.authBanner} role="alert" data-testid="session-auth-expired-banner">
                    <div>
                      <strong>Realtime authentication expired.</strong> Your dashboard session is still active, but SignalR cannot reconnect until you sign in again.
                    </div>
                    <div className={styles.authBannerActions}>
                      <a className={styles.primaryButton} href="/api/auth/login">
                        Sign in again
                      </a>
                      <form action={logout}>
                        <button className={styles.inlineButton} type="submit">
                          Sign out
                        </button>
                      </form>
                    </div>
                  </section>
                )}
              </section>

              <div className={styles.bottomGrid}>
                <section className={styles.panel} aria-labelledby="session-controls-title">
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 id="session-controls-title">Session controls</h2>
                      <div className={styles.panelMeta}>Allowed next actions are derived from the backend lifecycle state.</div>
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
                          {pendingTransition === action.targetState ? 'Working...' : action.label}
                        </span>
                        <span className={styles.controlCopy}>{action.description}</span>
                      </button>
                    ))}
                  </div>

                  {lifecycleActions[selectedOperatorState].length === 0 && (
                    <p className={styles.emptyStateCopy} data-testid="session-no-actions">
                      This session is terminal. No further lifecycle actions are available.
                    </p>
                  )}

                  {confirmTransition && (
                    <section className={styles.confirmPanel} aria-label="Confirm terminal transition">
                      <h3>Confirm {confirmTransition}</h3>
                      <p className={styles.panelMeta}>
                        This is a terminal lifecycle action. The backend will reject it if the transition is no longer valid.
                      </p>
                      {confirmTransition === 'Cancelled' && (
                        <label className={styles.reasonField}>
                          <span>Cancellation reason (optional)</span>
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
                          Confirm {confirmTransition}
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
                          Keep session open
                        </button>
                      </div>
                    </section>
                  )}
                </section>

                <section className={styles.panel} aria-labelledby="session-detail-title">
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 id="session-detail-title">Session detail</h2>
                      <div className={styles.panelMeta}>Backend summary for the selected operator session.</div>
                    </div>
                  </div>

                  <dl className={styles.sessionMeta}>
                    <dt>Title</dt>
                    <dd>{selectedOperatorSession.title}</dd>
                    <dt>Code</dt>
                    <dd>{selectedOperatorSession.sessionCode}</dd>
                    <dt>Scheduled time</dt>
                    <dd>{formatDateTime(selectedOperatorSession.scheduledAt)}</dd>
                    <dt>Ownership</dt>
                    <dd>{selectedOperatorSession.assignedOperatorUserId == null ? 'No assigned operator' : 'Assigned operator present'}</dd>
                    <dt>Last transition</dt>
                    <dd>{formatDateTime(selectedOperatorSession.lastTransitionedAt)}</dd>
                    <dt>Transport</dt>
                    <dd>{transportStatusText}</dd>
                  </dl>
                </section>
              </div>
            </>
          ) : role === 'operator' ? (
            <section className={styles.emptyState} aria-labelledby="operator-session-unavailable-title" data-testid="operator-panel">
              <div>
                <h1 id="operator-session-unavailable-title">No operator session selected</h1>
                <p className={styles.emptyStateCopy}>
                  Select one of your assigned sessions before opening lifecycle controls.
                </p>
              </div>
              <div className={styles.emptyActions}>
                <button className={styles.primaryButton} onClick={() => setActiveNav('sessions')} type="button">
                  Back to my sessions
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
                        <h1 id="admin-title">Trivia night overview</h1>
                        <span className={styles.chip} data-tone="success">
                          Global view live
                        </span>
                      </div>
                      <div className={styles.sessionMeta}>
                        <span>Tonight&apos;s trivia events</span>
                        <span>•</span>
                        <span>2 sessions active</span>
                        <span>•</span>
                        <span>20 teams competing</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.heroActions}>
                    <button className={styles.secondaryButton} type="button" onClick={() => setActiveNav('sessions')}>
                      Assign operators
                    </button>
                  </div>
                </div>
              </section>

              <section className={styles.adminMetrics} aria-label="Admin overview metrics">
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

              <section className={styles.adminPanels} aria-label="Admin control panels">
                <article className={`${styles.panel} ${styles.adminPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2>Configuration shortcuts</h2>
                      <div className={styles.panelMeta}>Quiz, team, and session management stays close to the top of the surface.</div>
                    </div>
                  </div>
                  <div className={styles.controlGrid}>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>▤</span>
                      <span className={styles.controlTitle}>Quizzes</span>
                      <span className={styles.controlCopy}>Balance trivia difficulty and answer timing.</span>
                    </button>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>◫</span>
                      <span className={styles.controlTitle}>Teams</span>
                      <span className={styles.controlCopy}>Review rosters, team badges, and check-in states.</span>
                    </button>
                  </div>
                </article>

                <article className={`${styles.panel} ${styles.adminPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2>Live sessions</h2>
                      <div className={styles.panelMeta}>Active trivia sessions and their current round progress.</div>
                    </div>
                  </div>
                  <div className={styles.sessionList}>
                    {sessions.filter((s) => s.state !== 'draft').map((session) => (
                      <button
                        key={session.id}
                        className={styles.sessionButton}
                        type="button"
                        onClick={() => setSelectedSessionId(session.id)}
                      >
                        <div className={styles.sessionCardHeader}>
                          <div>
                            <h3>{session.title}</h3>
                            <div className={styles.sessionCardMeta}>
                              {session.subtitle} • {session.questionsAnswered} of {session.questionsTotal} questions answered
                            </div>
                          </div>
                          <span className={styles.chip} data-tone={session.state === 'live' ? 'success' : 'warning'}>
                            {stateLabels[session.state]}
                          </span>
                        </div>
                      </button>
                    ))}
                  </div>
                </article>

                <article className={`${styles.panel} ${styles.adminPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2>Recent global activity</h2>
                      <div className={styles.panelMeta}>Read-only history across sessions, useful for supervising the live floor without taking over.</div>
                    </div>
                  </div>
                  <div className={styles.activityList}>
                    {activity.slice(0, 4).map((item) => (
                      <div className={styles.activityItem} key={item.id}>
                        <div className={styles.activityTime}>{item.time}</div>
                        <span className={styles.teamBadge}>{item.badge}</span>
                        <div className={styles.activityMain}>
                          <div>
                            <strong>{item.team}</strong> {item.message}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </article>
              </section>
            </>
          )}
        </main>
      </div>

      {toast && (
        <div aria-live="polite" className={styles.toast} role="status">
          <div className={styles.toastTitle}>{toast.title}</div>
          <div className={styles.toastBody}>{toast.body}</div>
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

  // Fetch on mount and page change
  useEffect(() => {
    startTransition(async () => {
      setError(null)
      try {
        const result = await getUsersPage(page)
        setData(result)
      } catch {
        setError('Failed to load users.')
      }
    })
  }, [page])

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
        setError('Deactivation failed. Try again.')
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
        setRoleError('Role change failed. Try again.')
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
          <h2 id="users-panel-title">Registered users</h2>
          <div className={styles.panelMeta}>
            {role === 'admin'
              ? 'Admin and operator accounts. Deactivated users cannot log in.'
              : 'Registered accounts visible to operators.'}
          </div>
        </div>
        {isPending && <span className={styles.chip}>Loading…</span>}
      </div>

      {error && (
        <div className={styles.chip} data-tone="critical">
          {error}
        </div>
      )}

      {roleError && (
        <div className={styles.chip} data-tone="critical">
          {roleError}
        </div>
      )}

      {data && (
        <>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                {role === 'admin' && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {data.items.map((user) => (
                <tr key={user.id}>
                  <td>{user.displayName}</td>
                  <td>{user.email}</td>
                  <td>
                    {role === 'admin' && roleEditId === user.id ? (
                      <select
                        className={styles.inlineSelect}
                        data-testid={`role-select-${user.id}`}
                        value={pendingRole}
                        onChange={(e) => setPendingRole(e.target.value)}
                      >
                        <option value="Administrator">Administrator</option>
                        <option value="Operator">Operator</option>
                        <option value="Participant">Participant</option>
                      </select>
                    ) : (
                      user.role
                    )}
                  </td>
                  <td>
                    <span
                      className={styles.chip}
                      data-tone={user.isActive ? 'success' : 'critical'}
                    >
                      {user.isActive ? 'Active' : 'Deactivated'}
                    </span>
                  </td>
                  {role === 'admin' && (
                    <td>
                      {user.isActive && confirmId !== user.id && roleEditId !== user.id && (
                        <button
                          className={styles.inlineButton}
                          data-testid={`deactivate-btn-${user.id}`}
                          disabled={isPending}
                          onClick={() => setConfirmId(user.id)}
                          type="button"
                        >
                          Deactivate
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
                            Confirm
                          </button>
                          <button
                            className={styles.inlineButton}
                            disabled={isPending}
                            onClick={() => setConfirmId(null)}
                            type="button"
                          >
                            Cancel
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
                          Change role
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
                            Save
                          </button>
                          <button
                            className={styles.inlineButton}
                            disabled={isPending}
                            onClick={() => { setRoleEditId(null); setRoleError(null) }}
                            type="button"
                          >
                            Cancel
                          </button>
                        </span>
                      )}
                      {!user.isActive && (
                        <span className={styles.mutedText}>—</span>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          <div className={styles.pagination} data-testid="users-pagination">
            <span className={styles.panelMeta}>
              Page {data.page} of {data.totalPages} ({data.totalCount} users)
            </span>
            <span className={styles.paginationButtons}>
              <button
                className={styles.inlineButton}
                disabled={!data.hasPreviousPage || isPending}
                onClick={() => setPage((p) => p - 1)}
                type="button"
              >
                ← Previous
              </button>
              <button
                className={styles.inlineButton}
                disabled={!data.hasNextPage || isPending}
                onClick={() => setPage((p) => p + 1)}
                type="button"
              >
                Next →
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
