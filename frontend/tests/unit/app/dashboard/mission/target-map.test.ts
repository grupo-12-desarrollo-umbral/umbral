// @vitest-environment jsdom
// #156 (operator side): the TargetMap host renders the Leaflet iframe, forwards a pick posted from the
// iframe to onPick, ignores foreign/malformed messages, and shows the empty state only when there is
// nothing to show and no picking to do.
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { TargetMap } from '@/app/dashboard/mission/TargetMap'
import { TARGET_PICK_MESSAGE, type TargetMapMarker } from '@/app/dashboard/mission/target-map-html'

vi.mock('@/app/dashboard/dashboard.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

const markers: TargetMapMarker[] = [{ id: '1', name: 'Waterfall', latitude: 10, longitude: -70 }]

let container: HTMLElement
let root: Root

async function mount(props: Parameters<typeof TargetMap>[0]) {
  container = document.createElement('div')
  document.body.appendChild(container)
  root = createRoot(container)
  await act(async () => {
    root.render(createElement(TargetMap, props))
  })
}

async function postPick(data: unknown) {
  await act(async () => {
    window.dispatchEvent(new MessageEvent('message', { data }))
  })
}

afterEach(() => {
  act(() => root.unmount())
  container.remove()
})

describe('TargetMap', () => {
  it('renders the Leaflet iframe with a pin per marker', async () => {
    await mount({ markers, testId: 'tm' })
    const iframe = container.querySelector<HTMLIFrameElement>('[data-testid="tm"]')
    expect(iframe).not.toBeNull()
    expect(iframe!.srcdoc).toContain('"name":"Waterfall"')
  })

  it('forwards an interactive pick to onPick', async () => {
    const onPick = vi.fn()
    await mount({ interactive: true, onPick, testId: 'tm' })
    await postPick({ type: TARGET_PICK_MESSAGE, latitude: 12.34, longitude: -56.78 })
    expect(onPick).toHaveBeenCalledWith(12.34, -56.78)
  })

  it('ignores messages of a foreign type or with non-numeric coordinates', async () => {
    const onPick = vi.fn()
    await mount({ interactive: true, onPick, testId: 'tm' })
    await postPick({ type: 'something-else', latitude: 1, longitude: 2 })
    await postPick({ type: TARGET_PICK_MESSAGE, latitude: 'nope', longitude: 2 })
    expect(onPick).not.toHaveBeenCalled()
  })

  it('does not react to picks when not interactive', async () => {
    const onPick = vi.fn()
    await mount({ markers, onPick, testId: 'tm' })
    await postPick({ type: TARGET_PICK_MESSAGE, latitude: 1, longitude: 2 })
    expect(onPick).not.toHaveBeenCalled()
  })

  it('shows the empty state when passive with nothing placed', async () => {
    await mount({ testId: 'tm' })
    expect(container.querySelector('[data-testid="tm-empty"]')).not.toBeNull()
    expect(container.querySelector('[data-testid="tm"]')).toBeNull()
  })
})
