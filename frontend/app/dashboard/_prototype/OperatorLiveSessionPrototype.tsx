'use client'

/*
 * PROTOTYPE — three throwaway layouts for the cluttered operator live-session view,
 * switchable via `?variant=` and a floating bottom bar (dev only).
 *
 *   current — today's single stacked column (baseline for comparison)
 *   A       — Tabbed workspace: sticky timer + tabs, one group visible at a time
 *   B       — Two-column console: "Monitor" (watch) beside "Operate" (act)
 *   C       — Command dock + accordion: cockpit up top, everything else collapsed
 *
 * The real panels are passed in as `slots` (already-built nodes) so all data fetching,
 * state and mutations stay in DashboardClient — this file only decides arrangement.
 * Once a winner is picked, fold it into DashboardClient and delete this folder.
 */

import { useCallback, useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import styles from './operatorLiveVariants.module.css'

const VARIANT_EVENT = 'prototype:variant-change'

export interface OperatorLiveSlots {
  hero: ReactNode
  timer: ReactNode
  missionTimer: ReactNode
  teamProgress: ReactNode
  ranking: ReactNode
  evidence: ReactNode
  history: ReactNode
  activity: ReactNode
  cluesRow: ReactNode
  triviaRound: ReactNode
  answeredMonitor: ReactNode
  answerReview: ReactNode
  controls: ReactNode
  detail: ReactNode
  banners: ReactNode
}

type VariantKey = 'current' | 'A' | 'B' | 'C'

const VARIANTS: { key: VariantKey; name: string }[] = [
  { key: 'current', name: 'Current (stacked)' },
  { key: 'A', name: 'Tabbed workspace' },
  { key: 'B', name: 'Two-column console' },
  { key: 'C', name: 'Command dock + accordion' },
]

function readVariant(): VariantKey {
  if (typeof window === 'undefined') return 'current'
  const raw = new URLSearchParams(window.location.search).get('variant')
  return VARIANTS.some((v) => v.key === raw) ? (raw as VariantKey) : 'current'
}

// The `?variant=` search param is the source of truth — read it via useSyncExternalStore
// (mirrors DashboardClient's theme/sidebar stores) so SSR falls back to 'current' with no
// hydration mismatch and no setState-in-effect.
function subscribeVariant(onChange: () => void) {
  window.addEventListener('popstate', onChange)
  window.addEventListener(VARIANT_EVENT, onChange)
  return () => {
    window.removeEventListener('popstate', onChange)
    window.removeEventListener(VARIANT_EVENT, onChange)
  }
}

export function OperatorLiveSessionPrototype({ slots }: { slots: OperatorLiveSlots }) {
  const variant = useSyncExternalStore(subscribeVariant, readVariant, () => 'current' as VariantKey)

  const changeVariant = useCallback((next: VariantKey) => {
    const url = new URL(window.location.href)
    if (next === 'current') url.searchParams.delete('variant')
    else url.searchParams.set('variant', next)
    window.history.replaceState(null, '', url)
    window.dispatchEvent(new Event(VARIANT_EVENT))
  }, [])

  const cycle = useCallback(
    (dir: 1 | -1) => {
      const i = VARIANTS.findIndex((v) => v.key === variant)
      const next = VARIANTS[(i + dir + VARIANTS.length) % VARIANTS.length]
      changeVariant(next.key)
    },
    [variant, changeVariant],
  )

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return
      const el = document.activeElement as HTMLElement | null
      const tag = el?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || el?.isContentEditable) return
      cycle(e.key === 'ArrowRight' ? 1 : -1)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [cycle])

  return (
    <>
      {variant === 'current' && <CurrentLayout slots={slots} />}
      {variant === 'A' && <TabbedLayout slots={slots} />}
      {variant === 'B' && <TwoColumnLayout slots={slots} />}
      {variant === 'C' && <DockLayout slots={slots} />}

      {process.env.NODE_ENV !== 'production' && (
        <PrototypeSwitcher current={variant} onCycle={cycle} />
      )}
    </>
  )
}

/* ---------------- current: faithful stacked baseline ---------------- */
function CurrentLayout({ slots }: { slots: OperatorLiveSlots }) {
  return (
    <div className={styles.wrap}>
      {slots.hero}
      {slots.timer}
      {slots.missionTimer}
      {slots.teamProgress}
      {slots.ranking}
      {slots.evidence}
      {slots.history}
      {slots.activity}
      {slots.cluesRow}
      {slots.triviaRound}
      {slots.answeredMonitor}
      {slots.answerReview}
      {slots.banners}
      <div className={styles.fullRow}>
        {slots.controls}
        {slots.detail}
      </div>
    </div>
  )
}

/* ---------------- A: tabbed workspace ---------------- */
function TabbedLayout({ slots }: { slots: OperatorLiveSlots }) {
  const tabs: { key: string; label: string; content: ReactNode }[] = [
    {
      key: 'live',
      label: 'Live',
      content: (
        <>
          {slots.teamProgress}
          {slots.ranking}
        </>
      ),
    },
    {
      key: 'clues',
      label: 'Clues & penalties',
      content: <>{slots.cluesRow}</>,
    },
    {
      key: 'evidence',
      label: 'Evidence',
      content: <>{slots.evidence}</>,
    },
    {
      key: 'trivia',
      label: 'Trivia',
      content: (
        <>
          {slots.answeredMonitor}
          {slots.answerReview}
        </>
      ),
    },
    {
      key: 'timeline',
      label: 'Timeline',
      content: (
        <>
          {slots.history}
          {slots.activity}
        </>
      ),
    },
    {
      key: 'controls',
      label: 'Controls',
      content: (
        <>
          {slots.controls}
          {slots.detail}
        </>
      ),
    },
  ]
  const [active, setActive] = useState(tabs[0].key)
  const current = tabs.find((t) => t.key === active) ?? tabs[0]

  return (
    <div className={styles.wrap}>
      <div className={styles.stickyRail}>
        {slots.hero}
        {/* Both clocks stay pinned: question timer + whole-mission timer, side by side. */}
        <div className={styles.railTimers}>
          {slots.timer}
          {slots.missionTimer}
        </div>
        {/* Pinned too: the round panel (pregame "Get ready" countdown / live question / advancing).
            Renders null when idle, so it costs no space until a round is running — this is what
            makes the post-Activate countdown visible regardless of the active tab. */}
        {slots.triviaRound}
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

      {slots.banners}
    </div>
  )
}

/* ---------------- B: two-column console ---------------- */
function TwoColumnLayout({ slots }: { slots: OperatorLiveSlots }) {
  return (
    <div className={styles.wrap}>
      {slots.hero}

      <div className={styles.twoCol}>
        <div className={styles.col}>
          <div className={styles.colHead}>
            <span className={styles.colDot} data-tone="watch" /> Monitor
          </div>
          {slots.timer}
          {slots.missionTimer}
          {slots.teamProgress}
          {slots.ranking}
          {slots.evidence}
        </div>

        <div className={styles.col}>
          <div className={styles.colHead}>
            <span className={styles.colDot} data-tone="act" /> Operate
          </div>
          {slots.controls}
          {slots.cluesRow}
          {slots.triviaRound}
          {slots.answeredMonitor}
          {slots.answerReview}
        </div>
      </div>

      <div className={styles.fullRow}>
        {slots.history}
        {slots.activity}
      </div>

      {slots.detail}
      {slots.banners}
    </div>
  )
}

/* ---------------- C: command dock + accordion ---------------- */
function Section({
  title,
  tag,
  defaultOpen = false,
  children,
}: {
  title: string
  tag?: string
  defaultOpen?: boolean
  children: ReactNode
}) {
  return (
    <details className={styles.section} open={defaultOpen}>
      <summary>
        {tag && <span className={styles.sectionTag}>{tag}</span>}
        {title}
      </summary>
      <div className={styles.sectionBody}>{children}</div>
    </details>
  )
}

function DockLayout({ slots }: { slots: OperatorLiveSlots }) {
  return (
    <div className={styles.wrap}>
      <div className={styles.dock}>
        <div className={styles.dockCol}>
          {slots.hero}
          {slots.timer}
          {slots.controls}
        </div>
        <div className={styles.dockCol}>
          {slots.missionTimer}
          {slots.teamProgress}
        </div>
      </div>

      <div className={styles.accordion}>
        <Section title="Ranking" tag="Live" defaultOpen>
          {slots.ranking}
        </Section>
        <Section title="Evidence submissions" tag="Live">
          {slots.evidence}
        </Section>
        <Section title="Clues & penalties" tag="Act">
          {slots.cluesRow}
        </Section>
        <Section title="Trivia" tag="Act">
          {slots.triviaRound}
          {slots.answeredMonitor}
          {slots.answerReview}
        </Section>
        <Section title="Timeline" tag="Log">
          {slots.history}
          {slots.activity}
        </Section>
        <Section title="Session detail" tag="Info">
          {slots.detail}
        </Section>
      </div>

      {slots.banners}
    </div>
  )
}

/* ---------------- floating switcher (dev only) ---------------- */
function PrototypeSwitcher({
  current,
  onCycle,
}: {
  current: VariantKey
  onCycle: (dir: 1 | -1) => void
}) {
  const meta = VARIANTS.find((v) => v.key === current) ?? VARIANTS[0]
  return (
    <div className={styles.switcher} role="group" aria-label="Prototype variant switcher">
      <button type="button" className={styles.switchArrow} aria-label="Previous variant" onClick={() => onCycle(-1)}>
        ‹
      </button>
      <div className={styles.switchLabel}>
        <span className={styles.switchKey}>Variant {meta.key === 'current' ? '·' : meta.key}</span>
        <span className={styles.switchName}>{meta.name}</span>
        <span className={styles.switchHint}>← / → to switch · prototype</span>
      </div>
      <button type="button" className={styles.switchArrow} aria-label="Next variant" onClick={() => onCycle(1)}>
        ›
      </button>
    </div>
  )
}
