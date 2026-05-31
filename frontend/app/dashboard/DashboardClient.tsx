'use client';

import { useEffect, useRef, useState, useTransition } from 'react';
import { logout } from '@/app/actions/auth';
import { refreshSession } from '@/app/actions/session';
import { getUsersPage, deactivateUser, assignUserRole } from '@/app/actions/users';
import type { PagedResult, UserAccessCatalogItemDto } from '@/app/lib/definitions';
import styles from './dashboard.module.css';

type DashboardRole = 'operator' | 'admin' | 'participant';
type Theme = 'dark' | 'light';
type SessionState = 'live' | 'paused' | 'draft';
type ReviewStatus = 'verified' | 'rejected' | 'pending';

type Session = {
  id: string;
  title: string;
  subtitle: string;
  district: string;
  night: string;
  state: SessionState;
  startedAt: string;
  nextClueCountdown: string;
  teamsActive: number;
  teamsTotal: number;
  cluesReleased: number;
  cluesTotal: number;
  assignedToOperator: boolean;
  recent: boolean;
};

type TeamEntry = {
  rank: number;
  name: string;
  badge: string;
  score: number;
  clues: string;
  updatedAt: string;
  delta: number;
};

type Submission = {
  id: string;
  team: string;
  badge: string;
  clue: string;
  target: string;
  status: ReviewStatus;
  validatedAt: string;
  timestamp: string;
  rejectionReason?: string;
  thumbnail: string;
  note: string;
};

type HistoryEntry = {
  id: string;
  title: string;
  time: string;
  complete: boolean;
};

type ActivityEntry = {
  id: string;
  time: string;
  badge: string;
  team: string;
  message: string;
};

type ProgressRow = {
  team: string;
  badge: string;
  checkpoints: Array<'done' | 'current' | 'pending'>;
};

const navigation = [
  { key: 'overview', label: 'Overview', icon: '⌂' },
  { key: 'operator', label: 'Operator', icon: '◌' },
  { key: 'map', label: 'Map', icon: '⌖' },
  { key: 'clues', label: 'Clues', icon: '✦' },
  { key: 'evidence', label: 'Evidence', icon: '▣' },
  { key: 'teams', label: 'Teams', icon: '◫' },
  { key: 'missions', label: 'Missions', icon: '▤' },
  { key: 'rules', label: 'Rules', icon: '⚑' },
  { key: 'users', label: 'Users', icon: '⊞' },
  { key: 'settings', label: 'Settings', icon: '⚙' },
];

const sessions: Session[] = [
  {
    id: 'verdant-keys',
    title: 'Verdant Keys Hunt',
    subtitle: '12 Teams',
    district: 'Downtown Expedition',
    night: 'Night 1',
    state: 'live',
    startedAt: '19:52',
    nextClueCountdown: '00:07:18',
    teamsActive: 12,
    teamsTotal: 12,
    cluesReleased: 5,
    cluesTotal: 7,
    assignedToOperator: true,
    recent: true,
  },
  {
    id: 'clockwork-river',
    title: 'Clockwork River Rally',
    subtitle: '8 Teams',
    district: 'Canal Quarter',
    night: 'Night 2',
    state: 'paused',
    startedAt: '18:20',
    nextClueCountdown: '00:12:40',
    teamsActive: 8,
    teamsTotal: 8,
    cluesReleased: 4,
    cluesTotal: 7,
    assignedToOperator: true,
    recent: false,
  },
  {
    id: 'lantern-garden',
    title: 'Lantern Garden Sprint',
    subtitle: '10 Teams',
    district: 'Museum Mile',
    night: 'Preview Run',
    state: 'draft',
    startedAt: 'Pending',
    nextClueCountdown: 'Not started',
    teamsActive: 0,
    teamsTotal: 10,
    cluesReleased: 0,
    cluesTotal: 6,
    assignedToOperator: false,
    recent: false,
  },
];

const leaderboard: TeamEntry[] = [
  { rank: 1, name: 'Gilded Owls', badge: '🦉', score: 1820, clues: '5 / 7', updatedAt: '8:00:40 PM', delta: 2 },
  { rank: 2, name: 'Maple Runners', badge: '🍁', score: 1560, clues: '5 / 7', updatedAt: '8:00:39 PM', delta: 1 },
  { rank: 3, name: 'Cipher Secrets', badge: '🗝', score: 1410, clues: '4 / 7', updatedAt: '8:00:32 PM', delta: -1 },
  { rank: 4, name: 'The Wayfinders', badge: '🧭', score: 1380, clues: '4 / 7', updatedAt: '8:00:35 PM', delta: 3 },
  { rank: 5, name: 'Pocket Compass', badge: '✺', score: 1260, clues: '4 / 7', updatedAt: '8:00:30 PM', delta: -1 },
  { rank: 6, name: 'The Curious Quill', badge: '✒', score: 1180, clues: '4 / 7', updatedAt: '8:00:28 PM', delta: 0 },
  { rank: 7, name: 'Iron Magnolias', badge: '❉', score: 1090, clues: '3 / 7', updatedAt: '8:00:26 PM', delta: 0 },
  { rank: 8, name: 'Brass Lanterns', badge: '☼', score: 980, clues: '3 / 7', updatedAt: '8:00:24 PM', delta: 0 },
  { rank: 9, name: 'Lost & Found', badge: '◈', score: 910, clues: '3 / 7', updatedAt: '8:00:20 PM', delta: 0 },
  { rank: 10, name: 'Fourth Wall', badge: '▣', score: 780, clues: '2 / 7', updatedAt: '8:00:18 PM', delta: 1 },
  { rank: 11, name: 'Red Herrings', badge: '⬢', score: 620, clues: '2 / 7', updatedAt: '8:00:17 PM', delta: -1 },
  { rank: 12, name: 'Tea & Tactics', badge: '☕', score: 410, clues: '1 / 7', updatedAt: '8:00:15 PM', delta: 0 },
];

const submissions: Submission[] = [
  {
    id: 'submission-1',
    team: 'Pocket Compass',
    badge: '✺',
    clue: 'Clue 5',
    target: 'The Hidden Scale',
    status: 'verified',
    validatedAt: '8:00:36 PM',
    timestamp: '8:00:36 PM',
    thumbnail: 'Gate Arch',
    note: 'QR scan matched checkpoint signature and team assignment.',
  },
  {
    id: 'submission-2',
    team: 'Iron Magnolias',
    badge: '❉',
    clue: 'Clue 4',
    target: "Lantern's Reflection",
    status: 'pending',
    validatedAt: 'Awaiting sync',
    timestamp: '8:00:31 PM',
    thumbnail: 'Brass Plaque',
    note: 'SignalR event received, awaiting final backend validation packet.',
  },
  {
    id: 'submission-3',
    team: 'The Wayfinders',
    badge: '🧭',
    clue: 'Clue 4',
    target: "Lantern's Reflection",
    status: 'rejected',
    validatedAt: '8:00:29 PM',
    timestamp: '8:00:29 PM',
    rejectionReason: 'Wrong target marker, scan resolved to checkpoint 3A.',
    thumbnail: 'Marker 732',
    note: 'Rejection reason pushed from backend validator, no operator action required.',
  },
];

const clueHistory: HistoryEntry[] = [
  { id: 'history-1', title: 'The Hidden Scale', time: '7:41 PM', complete: true },
  { id: 'history-2', title: "Lantern's Reflection", time: '7:22 PM', complete: true },
  { id: 'history-3', title: 'Echo in the Market', time: '7:02 PM', complete: true },
  { id: 'history-4', title: 'Footsteps in the Stone', time: '6:41 PM', complete: true },
  { id: 'history-5', title: 'The First Whisper', time: '6:20 PM', complete: true },
];

const activity: ActivityEntry[] = [
  { id: 'activity-1', time: '8:00:36 PM', badge: '✺', team: 'Pocket Compass', message: 'submitted a verified QR checkpoint for Clue 5' },
  { id: 'activity-2', time: '8:00:31 PM', badge: '❉', team: 'Iron Magnolias', message: 'submitted a checkpoint that is waiting on validator sync' },
  { id: 'activity-3', time: '8:00:24 PM', badge: '🧭', team: 'The Wayfinders', message: 'completed Clue 4 and advanced to Mission Step 5' },
  { id: 'activity-4', time: '8:00:18 PM', badge: '🦉', team: 'Gilded Owls', message: 'found the checkpoint at Old Post Office' },
  { id: 'activity-5', time: '8:00:15 PM', badge: '⬢', team: 'Red Herrings', message: 'requested a hint for Clue 2' },
];

const progressRows: ProgressRow[] = [
  { team: 'Gilded Owls', badge: '🦉', checkpoints: ['done', 'done', 'done', 'done', 'current', 'pending', 'pending'] },
  { team: 'Maple Runners', badge: '🍁', checkpoints: ['done', 'done', 'done', 'done', 'current', 'pending', 'pending'] },
  { team: 'Cipher Secrets', badge: '🗝', checkpoints: ['done', 'done', 'done', 'current', 'pending', 'pending', 'pending'] },
  { team: 'The Wayfinders', badge: '🧭', checkpoints: ['done', 'done', 'done', 'done', 'current', 'pending', 'pending'] },
];

const adminMetrics = [
  { label: 'Active sessions', value: '7', hint: '2 need operator reassignment', pill: 'Live overview' },
  { label: 'Teams in the field', value: '64', hint: 'Across tonight\'s events', pill: 'Cross-session' },
  { label: 'Pending issues', value: '3', hint: '1 validator retry, 2 route escalations', pill: 'Needs review' },
];

const operatorAssignments = [
  'Avery Knight → Verdant Keys Hunt, Clockwork River Rally',
  'Noor Vale → Gilt Arcade Sprint',
  'Tomas Reed → Harbor Cipher Run',
];

const stateLabels: Record<SessionState, string> = {
  draft: 'Draft',
  live: 'Live',
  paused: 'Paused',
};

const reviewTones: Record<ReviewStatus, 'success' | 'warning' | 'critical'> = {
  pending: 'warning',
  rejected: 'critical',
  verified: 'success',
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

function getInitials(name: string): string {
  return name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
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
        if (mapped !== role) {
          setRole(mapped);
        }
      }
    })
  }, []);
  const [selectedSessionId, setSelectedSessionId] = useState(() =>
    role === 'operator' ? getOperatorDefaultSession() : 'verdant-keys'
  );
  const [activeNav, setActiveNav] = useState(() =>
    role === 'operator' ? 'operator' : 'overview'
  );
  const [isConnected, setIsConnected] = useState(true);
  const [toast, setToast] = useState<{ title: string; body: string } | null>(null);
  const [selectedSubmissionId, setSelectedSubmissionId] = useState<string | null>(null);
  const [selectedClueTeamNames, setSelectedClueTeamNames] = useState<string[]>(() => leaderboard.map((team) => team.name));
  const [isTeamDropdownOpen, setIsTeamDropdownOpen] = useState(false);
  const teamDropdownRef = useRef<HTMLDivElement>(null);
  const [sessionStateMap, setSessionStateMap] = useState<Record<string, SessionState>>(() =>
    Object.fromEntries(sessions.map((session) => [session.id, session.state]))
  );

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
    function closeTeamDropdown(event: PointerEvent) {
      if (!teamDropdownRef.current?.contains(event.target as Node)) {
        setIsTeamDropdownOpen(false);
      }
    }

    if (!isTeamDropdownOpen) {
      return;
    }

    document.addEventListener('pointerdown', closeTeamDropdown);
    return () => document.removeEventListener('pointerdown', closeTeamDropdown);
  }, [isTeamDropdownOpen]);

  const selectedSubmission = submissions.find((submission) => submission.id === selectedSubmissionId) ?? null;
  const activeSession = sessions.find((session) => session.id === selectedSessionId);
  const derivedSession = activeSession
    ? { ...activeSession, state: sessionStateMap[activeSession.id] }
    : null;
  const selectedClueTeams = leaderboard.filter((team) => selectedClueTeamNames.includes(team.name));
  const clueRecipientLabel =
    selectedClueTeams.length === leaderboard.length
      ? `All active teams (${leaderboard.length})`
      : selectedClueTeams.length > 0
        ? `${selectedClueTeams.length} selected team${selectedClueTeams.length === 1 ? '' : 's'}`
        : 'No teams selected';

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
        body: `Teams are active and ${derivedSession.cluesReleased} clues remain in circulation.`,
      },
      paused: {
        title: `${derivedSession.title} paused`,
        body: 'Countdown timers are frozen while operators resolve the issue.',
      },
    };

    announce(messages[nextState].title, messages[nextState].body);
  }

  function releaseClue() {
    if (!derivedSession) {
      return;
    }

    if (selectedClueTeams.length === 0) {
      announce('Choose at least one team', 'Clue 6 was not released because no recipients are selected.');
      return;
    }

    const recipientCopy =
      selectedClueTeams.length === leaderboard.length
        ? `all ${derivedSession.teamsActive} active teams`
        : selectedClueTeams.map((team) => team.name).join(', ');

    announce('Clue 6 released', `The next clue went out to ${recipientCopy}.`);
  }

  function toggleClueTeam(teamName: string) {
    setSelectedClueTeamNames((current) =>
      current.includes(teamName) ? current.filter((name) => name !== teamName) : [...current, teamName]
    );
  }

  function openSubmission(id: string) {
    setSelectedSubmissionId(id);
  }

  function closeSubmission() {
    setSelectedSubmissionId(null);
  }

  const statusTone = !isConnected ? 'critical' : derivedSession?.state === 'paused' ? 'warning' : 'success';

  const visibleNavigation = navigation.filter((item) => {
    if (role === 'participant') return item.key === 'overview'
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
                <span>{item.label}</span>
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
                {isConnected ? 'SignalR is healthy, validator sync is stable.' : 'Realtime transport dropped. Hold clue releases until sync recovers.'}
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
              <label>
                <span className="sr-only">Session switcher</span>
                <select
                  className={styles.sessionSelect}
                  onChange={(event) => setSelectedSessionId(event.target.value)}
                  value={selectedSessionId}
                >
                  {role === 'operator' && <option value="assigned-list">Assigned sessions</option>}
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
          ) : role === 'operator' && selectedSessionId === 'assigned-list' ? (
            <section className={styles.emptyState} aria-labelledby="assigned-sessions-title" data-testid="operator-panel">
              <div>
                <h1 id="assigned-sessions-title">Assigned sessions</h1>
                <p className={styles.emptyStateCopy}>
                  Operators default to the most recently assigned active session. If none are active, this list becomes the fallback workspace so the handoff never feels abrupt.
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
                <button className={styles.primaryButton} onClick={() => setSelectedSessionId('verdant-keys')} type="button">
                  Return to live session
                </button>
                {/* Removed hardcoded admin switch; role is session-bound */}
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
                      <span className={styles.metricValue}>{derivedSession.nextClueCountdown}</span>
                      <span className={styles.metricLabel}>Until next clue</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>
                        {derivedSession.teamsActive} <small>/ {derivedSession.teamsTotal}</small>
                      </span>
                      <span className={styles.metricLabel}>Teams active</span>
                    </div>
                    <div className={styles.metricBlock}>
                      <span className={styles.metricValue}>
                        {derivedSession.cluesReleased} <small>/ {derivedSession.cluesTotal}</small>
                      </span>
                      <span className={styles.metricLabel}>Clues released</span>
                    </div>
                  </div>
                </div>

                <div className={styles.heroActions}>
                  <button
                    className={styles.primaryButton}
                    onClick={() => updateSessionState(derivedSession.state === 'paused' ? 'live' : 'paused')}
                    type="button"
                  >
                    {derivedSession.state === 'paused' ? 'Resume hunt' : 'Pause hunt'}
                  </button>
                </div>
              </section>

              <div className={styles.contentGrid}>
                <div className={styles.stack}>
                  <section className={styles.panel} aria-labelledby="leaderboard-title">
                    <div className={styles.panelHeader}>
                      <div>
                        <h2 id="leaderboard-title">Live leaderboard</h2>
                        <div className={styles.panelMeta}>SignalR updates every 15 seconds, score deltas surface instantly.</div>
                      </div>
                      <span className={derivedSession.state === 'live' ? styles.liveChip : styles.chip} data-tone={derivedSession.state === 'live' ? undefined : 'warning'}>
                        {stateLabels[derivedSession.state]}
                      </span>
                    </div>

                    <table className={styles.table}>
                      <thead>
                        <tr>
                          <th>#</th>
                          <th>Team</th>
                          <th>Score</th>
                          <th>Clues</th>
                          <th>Last update</th>
                        </tr>
                      </thead>
                      <tbody>
                        {leaderboard.map((entry, index) => (
                          <tr className={index < 3 ? styles.tableRowAccent : undefined} key={entry.rank}>
                            <td data-label="#">{entry.rank}</td>
                            <td data-label="Team">
                              <div className={styles.teamCell}>
                                <span className={styles.teamBadge}>{entry.badge}</span>
                                <span>{entry.name}</span>
                              </div>
                            </td>
                            <td data-label="Score">{entry.score.toLocaleString()}</td>
                            <td data-label="Clues">{entry.clues}</td>
                            <td data-label="Last update">
                              {entry.updatedAt}{' '}
                              {entry.delta !== 0 && (
                                <span className={`${styles.delta} ${entry.delta > 0 ? styles.deltaUp : styles.deltaDown}`}>
                                  {entry.delta > 0 ? `▲ ${entry.delta}` : `▼ ${Math.abs(entry.delta)}`}
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </section>
                </div>

                <div className={styles.stack}>
                  <section className={styles.panel} aria-labelledby="clue-control-title">
                    <div className={styles.panelHeader}>
                      <div>
                        <h2 id="clue-control-title">Clue control</h2>
                        <div className={styles.panelMeta}>The next clue stays tactile and human, even in a dense operator surface.</div>
                      </div>
                    </div>

                    <div className={styles.clueCard}>
                      <div className={styles.clueMeta}>Next up: Clue 6 · The Gilded Gate</div>
                      <div className={styles.cluePreview}>
                        <p className={styles.clueText}>
                          Where iron meets ivy,
                          <br />a whisper opens the way.
                          <br />Find the keeper beneath
                          <br />the morning&apos;s sway.
                        </p>
                      </div>
                      <div className={styles.recipientPicker}>
                        <div className={styles.recipientHeader}>
                          <div>
                            <div className={styles.fieldLabel}>Release to</div>
                            <div className={styles.recipientSummary}>{clueRecipientLabel}</div>
                          </div>
                          <div className={styles.recipientShortcuts}>
                            <button
                              className={styles.inlineButton}
                              onClick={() => setSelectedClueTeamNames(leaderboard.map((team) => team.name))}
                              type="button"
                            >
                              All teams
                            </button>
                            <button className={styles.inlineButton} onClick={() => setSelectedClueTeamNames([])} type="button">
                              Clear
                            </button>
                          </div>
                        </div>

                        <div className={styles.teamDropdown} ref={teamDropdownRef}>
                          <button
                            aria-expanded={isTeamDropdownOpen}
                            className={styles.teamDropdownButton}
                            onClick={() => setIsTeamDropdownOpen((current) => !current)}
                            type="button"
                          >
                            {clueRecipientLabel}
                          </button>
                          {isTeamDropdownOpen && (
                            <div className={styles.teamMenu}>
                              {leaderboard.map((team) => (
                                <label className={styles.teamOption} key={team.name}>
                                  <input
                                    checked={selectedClueTeamNames.includes(team.name)}
                                    onChange={() => toggleClueTeam(team.name)}
                                    type="checkbox"
                                  />
                                  <span className={styles.teamBadge}>{team.badge}</span>
                                  <span>{team.name}</span>
                                </label>
                              ))}
                            </div>
                          )}
                        </div>
                      </div>
                      <div className={styles.clueActions}>
                        <button className={styles.primaryButton} disabled={selectedClueTeams.length === 0} onClick={releaseClue} type="button">
                          Release clue now
                        </button>
                        <button className={styles.secondaryButton} type="button">
                          Schedule next clue
                        </button>
                      </div>
                    </div>
                  </section>

                  <section className={styles.panel} aria-labelledby="clue-history-title">
                    <div className={styles.subsectionHeader}>
                      <h3 id="clue-history-title">Clue history</h3>
                    </div>

                    <div className={styles.historyList}>
                      {clueHistory.map((item, index) => (
                        <div className={styles.historyItem} key={item.id}>
                          <div className={styles.historyMain}>
                            <span className={styles.tinyBadge}>{index + 1}</span>
                            <span>{item.title}</span>
                          </div>
                          <div className={styles.historyTime}>{item.time}</div>
                        </div>
                      ))}
                    </div>
                  </section>
                </div>

                <div className={styles.stack}>
                  <section className={styles.panel} aria-labelledby="queue-title">
                    <div className={styles.panelHeader}>
                      <div>
                        <h2 id="queue-title">QR submission queue</h2>
                        <div className={styles.panelMeta}>QR-first monitoring with backend-driven validation, not manual approval.</div>
                      </div>
                      <span className={styles.chip} data-tone="warning">
                        3 pending
                      </span>
                    </div>

                    <div className={styles.queueList}>
                      {submissions.map((submission) => (
                        <article className={styles.queueItem} key={submission.id}>
                          <div className={styles.evidenceThumb}>{submission.thumbnail}</div>
                          <div className={styles.queueSummary}>
                            <div className={styles.queueItemHeader}>
                              <div>
                                <h3 className={styles.queueTitle}>
                                  {submission.team} · {submission.clue}
                                </h3>
                                <div className={styles.queueDescription}>{submission.target}</div>
                              </div>
                              <span className={styles.chip} data-tone={reviewTones[submission.status]}>
                                {submission.status}
                              </span>
                            </div>

                            <div className={styles.queueContext}>
                              <span>Validated: {submission.validatedAt}</span>
                              <span>Submitted: {submission.timestamp}</span>
                              {submission.rejectionReason && <span>Reason: {submission.rejectionReason}</span>}
                            </div>

                            <div className={styles.panelActions}>
                              <button className={styles.inlineButton} onClick={() => openSubmission(submission.id)} type="button">
                                View audit detail
                              </button>
                            </div>
                          </div>
                        </article>
                      ))}
                    </div>
                  </section>

                  <section className={styles.panel} aria-labelledby="activity-title">
                    <div className={styles.subsectionHeader}>
                      <h3 id="activity-title">Recent activity</h3>
                      <span className={derivedSession.state === 'live' ? styles.liveChip : styles.chip} data-tone={derivedSession.state === 'live' ? undefined : 'warning'}>
                        {stateLabels[derivedSession.state]}
                      </span>
                    </div>

                    <div className={styles.activityList}>
                      {activity.map((item) => (
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
                  </section>
                </div>
              </div>

              <div className={styles.bottomGrid}>
                <section className={styles.panel} aria-labelledby="mission-progress-title">
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 id="mission-progress-title">Mission progress</h2>
                      <div className={styles.panelMeta}>Checkpoint visibility stays glanceable even when the grid gets dense.</div>
                    </div>
                  </div>

                  <table className={styles.missionTable}>
                    <thead>
                      <tr>
                        <th>Team</th>
                        {['1', '2', '3', '4', '5', '6', 'Finish'].map((step) => (
                          <th data-align="center" key={step}>
                            {step}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {progressRows.map((row) => (
                        <tr key={row.team}>
                          <td data-label="Team">
                            <div className={styles.teamCell}>
                              <span className={styles.teamBadge}>{row.badge}</span>
                              <span>{row.team}</span>
                            </div>
                          </td>
                          {row.checkpoints.map((state, index) => (
                            <td data-align="center" data-label={`Step ${index + 1}`} key={`${row.team}-${index}`}>
                              <span
                                className={`${styles.progressIcon} ${
                                  state === 'done'
                                    ? styles.progressDone
                                    : state === 'current'
                                      ? styles.progressCurrent
                                      : styles.progressPending
                                }`}
                              >
                                {state === 'done' ? '✓' : state === 'current' ? '◎' : '○'}
                              </span>
                            </td>
                          ))}
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
                      <span className={styles.controlTitle}>Pause hunt</span>
                      <span className={styles.controlCopy}>Freeze countdown and clue releases.</span>
                    </button>
                    <button className={styles.controlTile} onClick={() => updateSessionState('live')} type="button">
                      <span className={styles.controlKicker}>▷</span>
                      <span className={styles.controlTitle}>Resume hunt</span>
                      <span className={styles.controlCopy}>Return teams to live progression.</span>
                    </button>
                    <button className={styles.controlTile} data-tone="accent" onClick={releaseClue} type="button">
                      <span className={styles.controlKicker}>◉</span>
                      <span className={styles.controlTitle}>Reveal clue</span>
                      <span className={styles.controlCopy}>Send the next clue to the active field.</span>
                    </button>
                    <button className={styles.controlTile} data-tone="success" type="button">
                      <span className={styles.controlKicker}>▣</span>
                      <span className={styles.controlTitle}>Validate checkpoint</span>
                      <span className={styles.controlCopy}>Open the audit drawer for rejected or retried scans.</span>
                    </button>
                    <button className={styles.controlTile} data-tone="critical" type="button">
                      <span className={styles.controlKicker}>⚑</span>
                      <span className={styles.controlTitle}>Apply penalty</span>
                      <span className={styles.controlCopy}>Log score deductions with reason tracking.</span>
                    </button>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>✉</span>
                      <span className={styles.controlTitle}>Send announcement</span>
                      <span className={styles.controlCopy}>Broadcast delays, hints, or safety notices.</span>
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
                        <h1 id="admin-title">Operator coverage and event orchestration</h1>
                        <span className={styles.chip} data-tone={isConnected ? 'success' : 'critical'}>
                          {isConnected ? 'Global view live' : 'Signal degraded'}
                        </span>
                      </div>
                      <div className={styles.sessionMeta}>
                        <span>Tonight&apos;s events</span>
                        <span>•</span>
                        <span>7 sessions in circulation</span>
                        <span>•</span>
                        <span>Operator coverage review at 8:15 PM</span>
                      </div>
                    </div>
                  </div>

                  <div className={styles.heroActions}>
                    <button className={styles.secondaryButton} type="button">
                      Assign operators
                    </button>
                    <button className={styles.primaryButton} type="button">
                      Create mission session
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
                      <div className={styles.panelMeta}>Mission, quiz, team, and rules management stays close to the top of the surface.</div>
                    </div>
                  </div>
                  <div className={styles.controlGrid}>
                    <button className={styles.controlTile} type="button">
                      <span className={styles.controlKicker}>◎</span>
                      <span className={styles.controlTitle}>Missions</span>
                      <span className={styles.controlCopy}>Tune route logic, clue cadence, and scoring rules.</span>
                    </button>
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
                      <h2>Operator assignments</h2>
                      <div className={styles.panelMeta}>Global read-only history stays separate from live steering, but assignment clarity is immediate.</div>
                    </div>
                  </div>
                  <div className={styles.assignmentContent}>
                    <ul>
                      {operatorAssignments.map((assignment) => (
                        <li key={assignment}>{assignment}</li>
                      ))}
                    </ul>
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

      {selectedSubmission && (
        <aside aria-labelledby="audit-title" className={styles.detailDrawer}>
          <div className={styles.drawerHeader}>
            <div>
              <h2 id="audit-title">Submission audit</h2>
              <div className={styles.drawerMeta}>
                {selectedSubmission.team} · {selectedSubmission.clue} · {selectedSubmission.target}
              </div>
            </div>
            <button className={styles.drawerClose} onClick={closeSubmission} type="button">
              ✕
            </button>
          </div>

          <div className={styles.drawerBody}>
            <div className={styles.evidenceThumb}>{selectedSubmission.thumbnail}</div>
            <span className={styles.chip} data-tone={reviewTones[selectedSubmission.status]}>
              {selectedSubmission.status}
            </span>
            <p className={styles.mutedText}>{selectedSubmission.note}</p>
            {selectedSubmission.rejectionReason && (
              <p className={styles.mutedText}>Rejection reason: {selectedSubmission.rejectionReason}</p>
            )}

            <section>
              <div className={styles.subsectionHeader}>
                <h3>Audit timeline</h3>
              </div>
              <div className={styles.drawerTimeline}>
                <div className={styles.timelineItem}>
                  <div className={styles.timelineTime}>{selectedSubmission.timestamp}</div>
                  <div className={styles.timelineMain}>Submission entered the SignalR transport stream.</div>
                </div>
                <div className={styles.timelineItem}>
                  <div className={styles.timelineTime}>{selectedSubmission.validatedAt}</div>
                  <div className={styles.timelineMain}>Backend validator resolved QR ownership and target context.</div>
                </div>
                <div className={styles.timelineItem}>
                  <div className={styles.timelineTime}>8:00:37 PM</div>
                  <div className={styles.timelineMain}>Operator dashboard received the final status packet and refreshed the queue row inline.</div>
                </div>
              </div>
            </section>
          </div>
        </aside>
      )}

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
