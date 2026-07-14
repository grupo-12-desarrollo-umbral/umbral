// #156 (operator side): the Leaflet HTML builder centres correctly, renders a pin per target, wires
// click-to-pick only when interactive, and neutralises `<` in operator-authored names so a name can
// never break out of the inlined <script>.
import { describe, expect, it } from 'vitest'
import {
  buildTargetMapHtml,
  TARGET_PICK_MESSAGE,
  type TargetMapMarker,
} from '@/app/dashboard/mission/target-map-html'

const markers: TargetMapMarker[] = [
  { id: '1', name: 'Waterfall', latitude: 10.5, longitude: -70.25 },
  { id: '2', name: 'Cavern', latitude: 11, longitude: -71 },
]

describe('buildTargetMapHtml', () => {
  it('centres on the draft when one is provided', () => {
    const html = buildTargetMapHtml({ markers, draft: { latitude: 40.1, longitude: 5.2 } })
    expect(html).toContain('setView([40.1, 5.2]')
  })

  it('falls back to the first marker when there is no draft', () => {
    const html = buildTargetMapHtml({ markers })
    expect(html).toContain('setView([10.5, -70.25]')
  })

  it('opens on a zoomed-out world view when nothing is placed', () => {
    const html = buildTargetMapHtml({})
    expect(html).toContain('setView([0, 0], 2)')
  })

  it('serialises every marker into the payload', () => {
    const html = buildTargetMapHtml({ markers })
    expect(html).toContain('"name":"Waterfall"')
    expect(html).toContain('"name":"Cavern"')
  })

  it('enables the pick flow and hint only in interactive mode', () => {
    const passive = buildTargetMapHtml({ markers })
    expect(passive).toContain('var interactive = false;')
    expect(passive).not.toContain('Click the map to place this target')

    const interactive = buildTargetMapHtml({ markers, interactive: true })
    expect(interactive).toContain('var interactive = true;')
    expect(interactive).toContain(TARGET_PICK_MESSAGE)
    expect(interactive).toContain('Click the map to place this target')
  })

  it('escapes < in a target name to prevent script-block injection', () => {
    const evil: TargetMapMarker[] = [
      { id: 'x', name: '</script><img src=x>', latitude: 0, longitude: 0 },
    ]
    const html = buildTargetMapHtml({ markers: evil })
    expect(html).not.toContain('</script><img')
    expect(html).toContain('\\u003c/script>')
  })
})
