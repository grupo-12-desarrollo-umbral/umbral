'use client'

import { useEffect, useMemo, useRef } from 'react'
import {
  buildTargetMapHtml,
  TARGET_PICK_MESSAGE,
  type TargetMapDraft,
  type TargetMapMarker,
} from './target-map-html'
import styles from '../dashboard.module.css'

// Leaflet map for the mission editor. In `interactive` mode the operator clicks to place a target and
// `onPick` receives the coordinates; otherwise it is a read-only overview of the substage's targets.
// The map lives in an <iframe srcDoc> so the CDN Leaflet/OSM assets never touch the host page's scope;
// picks flow back over postMessage.
export function TargetMap({
  markers = [],
  draft = null,
  interactive = false,
  onPick,
  testId = 'target-map',
  label = 'Target map',
}: {
  markers?: readonly TargetMapMarker[]
  draft?: TargetMapDraft | null
  interactive?: boolean
  onPick?: (latitude: number, longitude: number) => void
  testId?: string
  label?: string
}) {
  // Keep the latest onPick in a ref so the message listener is registered once and never goes stale.
  const onPickRef = useRef(onPick)
  useEffect(() => {
    onPickRef.current = onPick
  }, [onPick])

  useEffect(() => {
    if (!interactive) return
    function handle(event: MessageEvent) {
      const data = event.data
      if (!data || data.type !== TARGET_PICK_MESSAGE) return
      const { latitude, longitude } = data
      if (typeof latitude !== 'number' || typeof longitude !== 'number') return
      onPickRef.current?.(latitude, longitude)
    }
    window.addEventListener('message', handle)
    return () => window.removeEventListener('message', handle)
  }, [interactive])

  // Re-derive srcDoc only when the placed pins, draft, or mode change — not on every parent render.
  const srcDoc = useMemo(
    () => buildTargetMapHtml({ markers, draft, interactive }),
    [markers, draft, interactive],
  )

  const hasContent = markers.length > 0 || draft !== null
  if (!interactive && !hasContent) {
    return (
      <div className={styles.mapEmpty} data-testid={`${testId}-empty`}>
        No target has a map location yet.
      </div>
    )
  }

  return (
    <iframe
      className={styles.mapFrame}
      data-testid={testId}
      title={label}
      srcDoc={srcDoc}
      // Scripts run the Leaflet bundle; the iframe stays cross-origin (no allow-same-origin) so it can
      // only reach the host via postMessage.
      sandbox="allow-scripts"
    />
  )
}
