'use client';

import { useEffect, useState, useTransition } from 'react';
import { logout } from '@/app/actions/auth';
import { refreshSession } from '@/app/actions/session';
import { getUsersPage, deactivateUser, assignUserRole } from '@/app/actions/users';
import { TeamsPanel } from './TeamsPanel'
import { TriviasPanel } from './TriviasPanel'
import { SessionsPanel } from './SessionsPanel'
import { SessionOperatorPanel } from './SessionOperatorPanel'
import type { PagedResult, UserAccessCatalogItemDto } from '@/app/lib/definitions';
import styles from './dashboard.module.css';

type DashboardRole = 'operator' | 'admin' | 'participant';
type Theme = 'dark' | 'light';
type SessionState = 'live' | 'paused' | 'draft';

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
  const sessionStateMap: Record<string, SessionState> = Object.fromEntries(
    sessions.map((session) => [session.id, session.state])
  );
  const activeAssignedSession = sessions.find(
    (session) => session.assignedToOperator && session.recent && sessionStateMap[session.id] !== 'draft'
  );
  return activeAssignedSession ? activeAssignedSession.id : 'assigned-list';
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
    role === 'operator' ? 'operator' : 'overview'
  );
  const [isConnected, setIsConnected] = useState(true);
  const [toast, setToast] = useState<{ title: string; body: string } | null>(null);
  const [sessionStateMap, setSessionStateMap] = useState<Record<string, SessionState>>(() =>
    Object.fromEntries(sessions.map((session) => [session.id, session.state]))
  );
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

  const activeSession = sessions.find((session) => session.id === selectedSessionId);
  const derivedSession = activeSession
    ? { ...activeSession, state: sessionStateMap[activeSession.id] }
    : null;
  function announce(title: string, body: string) {
    setToast({ title, body });
  }

  function toggleTheme() {
    setTheme((current) => (current === 'dark' ? 'light' : 'dark'));
  }

  function toggleConnection() {
    setIsConnected((current) => {
      const next = !current;
      announce(next ? 'SignalR link restored' : 'SignalR link lost', next ? 'Live updates resumed across the control room.' : 'Realtime updates paused. Last synced 8 seconds ago.');
      return next;
    });
  }

  function updateSessionState(nextState: SessionState) {
    if (!derivedSession) {
      return;
    }

    setSessionStateMap((current) => ({ ...current, [derivedSession.id]: nextState }));

    const messages: Record<SessionState, { title: string; body: string }> = {
      draft: {
        title: `${derivedSession.title} returned to draft`,
        body: 'The session is no longer visible to operators on the live board.',
      },
      live: {
        title: `${derivedSession.title} is live`,
        body: `Teams are active and ${derivedSession.questionsAnswered} of ${derivedSession.questionsTotal} questions have been answered.`,
      },
      paused: {
        title: `${derivedSession.title} paused`,
        body: 'Countdown timers are frozen while operators resolve the issue.',
      },
    };

    announce(messages[nextState].title, messages[nextState].body);
  }

  const statusTone = !isConnected ? 'critical' : derivedSession?.state === 'paused' ? 'warning' : 'success';

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
                <span className={styles.statusDot} aria-hidden="true" data-tone={isConnected ? 'success' : 'critical'} />
                <span>{isConnected ? 'Good' : 'Needs attention'}</span>
              </div>
              <p className={styles.healthText}>
                {isConnected ? 'SignalR is healthy, validator sync is stable.' : 'Realtime transport dropped. Hold question reveals until sync recovers.'}
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
                    {derivedSession
                      ? `${derivedSession.title} is part of your current workload. Create new sessions here, or select one to continue setup before live operation.`
                      : 'Review the sessions you own, create a new one from valid content, or ask an administrator to reassign an existing session.'}
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
                      {role === 'operator' && <option value="assigned-list">My sessions</option>}
                      {sessions.map((session) => (
                        <option key={session.id} value={session.id}>
                          {session.title}
                        </option>
                      ))}
                    </select>
                  </label>
                  <span className={!isConnected ? styles.chip : derivedSession?.state === 'live' ? styles.liveChip : styles.chip} data-tone={!isConnected ? 'critical' : derivedSession?.state === 'live' ? undefined : 'warning'}>
                    {!isConnected ? 'Signal lost' : derivedSession ? stateLabels[derivedSession.state] : 'Live'}
                  </span>
                  <span className={styles.panelMeta}>
                    {derivedSession ? `Started ${derivedSession.startedAt}` : 'Choose a session to inspect'}
                  </span>
                </>
              )}
            </div>

            <div className={styles.topbarRight}>
              <div className={styles.statusRow}>
                <button className={styles.statusToggle} onClick={toggleConnection} type="button">
                  {isConnected ? 'SignalR' : 'Offline'}
                </button>
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
                    assignedSessions={sessions.filter((session) => session.assignedToOperator)}
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
                  Review the sessions you are responsible for, create a new one from valid content, or ask an administrator to reassign a session when the live floor changes.
                </p>
              </div>

              <div className={styles.sessionCards}>
                {sessions.filter((session) => session.assignedToOperator).map((session) => {
                  const sessionState = sessionStateMap[session.id];

                  return (
                    <button
                      key={session.id}
                      className={styles.sessionButton}
                      data-current={false}
                      onClick={() => setSelectedSessionId(session.id)}
                      type="button"
                    >
                      <div className={styles.sessionCardHeader}>
                        <div>
                          <h3>{session.title}</h3>
                          <div className={styles.sessionCardMeta}>
                            {session.subtitle} • {session.district} • {session.night}
                          </div>
                        </div>
                        <span className={styles.chip} data-tone={sessionState === 'live' ? 'success' : sessionState === 'paused' ? 'warning' : 'critical'}>
                          {stateLabels[sessionState]}
                        </span>
                      </div>
                    </button>
                  );
                })}
              </div>

              <div className={styles.emptyActions}>
                <button className={styles.primaryButton} onClick={() => setActiveNav('sessions')} type="button">
                  Create session
                </button>
                <button className={styles.secondaryButton} onClick={() => setSelectedSessionId('downtown-trivia')} type="button">
                  Open live session
                </button>
              </div>
            </section>
          ) : role === 'operator' && derivedSession ? (
            <>
              <section className={styles.hero} aria-labelledby="session-title" data-testid="operator-panel">
                <div className={styles.heroHeader}>
                  <div className={styles.heroTitleWrap}>
                    <div className={styles.heroMark} aria-hidden="true">
                      <CompassMark />
                    </div>

                    <div className={styles.heroHeading}>
                      <div className={styles.headerButtons}>
                        <h1 id="session-title">{derivedSession.title}</h1>
                        <span className={styles.chip} data-tone={statusTone}>
                          {!isConnected ? 'Signal lost' : stateLabels[derivedSession.state]}
                        </span>
                      </div>

                      <div className={styles.sessionMeta}>
                        <span>{derivedSession.subtitle}</span>
                        <span>•</span>
                        <span>{derivedSession.district}</span>
                        <span>•</span>
                        <span>{derivedSession.night}</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.heroMetrics}>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>{derivedSession.timeRemaining}</span>
                      <span className={styles.metricLabel}>Time remaining</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>
                        {derivedSession.teamsActive} <small>/ {derivedSession.teamsTotal}</small>
                      </span>
                      <span className={styles.metricLabel}>Teams active</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>
                        {derivedSession.questionsAnswered} <small>/ {derivedSession.questionsTotal}</small>
                      </span>
                      <span className={styles.metricLabel}>Answers submitted</span>
                    </div>
                  </div>
                </div>

                <div className={styles.heroActions}>
                  <button
                    className={styles.primaryButton}
                    onClick={() => updateSessionState(derivedSession.state === 'paused' ? 'live' : 'paused')}
                    type="button"
                  >
                    {derivedSession.state === 'paused' ? 'Resume session' : 'Pause session'}
                  </button>
                </div>
              </section>

              <div className={styles.contentGrid}>
              </div>

              <div className={styles.bottomGrid}>
                <section className={styles.panel} aria-labelledby="round-status-title">
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 id="round-status-title">Round status</h2>
                      <div className={styles.panelMeta}>Team response overview for the active question.</div>
                    </div>
                  </div>

                  <table className={styles.roundTable}>
                    <thead>
                      <tr>
                        <th>Team</th>
                        <th data-align="center">Status</th>
                        <th data-align="center">Answer</th>
                        <th data-align="center">Score</th>
                      </tr>
                    </thead>
                    <tbody>
                      {[
                        { team: 'Gilded Owls', badge: '🦉', status: 'Answered', answer: 'B', score: 40 },
                        { team: 'Maple Runners', badge: '🍁', status: 'Answered', answer: 'B', score: 40 },
                        { team: 'Cipher Secrets', badge: '🗝', status: 'Pending', answer: '—', score: 0 },
                        { team: 'The Wayfinders', badge: '🧭', status: 'Answered', answer: 'C', score: 10 },
                      ].map((row) => (
                        <tr key={row.team}>
                          <td data-label="Team">
                            <div className={styles.teamCell}>
                              <span className={styles.teamBadge}>{row.badge}</span>
                              <span>{row.team}</span>
                            </div>
                          </td>
                          <td data-align="center" data-label="Status">
                            <span className={styles.chip} data-tone={row.status === 'Answered' ? 'success' : 'warning'}>
                              {row.status}
                            </span>
                          </td>
                          <td data-align="center" data-label="Answer">{row.answer}</td>
                          <td data-align="center" data-label="Score">{row.score}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>

                <section className={styles.panel} aria-labelledby="session-controls-title">
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 id="session-controls-title">Session controls</h2>
                      <div className={styles.panelMeta}>High-stakes actions stay obvious, reversible where possible, and keyboard reachable.</div>
                    </div>
                  </div>

                  <div className={styles.controlGrid}>
                    <button className={styles.controlTile} onClick={() => updateSessionState('paused')} type="button">
                      <span className={styles.controlKicker}>Ⅱ</span>
                      <span className={styles.controlTitle}>Pause session</span>
                      <span className={styles.controlCopy}>Freeze countdown and stop accepting answers.</span>
                    </button>
                    <button className={styles.controlTile} onClick={() => updateSessionState('live')} type="button">
                      <span className={styles.controlKicker}>▷</span>
                      <span className={styles.controlTitle}>Resume session</span>
                      <span className={styles.controlCopy}>Return teams to live answering.</span>
                    </button>
                    <button className={styles.controlTile} data-tone="accent" type="button">
                      <span className={styles.controlKicker}>▶</span>
                      <span className={styles.controlTitle}>Next question</span>
                      <span className={styles.controlCopy}>Advance to the next trivia question.</span>
                    </button>
                    <button className={styles.controlTile} data-tone="success" type="button">
                      <span className={styles.controlKicker}>◉</span>
                      <span className={styles.controlTitle}>Reveal answer</span>
                      <span className={styles.controlCopy}>Show the correct answer to all teams.</span>
                    </button>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>✉</span>
                      <span className={styles.controlTitle}>Send announcement</span>
                      <span className={styles.controlCopy}>Broadcast delays, hints, or clarifications.</span>
                    </button>
                  </div>
                </section>
              </div>
            </>
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
                        <span className={styles.chip} data-tone={isConnected ? 'success' : 'critical'}>
                          {isConnected ? 'Global view live' : 'Signal degraded'}
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
                      Manage sessions
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
