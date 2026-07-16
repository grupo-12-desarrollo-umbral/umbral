// HU-24B: the ranking panel's resolution time arrives as a raw .NET TimeSpan. These lock the formatting
// of the shapes the backend actually emits — including the day-carrying, tick-precise one that rendered
// verbatim as "1.05:05:07.5499560" in the operator standings.
import { describe, expect, it } from 'vitest'

import { formatResolutionTime } from '@/app/dashboard/resolution-time'

describe('formatResolutionTime', () => {
  it('formats a day-carrying, tick-precise span instead of leaking the raw TimeSpan', () => {
    // The exact value seen in the dashboard; the whole reason this helper exists.
    expect(formatResolutionTime('1.05:05:07.5499560')).toBe('1d 05h 05m')
  })

  it('formats a minutes-only span', () => {
    expect(formatResolutionTime('00:05:00')).toBe('5m 00s')
  })

  it('formats an hours-carrying span', () => {
    expect(formatResolutionTime('02:07:30')).toBe('2h 07m 30s')
  })

  it('formats a seconds-only span', () => {
    expect(formatResolutionTime('00:00:42')).toBe('42s')
  })

  it('formats a zero span', () => {
    expect(formatResolutionTime('00:00:00')).toBe('0s')
  })

  it('drops fractional seconds without disturbing the whole seconds', () => {
    // Sub-second precision is display noise: the backend already applied it to Position/order.
    expect(formatResolutionTime('00:07:30.9990000')).toBe('7m 30s')
  })

  it('degrades an unrecognized value to the raw string rather than hiding or throwing', () => {
    expect(formatResolutionTime('not-a-timespan')).toBe('not-a-timespan')
    // Negative spans are rejected by the domain, so they are treated as any other mismatch.
    expect(formatResolutionTime('-00:05:00')).toBe('-00:05:00')
  })
})
