'use client'

// Operator live-session workspace: a sticky rail (session header + both clocks + the live
// round/pregame countdown) over a tab bar, so only one panel group is on screen at a time
// instead of one long scroll. DashboardClient owns all data/state/mutations and passes the
// built panels in as nodes; this component only arranges them.

import { useState, type ReactNode } from 'react'
import styles from './operatorLiveSession.module.css'

export interface OperatorLivePanels {
  hero: ReactNode
  timer: ReactNode
  missionTimer: ReactNode
  triviaRound: ReactNode
  teamProgress: ReactNode
  ranking: ReactNode
  evidence: ReactNode
  history: ReactNode
  activity: ReactNode
  cluesRow: ReactNode
  answeredMonitor: ReactNode
  answerReview: ReactNode
  controls: ReactNode
  detail: ReactNode
  banners: ReactNode
}

export function OperatorLiveSession({ panels }: { panels: OperatorLivePanels }) {
  const tabs: { key: string; label: string; content: ReactNode }[] = [
    {
      key: 'live',
      label: 'Live',
      content: (
        <>
          {panels.teamProgress}
          {panels.ranking}
        </>
      ),
    },
    {
      key: 'clues',
      label: 'Clues & penalties',
      content: <>{panels.cluesRow}</>,
    },
    {
      key: 'evidence',
      label: 'Evidence',
      content: <>{panels.evidence}</>,
    },
    {
      key: 'trivia',
      label: 'Trivia',
      content: (
        <>
          {panels.answeredMonitor}
          {panels.answerReview}
        </>
      ),
    },
    {
      key: 'timeline',
      label: 'Timeline',
      content: (
        <>
          {panels.history}
          {panels.activity}
        </>
      ),
    },
    {
      key: 'controls',
      label: 'Controls',
      content: (
        <>
          {panels.controls}
          {panels.detail}
        </>
      ),
    },
  ]
  const [active, setActive] = useState(tabs[0].key)
  const current = tabs.find((t) => t.key === active) ?? tabs[0]

  return (
    <div className={styles.wrap}>
      <div className={styles.stickyRail}>
        {panels.hero}
        {/* Both clocks stay pinned: question timer + whole-mission timer, side by side. */}
        <div className={styles.railTimers}>
          {panels.timer}
          {panels.missionTimer}
        </div>
        {/* Pinned too: the round panel (pregame "Get ready" countdown / live question / advancing).
            Renders null when idle, so it costs no space until a round is running — this keeps the
            post-Activate pregame countdown visible regardless of the active tab. */}
        {panels.triviaRound}
        <div className={styles.tabBar} role="tablist" aria-label="Live session sections">
          {tabs.map((t) => (
            <button
              key={t.key}
              type="button"
              role="tab"
              aria-selected={active === t.key}
              data-active={active === t.key}
              className={styles.tabButton}
              onClick={() => setActive(t.key)}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <div className={styles.tabPanel} role="tabpanel">
        {current.content}
      </div>

      {panels.banners}
    </div>
  )
}
